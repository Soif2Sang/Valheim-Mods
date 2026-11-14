using System;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

internal static class QuickConnectChecks
{
    internal static void Run(FejdStartup menu, string output)
    {
        Type plugin = AccessTools.TypeByName("MagiCorp.RememberServerPassword.Plugin");
        if (plugin == null) throw new Exception("RememberServerPassword test DLL missing");
        foreach (string method in new[] { "SetupGui", "ProceedJoinRequest", "SetSelectedProfile", "OnCharacterStart" })
            if (AccessTools.Method(typeof(FejdStartup), method) == null) throw new Exception("Missing menu method: " + method);
        foreach (string method in new[] { "RPC_ClientHandshake", "OnPasswordEntered", "RPC_PeerInfo", "RPC_Error" })
        {
            var target = AccessTools.Method(typeof(ZNet), method);
            if (target == null) throw new Exception("Missing authentication method: " + method);
            var info = Harmony.GetPatchInfo(target);
            bool found = false;
            if (info != null) foreach (string owner in info.Owners) if (owner == "magicorp.valheim.rememberserverpassword") found = true;
            if (!found) throw new Exception("Password patch not installed: " + method);
        }
        // A synthetic value only. Never read the actual server's password for this smoke test.
        string synthetic = Guid.NewGuid().ToString("N");
        string encrypted = (string)AccessTools.Method(plugin, "Encrypt").Invoke(null, new object[] { synthetic });
        string roundTrip = (string)AccessTools.Method(plugin, "Decrypt").Invoke(null, new object[] { encrypted });
        if (roundTrip != synthetic || encrypted.Contains(synthetic)) throw new Exception("Password encryption round trip failed");
        var passwordProperty = AccessTools.Property(typeof(FejdStartup), "ServerPassword");
        object oldPassword = passwordProperty.GetValue(null, null);
        try
        {
            AccessTools.Method(plugin, "SetServerPassword").Invoke(null, new object[] { synthetic, "isolated validation" });
            if ((string)passwordProperty.GetValue(null, null) != synthetic) throw new Exception("Native password assignment failed");
        }
        finally { AccessTools.Method(plugin, "SetServerPassword").Invoke(null, new object[] { oldPassword, "isolated validation restore" }); }
        var last = (ConfigEntry<string>)AccessTools.Field(plugin, "LastServer").GetValue(null);
        last.Value = "host/127.0.0.1:9";
        object[] joinArgs = { "playfab/abcdef1234567890", default(ServerJoinData) };
        if (!(bool)AccessTools.Method(plugin, "TryBuildJoinData").Invoke(null, joinArgs)) throw new Exception("PlayFab join-data construction failed");
        var join = (ServerJoinData)joinArgs[1];
        string remote = (string)AccessTools.Field(typeof(ServerJoinDataPlayFabUser), "m_remotePlayerId").GetValue(join.PlayFabUser);
        if (remote != "ABCDEF1234567890" || last.Value != "host/127.0.0.1:9") throw new Exception("PlayFab identity casing fix failed or modified saved destination");
        AccessTools.Method(plugin, "EnsureQuickConnectButton").Invoke(null, new object[] { menu });
        string objectName = (string)AccessTools.Field(plugin, "QuickConnectObjectName").GetRawConstantValue();
        bool button = false;
        foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>()) if (obj.name == objectName) button = true;
        if (!button) throw new Exception("Quick Connect menu button not created");
        Type horizon = AccessTools.TypeByName("HorizonClarity.Plugin");
        if (horizon != null)
        {
            object instance = AccessTools.Field(horizon, "current").GetValue(null);
            if (instance == null) throw new Exception("HorizonClarity did not initialize");
            float beforeFog = RenderSettings.fogDensity;
            Color beforeColor = RenderSettings.fogColor;
            var cameraObject = new GameObject("HorizonSyntheticCamera");
            Camera camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            try
            {
                AccessTools.Method(horizon, "ApplyFog").Invoke(instance, new object[] { 0.004f });
                if (!Mathf.Approximately(RenderSettings.fogDensity, 0.001f)) throw new Exception("Horizon fog not scaled");
                AccessTools.Method(horizon, "ApplyFog").Invoke(instance, new object[] { 0.004f });
                if (!Mathf.Approximately(RenderSettings.fogDensity, 0.001f)) throw new Exception("Fog compounded across native updates");
                camera.farClipPlane = 2000;
                AccessTools.Method(horizon, "ApplyCamera").Invoke(instance, new object[] { camera });
                if (camera.farClipPlane != 4000) throw new Exception("Main camera minimum failed");
                AccessTools.Method(horizon, "Restore").Invoke(instance, null);
                if (!Mathf.Approximately(RenderSettings.fogDensity, 0.004f) || camera.farClipPlane != 2000 || RenderSettings.fogColor != beforeColor) throw new Exception("Horizon restore changed native settings");
                AccessTools.Method(horizon, "ApplyFog").Invoke(instance, new object[] { 0.004f });
                RenderSettings.fogDensity = 0.007f;
                AccessTools.Method(horizon, "Restore").Invoke(instance, null);
                if (!Mathf.Approximately(RenderSettings.fogDensity, 0.007f)) throw new Exception("Horizon overwrote a later native change");
                File.WriteAllText(Path.Combine(output, "Horizon-PASS.json"), "{\"pluginLoaded\":true,\"fogScaledWithoutCompounding\":true,\"camera4000\":true,\"nativeRestore\":true,\"fogColorUnchanged\":true,\"laterChangesPreserved\":true,\"realWorldRenderingTested\":false}");
            }
            finally
            {
                AccessTools.Method(horizon, "Restore").Invoke(instance, null);
                RenderSettings.fogDensity = beforeFog;
                UnityEngine.Object.Destroy(cameraObject);
            }
        }
        Type party = AccessTools.TypeByName("Warband.Party");
        if (party != null)
        {
            Type stateType = AccessTools.TypeByName("Warband.PersistentRoster");
            var state = Activator.CreateInstance(stateType);
            stateType.GetField("Group").SetValue(state, Guid.NewGuid().ToString("N"));
            var keys = (System.Collections.Generic.List<string>)stateType.GetField("Keys").GetValue(state);
            keys.Add("synthetic-a"); keys.Add("synthetic-b");
            string path = Path.Combine(output, "synthetic-party.xml");
            stateType.GetMethod("Save").Invoke(state, new object[] { path });
            AccessTools.Field(party, "_saved").SetValue(null, state);
            AccessTools.Field(party, "_savePath").SetValue(null, path);
            var packet = (ZPackage)AccessTools.Method(party, "SavedPacket").Invoke(null, null);
            packet = new ZPackage(packet.GetArray());
            if (packet.ReadString() != (string)stateType.GetField("Group").GetValue(state) || packet.ReadInt() != 2 || packet.ReadString() != "synthetic-a" || packet.ReadString() != "synthetic-b") throw new Exception("Warband packet round trip failed");
            AccessTools.Method(party, "SuspendSaved").Invoke(null, null);
            if (!File.Exists(path) || AccessTools.Field(party, "_saved").GetValue(null) != null) throw new Exception("Warband logout erased saved membership");
            AccessTools.Field(party, "_savePath").SetValue(null, path);
            AccessTools.Method(party, "ForgetSaved").Invoke(null, null);
            if (File.Exists(path)) throw new Exception("Warband explicit forget did not remove saved membership");
            AccessTools.Method(party, "SuspendSaved").Invoke(null, null);
            File.WriteAllText(Path.Combine(output, "Warband-PASS.json"), "{\"pluginLoaded\":true,\"nativePacketRoundTrip\":true,\"logoutPreservesDisk\":true,\"explicitForgetClearsDisk\":true,\"realMultiplayerTested\":false}");
        }
        File.WriteAllText(Path.Combine(output, "PASS.json"), "{\"mode\":\"quick-connect-menu-smoke\",\"authenticationPatchesInstalled\":true,\"encryptionRoundTrip\":true,\"nativePasswordSetterValidated\":true,\"playFabJoinIdentityUppercase\":true,\"savedDestinationUnchanged\":true,\"protection\":\"" + (encrypted.StartsWith("D:") ? "Windows DPAPI" : "AES fallback") + "\",\"quickConnectButtonCreated\":true,\"realServerConnectionTested\":false}");
    }
}
