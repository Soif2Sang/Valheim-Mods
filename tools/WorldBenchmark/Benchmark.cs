using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("local.valheim.worldbenchmark", "Private World Benchmark", "0.1.0")]
[BepInDependency("local.valheim.horizonclarity", BepInDependency.DependencyFlags.SoftDependency)]
public class WorldBenchmark : BaseUnityPlugin
{
    static string saves;
    string output, worldName;
    float deadline, phaseAt, diagnosticAt;
    bool started, finished, located, introSkipped;
    int phase, scan;
    Vector3 anchor, eye;
    readonly List<ZDO> benches = new List<ZDO>();
    readonly List<float> samples = new List<float>();
    Camera camera;
    Type farLands;
    bool FarLandsReady()
    {
        if (farLands == null) return true;
        if ((bool)AccessTools.Field(farLands, "Failed").GetValue(null) || (bool)AccessTools.Field(farLands, "ForestFailed").GetValue(null))
            throw new Exception("FarLands terrain/forest generation failed");
        return (bool)AccessTools.Field(farLands, "Complete").GetValue(null) && (bool)AccessTools.Field(farLands, "ForestComplete").GetValue(null);
    }
    static bool PathPrefix(ref string __result) { __result = saves; return false; }
    static bool NoCloud(ref bool __result) { __result = false; return false; }
    static void PoseAfterCamera() { if (Active != null) Active.PlaceCamera(); }
    static WorldBenchmark Active;
    static bool KeepRunning() { Time.timeScale = 1f; return false; }
    static bool NotPaused(ref bool __result) { __result = false; return false; }
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VALHEIM_BENCH_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Active = this; saves = Path.Combine(output, "saves");
        worldName = Environment.GetEnvironmentVariable("VALHEIM_BENCH_WORLD");
        Utils.SetSaveDataPath(saves);
        var h = new Harmony("local.valheim.worldbenchmark");
        h.Patch(AccessTools.Method(typeof(Utils), "GetSaveDataPath"), prefix: new HarmonyMethod(typeof(WorldBenchmark), "PathPrefix"));
        h.Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupportedAndEnabled"), prefix: new HarmonyMethod(typeof(WorldBenchmark), "NoCloud"));
        h.Patch(AccessTools.Method(typeof(GameCamera), "LateUpdate"), postfix: new HarmonyMethod(typeof(WorldBenchmark), "PoseAfterCamera"));
        h.Patch(AccessTools.Method(typeof(Game), "UpdatePause"), prefix: new HarmonyMethod(typeof(WorldBenchmark), "KeepRunning"));
        h.Patch(AccessTools.Method(typeof(Game), "IsPaused"), prefix: new HarmonyMethod(typeof(WorldBenchmark), "NotPaused"));
        Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        deadline = Time.realtimeSinceStartup + 420f;
    }
    void Update()
    {
        if (finished || string.IsNullOrEmpty(output)) return;
        try
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("World benchmark timed out");
            if (!started)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FejdStartup>();
                if (menu == null || Time.realtimeSinceStartup < 8) return;
                farLands = AccessTools.TypeByName("FarLands.FarLandsDiagnostics");
                SaveSystem.ClearWorldListCache(true);
                World world = SaveSystem.GetWorldList().Find(w => w.m_worldName == worldName || w.m_name == worldName);
                if (world == null || !world.IsChunkedSave()) throw new Exception("Chunked copied world was not found in isolated saves");
                var profile = new PlayerProfile("WorldBenchmark", FileHelpers.FileSource.Local);
                profile.SetName("WorldBenchmark"); profile.Save(); Game.SetProfile("WorldBenchmark", FileHelpers.FileSource.Local);
                ZNet.SetServer(true, false, false, "OfflineBenchmark", "", world); ZNet.ResetServerHost();
                AccessTools.Method(typeof(FejdStartup), "LoadMainScene").Invoke(menu, null);
                started = true; Logger.LogInfo("Loading private copied world offline; original snapshot is not a save target.");
                return;
            }
            if (Player.m_localPlayer == null || EnvMan.instance == null || GameCamera.instance == null) return;
            if (Time.realtimeSinceStartup > diagnosticAt)
            {
                diagnosticAt = Time.realtimeSinceStartup + 10f;
                File.WriteAllText(Path.Combine(output, "progress.txt"), "phase=" + phase + " timeScale=" + Time.timeScale + " intro=" + Player.m_localPlayer.InIntro() + " teleport=" + Player.m_localPlayer.IsTeleporting() + " position=" + Player.m_localPlayer.transform.position + " anchorLoaded=" + (located && ZoneSystem.instance.IsZoneLoaded(anchor)));
            }
            Player.m_localPlayer.SetGodMode(true);
            if (Player.m_localPlayer.InIntro()) { if (!introSkipped) { introSkipped = true; Game.instance.SkipIntro(); } return; }
            EnvMan.instance.SetForceEnvironment("Clear");
            AccessTools.Field(typeof(EnvMan), "m_debugTimeOfDay").SetValue(EnvMan.instance, true);
            AccessTools.Field(typeof(EnvMan), "m_debugTime").SetValue(EnvMan.instance, 0.5f);
            if (!located)
            {
                if (!ZDOMan.instance.GetAllZDOsWithPrefabIterative("piece_workbench", benches, ref scan)) return;
                ZDO chosen = benches.Where(z => z.GetLong("creator".GetStableHashCode(), 0L) != 0L).OrderBy(z => z.GetPosition().sqrMagnitude).FirstOrDefault();
                if (chosen == null) throw new Exception("No player-built workbench found; choose another anchor in the probe");
                anchor = chosen.GetPosition();
                Player.m_localPlayer.TeleportTo(anchor + Vector3.up * 2f, Quaternion.identity, true);
                camera = (Camera)AccessTools.Field(typeof(GameCamera), "m_camera").GetValue(GameCamera.instance);
                eye = anchor + new Vector3(0, 10, -22);
                located = true; phaseAt = Time.realtimeSinceStartup;
                File.WriteAllText(Path.Combine(output, "anchor.txt"), anchor.ToString("F3"));
                return;
            }
            if (Player.m_localPlayer.IsTeleporting() || !ZoneSystem.instance.IsZoneLoaded(anchor))
            {
                // Intro respawn may replace the first player after our initial teleport.
                if (!Player.m_localPlayer.IsTeleporting() && Vector3.Distance(Player.m_localPlayer.transform.position, anchor) > 30f)
                    Player.m_localPlayer.TeleportTo(anchor + Vector3.up * 2f, Quaternion.identity, true);
                phase = 0; samples.Clear(); phaseAt = Time.realtimeSinceStartup; return;
            }
            float elapsed = Time.realtimeSinceStartup - phaseAt;
            if (!FarLandsReady()) { phaseAt = Time.realtimeSinceStartup; return; }
            if (phase == 0 && elapsed > 60f) { phase = 1; phaseAt = Time.realtimeSinceStartup; }
            else if (phase == 1)
            {
                samples.Add(Time.unscaledDeltaTime * 1000f);
                if (elapsed > 15f) { phase = 2; StartCoroutine(Finish()); }
            }
        }
        catch (Exception e) { finished = true; File.WriteAllText(Path.Combine(output, "FAILED.txt"), e.ToString()); Logger.LogError(e); Application.Quit(1); }
    }
    void PlaceCamera()
    {
        if (!located || camera == null) return;
        camera.transform.position = eye;
        camera.transform.rotation = Quaternion.LookRotation((anchor + Vector3.up * 3f) - eye);
        camera.fieldOfView = 65f;
        Camera sky = (Camera)AccessTools.Field(typeof(GameCamera), "m_skyCamera").GetValue(GameCamera.instance);
        if (sky != null) { sky.transform.position = eye; sky.transform.rotation = camera.transform.rotation; sky.fieldOfView = 65f; }
    }
    IEnumerator Finish()
    {
        yield return new WaitForEndOfFrame();
        if (farLands != null)
        {
            if (!FarLandsReady()) throw new Exception("FarLands became incomplete before capture");
            File.WriteAllText(Path.Combine(output, "farlands-ready.json"), "{\"terrainComplete\":true,\"forestComplete\":true}");
        }
        PlaceCamera();
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); screenshot.Apply();
            File.WriteAllBytes(Path.Combine(output, "world.png"), screenshot.EncodeToPNG());
        }
        finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target); Destroy(screenshot); }
        samples.Sort(); float average = samples.Average(); float p95 = samples[(int)((samples.Count - 1) * 0.95f)];
        string numeric = "{\"worldCopyLoaded\":true,\"playerBuiltAnchor\":true,\"width\":" + Screen.width + ",\"height\":" + Screen.height + ",\"samples\":" + samples.Count + ",\"meanFrameMs\":" + average.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ",\"p95FrameMs\":" + p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ",\"fogDensity\":" + RenderSettings.fogDensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"cameraFarClip\":" + camera.farClipPlane.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"realServerConnection\":false}";
        File.WriteAllText(Path.Combine(output, "PASS.json"), numeric);
        File.WriteAllLines(Path.Combine(output, "frame-ms.txt"), samples.Select(x => x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        finished = true; Application.Quit(0);
    }
}
