using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;

internal static class FarLandsProfile
{
    private sealed class Cost { internal long Calls, Ticks, Worst; }
    private static readonly Dictionary<string, Cost> costs = new Dictionary<string, Cost>();
    private static readonly Stopwatch elapsed = Stopwatch.StartNew();
    private static Type diagnostics;
    private static void Before(out long __state) { __state = Stopwatch.GetTimestamp(); }
    private static void After(MethodBase __originalMethod, long __state)
    {
        long ticks = Stopwatch.GetTimestamp() - __state;
        string key = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
        lock (costs)
        {
            Cost c;
            if (!costs.TryGetValue(key, out c)) costs[key] = c = new Cost();
            c.Calls++; c.Ticks += ticks; c.Worst = Math.Max(c.Worst, ticks);
        }
    }
    internal static void Init()
    {
        diagnostics = AccessTools.TypeByName("FarLands.FarLandsDiagnostics");
        if (diagnostics == null) throw new Exception("FarLands profile requested but DLL is not loaded");
        var harmony = new Harmony("local.valheim.mapvalidation.farlandsprofile");
        foreach (string entry in new[] { "FarLandsPlugin:TickTerrain", "FarLandsPlugin:TickForest", "TreeTypes:FromGame", "TreeTypes:Understorey", "ImpostorBaker:Step", "TileGenerator:Generate", "ForestGenerator:Generate" })
        {
            string[] parts = entry.Split(':');
            MethodInfo method = AccessTools.Method(AccessTools.TypeByName("FarLands." + parts[0]), parts[1]);
            if (method == null) throw new Exception("Missing profile target: " + entry);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(FarLandsProfile), "Before"), finalizer: new HarmonyMethod(typeof(FarLandsProfile), "After"));
        }
        elapsed.Restart();
    }
    private static object Value(string name) { return diagnostics.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }
    private static string Number(double n) { return n.ToString("0.###", CultureInfo.InvariantCulture); }
    internal static bool Tick(string output)
    {
        if ((bool)Value("Failed") || (bool)Value("ForestFailed")) throw new Exception("FarLands reported a failure");
        if (!(bool)Value("Complete") || !(bool)Value("ForestComplete")) return false;
        var report = new StringBuilder("{\"mode\":\"farlands-profile\",\"secondsFromProbeStart\":" + Number(elapsed.Elapsed.TotalSeconds) + ",\"costs\":{");
        bool first = true;
        lock (costs) foreach (var entry in costs)
        {
            if (!first) report.Append(','); first = false;
            report.Append('"').Append(entry.Key).Append("\":{\"calls\":").Append(entry.Value.Calls)
                .Append(",\"totalMs\":").Append(Number(entry.Value.Ticks * 1000.0 / Stopwatch.Frequency))
                .Append(",\"maxMs\":").Append(Number(entry.Value.Worst * 1000.0 / Stopwatch.Frequency)).Append('}');
        }
        report.Append("},\"diagnostics\":{"); first = true;
        foreach (string name in new[] { "TickMsTotal", "GenerateMsTotal", "FillMsTotal", "FirstFillMs", "MaxFillMs", "ForestTickMsTotal", "ForestMakeMsMax", "ForestTreeMeshRebuildMsTotal", "ForestWorldMsMax", "Generated", "Loaded", "ForestGenerated", "ForestLoaded", "ForestTrees", "ForestBaked", "ForestComplete", "Complete" })
        {
            if (!first) report.Append(','); first = false;
            object value = Value(name);
            report.Append('"').Append(name).Append("\":").Append(value is bool ? ((bool)value ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        report.Append("}}");
        File.WriteAllText(Path.Combine(output, "PASS.json"), report.ToString());
        return true;
    }
}
