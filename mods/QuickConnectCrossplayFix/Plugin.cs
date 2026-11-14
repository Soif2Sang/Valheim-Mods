using System;
using BepInEx;
using HarmonyLib;

namespace QuickConnectCrossplayFix
{
    [BepInPlugin("local.valheim.quickconnectcrossplayfix", "Quick Connect Crossplay Fix", "0.1.0")]
    [BepInDependency("magicorp.valheim.rememberserverpassword")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        private void Awake()
        {
            Type type = AccessTools.TypeByName("MagiCorp.RememberServerPassword.Plugin");
            var target = AccessTools.Method(type, "TryBuildJoinData");
            if (target == null) throw new MissingMethodException("RememberServerPassword.TryBuildJoinData");
            harmony = new Harmony("local.valheim.quickconnectcrossplayfix");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), "BeforeBuild"));
            Logger.LogInfo("PlayFab Quick Connect destination casing fix enabled; password store unchanged.");
        }
        private static void BeforeBuild(ref string __0) { __0 = Identity.Canonicalize(__0); }
        private void OnDestroy() { if (harmony != null) harmony.UnpatchSelf(); }
    }
}
