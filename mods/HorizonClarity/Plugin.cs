using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HorizonClarity
{
    [BepInPlugin("local.valheim.horizonclarity", "Horizon Clarity", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin current;
        private Harmony harmony;
        private ConfigEntry<bool> active;
        private ConfigEntry<float> fogMultiplier, viewDistance;
        private bool fogSaved;
        private float nativeFog, appliedFog, nativeClip, appliedClip;
        private Camera camera;
        private static readonly System.Reflection.FieldInfo CameraField = AccessTools.Field(typeof(GameCamera), "m_camera");
        private bool InWorld { get { return active.Value && Game.instance != null && Player.m_localPlayer != null; } }

        private void Awake()
        {
            current = this;
            active = Config.Bind("Display", "Enabled", true, "Thin distance fog and extend the main camera locally.");
            fogMultiplier = Config.Bind("Display", "FogMultiplier", 0.25f, new ConfigDescription("Fraction of native distance fog; weather colours/rain remain native. Does not clear Mistlands mist volumes.", new AcceptableValueRange<float>(0.05f, 1f)));
            viewDistance = Config.Bind("Display", "ViewDistance", 4000f, new ConfigDescription("Minimum main-camera far clip in metres. Does not generate missing terrain/objects.", new AcceptableValueRange<float>(1000f, 8000f)));
            harmony = new Harmony("local.valheim.horizonclarity");
            harmony.Patch(AccessTools.Method(typeof(EnvMan), "SetEnv"), postfix: new HarmonyMethod(typeof(Plugin), "AfterEnvironment"));
            harmony.Patch(AccessTools.Method(typeof(GameCamera), "LateUpdate"), postfix: new HarmonyMethod(typeof(Plugin), "AfterCamera"));
            Logger.LogInfo("Horizon Clarity ready: local fog multiplier and main-camera distance; no world generation.");
        }
        private static void AfterEnvironment()
        {
            if (current == null || !current.InWorld) return;
            current.ApplyFog(RenderSettings.fogDensity);
        }
        private void ApplyFog(float native)
        {
            nativeFog = native; appliedFog = Policy.Fog(native, fogMultiplier.Value); fogSaved = true;
            RenderSettings.fogDensity = appliedFog;
        }
        private static void AfterCamera(GameCamera __instance)
        {
            if (current == null || !current.InWorld) return;
            current.ApplyCamera((Camera)CameraField.GetValue(__instance));
        }
        private void ApplyCamera(Camera target)
        {
            if (target == null) return;
            if (camera != target) { RestoreCamera(); camera = target; nativeClip = camera.farClipPlane; }
            else if (!Mathf.Approximately(camera.farClipPlane, appliedClip)) nativeClip = camera.farClipPlane;
            appliedClip = Policy.Clip(nativeClip, viewDistance.Value);
            camera.farClipPlane = appliedClip;
        }
        private void RestoreCamera()
        {
            if (camera != null && Mathf.Approximately(camera.farClipPlane, appliedClip)) camera.farClipPlane = nativeClip;
            camera = null;
        }
        private void Restore()
        {
            if (fogSaved && Mathf.Approximately(RenderSettings.fogDensity, appliedFog)) RenderSettings.fogDensity = nativeFog;
            fogSaved = false;
            RestoreCamera();
        }
        private void LateUpdate() { if (!InWorld) Restore(); }
        private void OnDestroy() { Restore(); if (harmony != null) harmony.UnpatchSelf(); if (current == this) current = null; }
    }
}
