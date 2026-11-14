using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("local.valheim.mapvalidation", "Map Validation", "0.1.0")]
[BepInDependency("local.valheim.biomeclarity")]
[BepInDependency("j1ga.livingmap")]
[BepInDependency("ru.ivest.farlands", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("local.valheim.progressivemap", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("magicorp.valheim.rememberserverpassword", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("local.valheim.quickconnectcrossplayfix", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class MapValidation : BaseUnityPlugin
{
    private string output;
    private bool started, finished;
    private float deadline;
    private bool profileFarLands;
    private bool testProgressiveMap;
    private bool testQuickConnect;
    private const int Size = 128;
    private static string saveDirectory;
    private static bool SavePath(ref string __result) { __result = saveDirectory; return false; }
    private static bool NoCloud(ref bool __result) { __result = false; return false; }
    private static void TestMapSize(Minimap __instance)
    {
        int size;
        if (int.TryParse(Environment.GetEnvironmentVariable("VALHEIM_PROGRESSIVE_MAP_SIZE"), out size))
        {
            if (size != 512 && size != 2048) throw new Exception("Unsupported validation map size");
            Set(__instance, "m_textureSize", size);
        }
    }
    private static object Get(object o, string name) { return Field(o, name).GetValue(o); }
    private static void Set(object o, string name, object value) { Field(o, name).SetValue(o, value); }
    private static FieldInfo Field(object o, string name) { return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
    private static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args); }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private void Awake()
    {
        output = Environment.GetEnvironmentVariable("VALHEIM_MAP_VALIDATION_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        saveDirectory = Path.Combine(output, "saves");
        Utils.SetSaveDataPath(saveDirectory);
        var isolation = new Harmony("local.valheim.mapvalidation.isolation");
        isolation.Patch(AccessTools.Method(typeof(Utils), "GetSaveDataPath"), prefix: new HarmonyMethod(typeof(MapValidation), "SavePath"));
        isolation.Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupportedAndEnabled"), prefix: new HarmonyMethod(typeof(MapValidation), "NoCloud"));
        profileFarLands = Environment.GetEnvironmentVariable("VALHEIM_FARLANDS_PROFILE") == "1";
        testProgressiveMap = Environment.GetEnvironmentVariable("VALHEIM_PROGRESSIVE_MAP_TEST") == "1";
        testQuickConnect = Environment.GetEnvironmentVariable("VALHEIM_QUICK_CONNECT_TEST") == "1";
        if (testProgressiveMap) isolation.Patch(AccessTools.Method(typeof(Minimap), "Start"), prefix: new HarmonyMethod(typeof(MapValidation), "TestMapSize"));
        if (profileFarLands) FarLandsProfile.Init();
        deadline = Time.realtimeSinceStartup + 240;
    }

    private void Update()
    {
        if (finished || string.IsNullOrEmpty(output)) return;
        try
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Timed out waiting for the local test scene");
            if (!started)
            {
                FejdStartup menu = FindFirstObjectByType<FejdStartup>();
                if (menu == null || Time.realtimeSinceStartup < 8) return;
                if (testQuickConnect)
                {
                    QuickConnectChecks.Run(menu, output);
                    finished = true;
                    Application.Quit(0);
                    return;
                }
                started = true;
                Check(Utils.GetSaveDataPath(FileHelpers.FileSource.Local) == saveDirectory, "Save path isolation failed");
                var profile = new PlayerProfile("MapValidation", FileHelpers.FileSource.Local);
                profile.SetName("MapValidation");
                Check(profile.Save(), "Could not save the isolated test profile");
                Game.SetProfile("MapValidation", FileHelpers.FileSource.Local);
                World world = new World("MapValidation", "MapTest123");
                world.m_uid = 9182746501L;
                world.m_fileSource = FileHelpers.FileSource.Local;
                ZNet.SetServer(true, false, false, "MapValidation", "", world);
                ZNet.ResetServerHost();
                Call(menu, "LoadMainScene");
                Logger.LogInfo("Loading isolated local test world; no remote server connection.");
                return;
            }
            Minimap map = Minimap.instance;
            if (map == null || Get(map, "m_mapTexture") == null || ZNet.instance == null) return;
            if (testProgressiveMap)
            {
                if (!ProgressiveMapChecks.Tick(map, output)) return;
                finished = true;
                Application.Quit(0);
                return;
            }
            if (profileFarLands)
            {
                if (!FarLandsProfile.Tick(output)) return;
                finished = true;
                Application.Quit(0);
                return;
            }
            finished = true;
            Validate(map);
            Application.Quit(0);
        }
        catch (Exception e)
        {
            finished = true;
            File.WriteAllText(Path.Combine(output, "FAILED.txt"), e.ToString());
            Logger.LogError(e);
            Application.Quit(1);
        }
    }

    private static Texture2D Texture(Color[] pixels, TextureFormat format)
    {
        var t = new Texture2D(Size, Size, format, false, format == TextureFormat.RGBAFloat);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Point;
        t.SetPixels(pixels); t.Apply(); return t;
    }

    private void Validate(Minimap map)
    {
        var biome = Chainloader.PluginInfos["local.valheim.biomeclarity"].Instance;
        var living = Chainloader.PluginInfos["j1ga.livingmap"].Instance;
        living.enabled = false;
        Call(living, "Teardown", true);
        // Synthetic data, real game material and real LivingMap reconstruction/bridge.
        Color meadow = (Color)Get(map, "m_meadowsColor"), forest = (Color)Get(map, "m_blackforestColor");
        Color[] colors = new Color[Size * Size], heights = new Color[colors.Length], masks = new Color[colors.Length], fogs = new Color[colors.Length];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            int i = y * Size + x;
            colors[i] = x < 48 ? Color.gray : (x < 80 ? meadow : forest);
            heights[i] = new Color(x < 48 ? 10f : 35f, 0, 0, 1);
            masks[i] = new Color(x >= 80 ? 1f : 0f, 0, 0, 1);
            fogs[i] = Color.clear;
        }
        Texture2D source = Texture(colors, TextureFormat.RGBA32);
        Texture2D height = Texture(heights, TextureFormat.RGBAFloat);
        Texture2D mask = Texture(masks, TextureFormat.RGBA32);
        Texture2D fog = Texture(fogs, TextureFormat.RGBA32);
        Set(map, "m_textureSize", Size); Set(map, "m_mapTexture", source);
        Set(map, "m_heightTexture", height); Set(map, "m_forestMaskTexture", mask); Set(map, "m_fogTexture", fog);
        foreach (string imageName in new[] { "m_mapImageSmall", "m_mapImageLarge" })
        {
            object image = Get(map, imageName);
            Material imageMaterial = (Material)image.GetType().GetProperty("material").GetValue(image, null);
            imageMaterial.SetTexture("_MaskTex", mask);
        }
        Material material = new Material((Material)Get(map, "m_mapSmallShader"));
        material.SetTexture("_HeightTex", height); material.SetTexture("_MaskTex", mask); material.SetTexture("_FogTex", fog);
        material.SetTextureScale("_MainTex", new Vector2(0.5f, 0.5f)); material.SetTextureOffset("_MainTex", new Vector2(0.25f, 0.25f));
        material.SetFloat("_zoom", 1000f); material.SetFloat("_pixelSize", 1000f);
        material.SetVector("_mapCenter", new Vector4(0.5f, 0.5f, 0, 0));
        material.SetTexture("_CloudTex", Texture2D.blackTexture);
        material.SetVector("_lightDir", new Vector4(0, 1, 0, 0));
        material.SetColor("_lightColor", Color.white); material.SetColor("_ambientLightColor", new Color(0.4f, 0.4f, 0.4f));
        File.WriteAllText(Path.Combine(output, "shader.txt"), material.shader.name);
        string properties = "";
        for (int p = 0; p < material.shader.GetPropertyCount(); p++)
        {
            string name = material.shader.GetPropertyName(p);
            properties += name + " " + material.shader.GetPropertyType(p) + "\n";
        }
        File.WriteAllText(Path.Combine(output, "properties.txt"), properties);
        Color32[] baseline = Render(source, material, "01-native.png");
        Call(biome, "Build", map);
        Texture2D colored = (Texture2D)Get(biome, "colored");
        Check(colored != null, "Biome texture missing");
        Texture2D borderMask = (Texture2D)Get(biome, "coloredMask");
        Check(borderMask != null, "Visible border mask missing");
        material.SetTexture("_MaskTex", borderMask);
        Color32[] recolored = Render(colored, material, "02-biomes.png");
        double borderLight = 0, forestLight = 0;
        for (int y = 32; y < 480; y++) for (int x = 0; x < 4; x++)
        {
            Color32 b = recolored[y * 512 + 389 + x], f = recolored[y * 512 + 420 + x];
            borderLight += b.r + b.g + b.b; forestLight += f.r + f.g + f.b;
        }
        Check(borderLight < forestLight * 0.8, "Biome border is not visibly darker than the forest");
        int meadowChanges = 0, waterChanges = 0, forestChanges = 0;
        for (int y = 32; y < 480; y++) for (int x = 16; x < 496; x++)
        {
            int i = y * 512 + x;
            bool changed = !baseline[i].Equals(recolored[i]);
            if (x < 120 && changed) waterChanges++;
            if (x > 180 && x < 310 && changed) meadowChanges++;
            if (x > 360 && changed) forestChanges++;
        }
        Check(waterChanges == 0, "Water render changed: " + waterChanges);
        Check(meadowChanges == 0, "Native meadow render changed: " + meadowChanges);
        Check(forestChanges > 1000, "Forest render did not change: " + forestChanges);
        Check((bool)Call(living, "Setup", map), "LivingMap setup failed");
        Check((Texture2D)Get(living, "_vanillaMask") == mask, "LivingMap classification lost its native mask");
        material.SetTexture("_MaskTex", (Texture)Get(living, "_rtMask"));
        Texture2D originalRef = (Texture2D)Get(living, "_vanillaTex");
        Call(living, "RebuildGpuLayer");
        Check((Texture2D)Get(living, "_vanillaTex") == originalRef, "Bridge failed to restore the original texture reference");
        Color32[] combined = Render((Texture)Get(living, "_rt"), material, "03-livingmap-biomes.png");
        int mismatch = 0;
        for (int y = 32; y < 480; y++) for (int x = 16; x < 496; x++)
        {
            // Ignore biome boundaries, where the higher-resolution layer can filter differently.
            if (x > 115 && x < 145 || x > 370 && x < 400) continue;
            int i = y * 512 + x;
            if (Math.Abs(combined[i].r - recolored[i].r) > 3 || Math.Abs(combined[i].g - recolored[i].g) > 3 || Math.Abs(combined[i].b - recolored[i].b) > 3) mismatch++;
        }
        Check(mismatch == 0, "LivingMap did not preserve the biome background: " + mismatch);
        AddBuilding(living);
        Call(living, "RebuildGpuLayer");
        Color32[] building = Render((Texture)Get(living, "_rt"), material, "04-building.png");
        int buildingPixels = 0;
        for (int i = 0; i < combined.Length; i++) if (!combined[i].Equals(building[i])) buildingPixels++;
        Check(buildingPixels > 0, "LivingMap building fixture did not reach the rendered map");
        Check(Get(living, "_rtMask") != null, "LivingMap forest mask unavailable");
        var cleared = (System.Collections.Generic.HashSet<int>)Get(living, "_clearedForest");
        var planted = (System.Collections.Generic.HashSet<int>)Get(living, "_plantedForest");
        for (int y = 55; y < 69; y++) for (int x = 83; x < 91; x++) cleared.Add(y * Size + x);
        for (int y = 55; y < 69; y++) for (int x = 56; x < 62; x++) planted.Add(y * Size + x);
        Call(living, "RebuildMask");
        material.SetTexture("_MaskTex", (Texture)Get(living, "_rtMask"));
        Color32[] forestEdited = Render((Texture)Get(living, "_rt"), material, "05-forest-mask-edits.png");
        int forestEditPixels = 0;
        for (int i = 0; i < forestEdited.Length; i++) if (!forestEdited[i].Equals(building[i])) forestEditPixels++;
        Check(forestEditPixels > 0, "Cleared/planted forest fixture did not change the render");
        File.WriteAllText(Path.Combine(output, "PASS.json"), "{\"fixture\":\"synthetic\",\"renderer\":\"native Unity shader\",\"waterChangedPixels\":" + waterChanges + ",\"meadowChangedPixels\":" + meadowChanges + ",\"forestChangedPixels\":" + forestChanges + ",\"bridgeMismatchPixels\":" + mismatch + ",\"buildingChangedPixels\":" + buildingPixels + ",\"forestMaskEditedPixels\":" + forestEditPixels + "}");
        RenderTexture.active = null;
        Logger.LogInfo("PASS: native shader renders, water/meadows unchanged, forest changed, bridge and building visible.");
    }

    private static void AddBuilding(object living)
    {
        Type record = living.GetType().GetNestedType("PieceRec", BindingFlags.NonPublic);
        object piece = Activator.CreateInstance(record);
        foreach (string name in new[] { "X0", "Z0", "X1", "Z1", "Y" })
            record.GetField(name).SetValue(piece, name == "X0" ? 60f : name == "X1" ? 84f : name == "Z0" ? -12f : name == "Z1" ? 12f : 40f);
        record.GetField("Mat").SetValue(piece, (byte)0);
        IList list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(record));
        list.Add(piece);
        ((IDictionary)Get(living, "_pieces")).Add(0, list);
    }

    private Color32[] Render(Texture source, Material material, string filename)
    {
        var target = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture previousMain = material.GetTexture("_MainTex");
        var readback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            material.SetTexture("_MainTex", source);
            GL.PushMatrix();
            try
            {
                GL.LoadOrtho(); Check(material.SetPass(0), "Native shader pass unavailable");
                GL.Begin(GL.QUADS);
                GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                GL.End();
            }
            finally { GL.PopMatrix(); }
            readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply();
            File.WriteAllBytes(Path.Combine(output, filename), readback.EncodeToPNG());
            return readback.GetPixels32();
        }
        finally
        {
            material.SetTexture("_MainTex", previousMain);
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(readback);
        }
    }
}
