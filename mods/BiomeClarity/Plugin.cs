using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace BiomeClarity
{
    [BepInPlugin("local.valheim.biomeclarity", "Biome Clarity", "0.3.4")]
    [BepInDependency("j1ga.livingmap", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin instance;
        private Harmony harmony;
        private ConfigEntry<bool> colorsEnabled, borders;
        private Texture2D original, colored;
        private Texture2D originalMask, coloredMask;
        private Material small, large;
        private static FieldInfo livingMapBase;
        private static FieldInfo livingMapMask;
        private bool maskBridge;

        private void Awake()
        {
            instance = this;
            colorsEnabled = Config.Bind("Display", "Enabled", true, "Native Meadows and greener Black Forest. Reload the world after changing.");
            borders = Config.Bind("Display", "Borders", true, "Dark two-pixel outline inside Black Forest at Meadows transitions. Reload the world after changing.");
            harmony = new Harmony("local.valheim.biomeclarity");
            harmony.PatchAll(typeof(Plugin));
            Type livingMap = AccessTools.TypeByName("LivingMap.LivingMapPlugin");
            if (livingMap != null)
            {
                MethodInfo rebuild = AccessTools.Method(livingMap, "RebuildGpuLayer");
                livingMapBase = AccessTools.Field(livingMap, "_vanillaTex");
                if (rebuild != null && livingMapBase != null && livingMapBase.FieldType == typeof(Texture2D))
                {
                    harmony.Patch(rebuild,
                        prefix: new HarmonyMethod(typeof(Plugin), "BeforeLivingMapRebuild"),
                        finalizer: new HarmonyMethod(typeof(Plugin), "AfterLivingMapRebuild"));
                    Logger.LogInfo("LivingMap bridge enabled: biome colors below buildings and paths.");
                    MethodInfo rebuildMask = AccessTools.Method(livingMap, "RebuildMask");
                    livingMapMask = AccessTools.Field(livingMap, "_vanillaMask");
                    if (rebuildMask != null && livingMapMask != null && livingMapMask.FieldType == typeof(Texture2D))
                    {
                        harmony.Patch(rebuildMask,
                            prefix: new HarmonyMethod(typeof(Plugin), "BeforeLivingMapMask"),
                            finalizer: new HarmonyMethod(typeof(Plugin), "AfterLivingMapMask"));
                        maskBridge = true;
                    }
                }
                else Logger.LogWarning("LivingMap bridge unavailable: unsupported LivingMap version.");
            }
            Logger.LogInfo("Biome Clarity 0.3.4 ready: biome colors only, no aerial camera or keyboard shortcuts.");
        }

        private static void BeforeLivingMapRebuild(object __instance, out Texture2D __state)
        {
            __state = null;
            if (instance == null || instance.colored == null || livingMapBase == null) return;
            Texture2D source = livingMapBase.GetValue(__instance) as Texture2D;
            if (source != instance.original) return;
            __state = source;
            livingMapBase.SetValue(__instance, instance.colored);
        }

        private static void AfterLivingMapRebuild(object __instance, Texture2D __state)
        {
            // Restore even on failure: LivingMap compares this field with the native map each tick.
            if (__state != null) livingMapBase.SetValue(__instance, __state);
        }

        private static void BeforeLivingMapMask(object __instance, out Texture2D __state)
        {
            __state = null;
            if (instance == null || instance.coloredMask == null || livingMapMask == null) return;
            Texture2D source = livingMapMask.GetValue(__instance) as Texture2D;
            if (source != instance.originalMask) return;
            __state = source;
            livingMapMask.SetValue(__instance, instance.coloredMask);
        }

        private static void AfterLivingMapMask(object __instance, Texture2D __state)
        {
            if (__state != null) livingMapMask.SetValue(__instance, __state);
        }

        private static T Field<T>(Minimap map, string name)
        {
            return (T)AccessTools.Field(typeof(Minimap), name).GetValue(map);
        }

        private void Build(Minimap map)
        {
            Release();
            if (!colorsEnabled.Value) return;
            try
            {
                original = Field<Texture2D>(map, "m_mapTexture");
                small = Field<Material>(map, "m_mapSmallShader");
                large = Field<Material>(map, "m_mapLargeShader");
                Color32[] pixels = original.GetPixels32();
                uint[] packed = new uint[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) packed[i] = Pack(pixels[i]);
                uint meadow = Pack((Color32)Field<Color>(map, "m_meadowsColor"));
                uint forest = Pack((Color32)Field<Color>(map, "m_blackforestColor"));
                packed = Palette.Render(packed, original.width, meadow, forest, borders.Value);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Unpack(packed[i]);
                colored = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false);
                colored.name = "BiomeClarity colors";
                colored.wrapMode = original.wrapMode;
                colored.filterMode = original.filterMode;
                colored.SetPixels32(pixels);
                colored.Apply(false, true);
                if (borders.Value)
                {
                    originalMask = Field<Texture2D>(map, "m_forestMaskTexture");
                    Color32[] mask = originalMask.GetPixels32();
                    if (mask.Length != packed.Length) throw new InvalidOperationException("Map/mask dimensions differ");
                    for (int i = 0; i < mask.Length; i++)
                        if (packed[i] == Palette.Border) mask[i].r = 0;
                    coloredMask = new Texture2D(originalMask.width, originalMask.height, TextureFormat.RGBA32, false);
                    coloredMask.wrapMode = originalMask.wrapMode;
                    coloredMask.filterMode = originalMask.filterMode;
                    coloredMask.SetPixels32(mask);
                    coloredMask.Apply(false, false);
                    // Keep LivingMap's tree classification based on the native mask.
                    if (!maskBridge)
                    {
                        small.SetTexture("_MaskTex", coloredMask);
                        large.SetTexture("_MaskTex", coloredMask);
                    }
                }
                small.SetTexture("_MainTex", colored);
                large.SetTexture("_MainTex", colored);
                Logger.LogInfo("Biome colors applied to both maps; exploration and world unchanged.");
            }
            catch (Exception e)
            {
                Release();
                Logger.LogError("Could not recolor map; vanilla retained: " + e);
            }
        }

        private void Release()
        {
            if (original != null)
            {
                if (small != null && small.GetTexture("_MainTex") == colored) small.SetTexture("_MainTex", original);
                if (large != null && large.GetTexture("_MainTex") == colored) large.SetTexture("_MainTex", original);
            }
            if (colored != null) Destroy(colored);
            if (originalMask != null)
            {
                if (small != null && small.GetTexture("_MaskTex") == coloredMask) small.SetTexture("_MaskTex", originalMask);
                if (large != null && large.GetTexture("_MaskTex") == coloredMask) large.SetTexture("_MaskTex", originalMask);
            }
            if (coloredMask != null) Destroy(coloredMask);
            originalMask = null; coloredMask = null;
            colored = null;
            original = null;
            small = null;
            large = null;
        }

        private void OnDestroy()
        {
            Release();
            if (harmony != null) harmony.UnpatchSelf();
            instance = null;
        }

        private static uint Pack(Color32 c)
        {
            return ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        }

        private static Color32 Unpack(uint c)
        {
            return new Color32((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
        }

        [HarmonyPatch(typeof(Minimap), "TryLoadMinimapTextureData")]
        [HarmonyPostfix]
        private static void AfterLoad(Minimap __instance, bool __result)
        {
            if (__result && instance != null) instance.Build(__instance);
        }

        [HarmonyPatch(typeof(Minimap), "GenerateWorldMap")]
        [HarmonyPostfix]
        private static void AfterGenerate(Minimap __instance)
        {
            if (instance != null) instance.Build(__instance);
        }

        [HarmonyPatch(typeof(Minimap), "OnDestroy")]
        [HarmonyPrefix]
        private static void BeforeMapDestroy()
        {
            if (instance != null) instance.Release();
        }
    }
}



