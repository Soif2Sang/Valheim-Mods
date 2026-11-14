using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;

internal static class ProgressiveMapChecks
{
    private static bool playerDuringGeneration;
    private static int hiddenFrames;
    private static bool Visible(Minimap map, string name)
    {
        var image = AccessTools.Field(typeof(Minimap), name).GetValue(map);
        return (bool)image.GetType().GetProperty("enabled").GetValue(image, null);
    }
    private static uint Hash(Texture2D texture)
    {
        byte[] bytes = texture.GetRawTextureData();
        uint hash = 2166136261;
        for (int i = 0; i < bytes.Length; i++) hash = unchecked((hash ^ bytes[i]) * 16777619);
        return hash;
    }
    private static uint[] Textures(Minimap map)
    {
        uint[] values = new uint[3];
        string[] names = { "m_mapTexture", "m_forestMaskTexture", "m_heightTexture" };
        for (int i = 0; i < names.Length; i++) values[i] = Hash((Texture2D)AccessTools.Field(typeof(Minimap), names[i]).GetValue(map));
        return values;
    }
    internal static bool Tick(Minimap map, string output)
    {
        Type type = AccessTools.TypeByName("ProgressiveMap.Plugin");
        if (type == null) throw new Exception("ProgressiveMap test DLL missing");
        if ((bool)AccessTools.Field(type, "DisplayBlocked").GetValue(null))
        {
            if (Visible(map, "m_mapImageSmall") || Visible(map, "m_mapImageLarge")) throw new Exception("Incomplete map exposed during preparation");
            hiddenFrames++;
            if (Player.m_localPlayer != null) playerDuringGeneration = true;
            return false;
        }
        int frames = (int)AccessTools.Field(type, "LastFrames").GetValue(null);
        if (frames == 0) return false;
        if (hiddenFrames == 0 || !Visible(map, "m_mapImageSmall") || !Visible(map, "m_mapImageLarge")) throw new Exception("Map visibility was not correctly restored");
        if (frames < 10) throw new Exception("Map did not yield enough frames: " + frames);
        uint[] before = Textures(map);
        var clock = Stopwatch.StartNew();
        AccessTools.Method(type, "GenerateNative").Invoke(null, new object[] { map });
        double nativeMs = clock.Elapsed.TotalMilliseconds;
        uint[] after = Textures(map);
        for (int i = 0; i < before.Length; i++) if (before[i] != after[i]) throw new Exception("Progressive/native texture mismatch at " + i);
        double slice = (double)AccessTools.Field(type, "LastMaxSliceMs").GetValue(null);
        double init = (double)AccessTools.Field(type, "LastInitMs").GetValue(null);
        double publish = (double)AccessTools.Field(type, "LastPublishMs").GetValue(null);
        if (nativeMs >= 1000 && Math.Max(slice, Math.Max(init, publish)) >= nativeMs / 2) throw new Exception("A progressive stage still blocks too long");
        string report = string.Format(CultureInfo.InvariantCulture,
            "{{\"mode\":\"progressive-map-equivalence\",\"frames\":{0},\"maxPixelSliceMs\":{1:F3},\"initMs\":{2:F3},\"publishMs\":{3:F3},\"nativeBlockingCallMs\":{4:F3},\"nativeTexturesMatch\":true,\"playerPresentAtCheck\":{5},\"playerObservedDuringGeneration\":{6},\"mapHiddenFrames\":{7},\"mapSize\":{8}}}",
            frames, slice, init, publish, nativeMs, Player.m_localPlayer != null ? "true" : "false", playerDuringGeneration ? "true" : "false", hiddenFrames,
            AccessTools.Field(typeof(Minimap), "m_textureSize").GetValue(map));
        File.WriteAllText(Path.Combine(output, "PASS.json"), report);
        return true;
    }
}
