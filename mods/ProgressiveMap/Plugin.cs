using System;
using System.Collections;
using System.Diagnostics;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProgressiveMap
{
    [BepInPlugin("local.valheim.progressivemap", "Progressive Map", "0.1.1")]
    [BepInDependency("local.valheim.biomeclarity", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("j1ga.livingmap", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private sealed class Job
        {
            internal Minimap Map;
            internal WorldGenerator World;
            internal int Size, Index, Frames;
            internal float PixelSize;
            internal Color32[] Colors, Mask;
            internal Color[] Heights;
            internal float[] RawHeights;
            internal Func<Heightmap.Biome, Color> PixelColor;
            internal Func<float, float, float, Heightmap.Biome, Color> MaskColor;
            internal Stopwatch Total = Stopwatch.StartNew();
            internal double MaxSliceMs;
            internal RawImage Small, Large;
            internal bool SmallEnabled, LargeEnabled;
            internal TMP_Text SmallLabel, LargeLabel;
            internal string SmallText, LargeText;
        }
        private static Plugin instance;
        private static bool native;
        public static bool Generating;
        public static bool DisplayBlocked;
        public static int LastFrames;
        public static double LastSeconds, LastMaxSliceMs, LastInitMs, LastPublishMs;
        private Job pending;
        private Coroutine work;
        private Harmony harmony;

        private void Awake()
        {
            instance = this;
            harmony = new Harmony("local.valheim.progressivemap");
            harmony.PatchAll(typeof(Plugin));
            PatchConsumer("BiomeClarity.Plugin", "Build");
            PatchConsumer("LivingMap.LivingMapPlugin", "Tick");
            Logger.LogInfo("Progressive Map ready: native collision/spawn checks unchanged, map generation sliced at ~4 ms/frame.");
        }

        private void PatchConsumer(string typeName, string method)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return;
            var target = AccessTools.Method(type, method);
            if (target == null) throw new MissingMethodException(typeName, method);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), "ConsumerReady"));
        }
        private static bool ConsumerReady() { return !Generating; }
        private static T Field<T>(Minimap map, string name) { return (T)AccessTools.Field(typeof(Minimap), name).GetValue(map); }

        [HarmonyPatch(typeof(Minimap), "GenerateWorldMap"), HarmonyPrefix]
        private static bool Queue(Minimap __instance)
        {
            if (native || instance == null) return true;
            if (instance.pending != null && instance.pending.Map == __instance) return false;
            instance.Cancel();
            Generating = true;
            instance.pending = new Job { Map = __instance };
            instance.HidePending(instance.pending);
            instance.work = instance.StartCoroutine(instance.Run(instance.pending));
            return false;
        }

        private void HidePending(Job job)
        {
            job.Small = Field<RawImage>(job.Map, "m_mapImageSmall");
            job.Large = Field<RawImage>(job.Map, "m_mapImageLarge");
            job.SmallLabel = Field<TMP_Text>(job.Map, "m_biomeNameSmall");
            job.LargeLabel = Field<TMP_Text>(job.Map, "m_biomeNameLarge");
            job.SmallEnabled = job.Small != null && job.Small.enabled;
            job.LargeEnabled = job.Large != null && job.Large.enabled;
            job.SmallText = job.SmallLabel != null ? job.SmallLabel.text : "";
            job.LargeText = job.LargeLabel != null ? job.LargeLabel.text : "";
            DisplayBlocked = true;
            Cover(job);
        }
        private static void Cover(Job job)
        {
            if (job.Small != null) job.Small.enabled = false;
            if (job.Large != null) job.Large.enabled = false;
            string text = "Carte en preparation";
            if (job.Colors != null) text += " (" + (job.Index * 100L / job.Colors.Length) + " %)";
            if (job.SmallLabel != null) job.SmallLabel.text = text;
            if (job.LargeLabel != null) job.LargeLabel.text = text;
        }
        private static void Reveal(Job job)
        {
            if (job == null) return;
            if (job.Small != null) job.Small.enabled = job.SmallEnabled;
            if (job.Large != null) job.Large.enabled = job.LargeEnabled;
            if (job.SmallLabel != null) job.SmallLabel.text = job.SmallText;
            if (job.LargeLabel != null) job.LargeLabel.text = job.LargeText;
            DisplayBlocked = false;
        }
        [HarmonyPatch(typeof(Minimap), "Update"), HarmonyPostfix]
        private static void KeepCovered(Minimap __instance)
        {
            if (DisplayBlocked && instance != null && instance.pending != null && instance.pending.Map == __instance)
                Cover(instance.pending);
        }

        private IEnumerator Run(Job job)
        {
            // Return control to the native startup/respawn loop before calculating map pixels.
            yield return null;
            bool failed = false;
            var initClock = Stopwatch.StartNew();
            try
            {
                job.Size = Field<int>(job.Map, "m_textureSize");
                job.PixelSize = Field<float>(job.Map, "m_pixelSize");
                job.World = WorldGenerator.instance;
                int count = checked(job.Size * job.Size);
                job.Colors = new Color32[count];
                job.Mask = new Color32[count];
                job.Heights = new Color[count];
                job.RawHeights = new float[count];
                job.PixelColor = (Func<Heightmap.Biome, Color>)Delegate.CreateDelegate(typeof(Func<Heightmap.Biome, Color>), job.Map, AccessTools.Method(typeof(Minimap), "GetPixelColor"));
                job.MaskColor = (Func<float, float, float, Heightmap.Biome, Color>)Delegate.CreateDelegate(typeof(Func<float, float, float, Heightmap.Biome, Color>), job.Map, AccessTools.Method(typeof(Minimap), "GetMaskColor"));
            }
            catch (Exception e) { Logger.LogError(e); failed = true; }
            LastInitMs = initClock.Elapsed.TotalMilliseconds;
            if (failed) { Fallback(job); yield break; }
            while (job.Index < job.Colors.Length)
            {
                if (job.Map == null || pending != job) { Generating = false; yield break; }
                var clock = Stopwatch.StartNew();
                try
                {
                    int half = job.Size / 2;
                    float offset = job.PixelSize / 2f;
                    do
                    {
                        int i = job.Index;
                        float x = (i % job.Size - half) * job.PixelSize + offset;
                        float z = (i / job.Size - half) * job.PixelSize + offset;
                        Heightmap.Biome biome = job.World.GetBiome(x, z, 0.02f, false);
                        Color unused;
                        float height = job.World.GetBiomeHeight(biome, x, z, out unused, false, true);
                        job.Colors[i] = job.PixelColor(biome);
                        job.Mask[i] = job.MaskColor(x, z, height, biome);
                        job.Heights[i].r = height;
                        job.RawHeights[i] = height;
                        job.Index++;
                    } while (job.Index < job.Colors.Length && (job.Index % 128 != 0 || clock.Elapsed.TotalMilliseconds < 4));
                }
                catch (Exception e) { Logger.LogError(e); failed = true; }
                job.Frames++;
                job.MaxSliceMs = Math.Max(job.MaxSliceMs, clock.Elapsed.TotalMilliseconds);
                if (failed) { Fallback(job); yield break; }
                yield return null;
            }
            try
            {
                var publishClock = Stopwatch.StartNew();
                Texture2D mask = Field<Texture2D>(job.Map, "m_forestMaskTexture");
                Texture2D color = Field<Texture2D>(job.Map, "m_mapTexture");
                Texture2D height = Field<Texture2D>(job.Map, "m_heightTexture");
                mask.SetPixels32(job.Mask); mask.Apply();
                color.SetPixels32(job.Colors); color.Apply();
                height.SetPixels(job.Heights); height.Apply();
                if (Convert.ToInt32(FileHelpers.LocalStorageSupport) == 2)
                    AccessTools.Method(typeof(Minimap), "SaveMapTextureDataToDisk").Invoke(job.Map, new object[] { job.Mask, job.Colors, job.RawHeights });
                Generating = false;
                LastFrames = job.Frames; LastSeconds = job.Total.Elapsed.TotalSeconds; LastMaxSliceMs = job.MaxSliceMs;
                PluginInfo info;
                if (Chainloader.PluginInfos.TryGetValue("local.valheim.biomeclarity", out info))
                    AccessTools.Method(info.Instance.GetType(), "Build").Invoke(info.Instance, new object[] { job.Map });
                LastPublishMs = publishClock.Elapsed.TotalMilliseconds;
                Logger.LogInfo(string.Format("Map complete: {0} frames, {1:F2} s elapsed, max pixel slice {2:F2} ms; native textures/cache published.", LastFrames, LastSeconds, LastMaxSliceMs));
            }
            catch (Exception e) { Logger.LogError(e); failed = true; Fallback(job); }
            if (failed) yield break;
            // Let LivingMap attach to the completed/recolored textures before exposing the images.
            yield return null;
            if (pending == job) { Reveal(job); pending = null; work = null; }
        }

        public static void GenerateNative(Minimap map)
        {
            bool old = native; native = true;
            try { AccessTools.Method(typeof(Minimap), "GenerateWorldMap").Invoke(map, null); }
            finally { native = old; }
        }
        private void Fallback(Job job)
        {
            Generating = false;
            Logger.LogWarning("Progressive generation failed; falling back to the native generator.");
            try { if (job.Map != null) GenerateNative(job.Map); }
            finally { Reveal(job); pending = null; work = null; }
        }
        private void Cancel()
        {
            if (work != null) StopCoroutine(work);
            Reveal(pending);
            work = null; pending = null; Generating = false;
        }
        [HarmonyPatch(typeof(Minimap), "OnDestroy"), HarmonyPrefix]
        private static void MapDestroyed(Minimap __instance) { if (instance != null && instance.pending != null && instance.pending.Map == __instance) instance.Cancel(); }
        private void OnDestroy() { Cancel(); if (harmony != null) harmony.UnpatchSelf(); instance = null; }
    }
}
