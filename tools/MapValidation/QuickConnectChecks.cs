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
        File.WriteAllText(Path.Combine(output, "PASS.json"), "{\"mode\":\"quick-connect-menu-smoke\",\"authenticationPatchesInstalled\":true,\"encryptionRoundTrip\":true,\"nativePasswordSetterValidated\":true,\"playFabJoinIdentityUppercase\":true,\"savedDestinationUnchanged\":true,\"protection\":\"" + (encrypted.StartsWith("D:") ? "Windows DPAPI" : "AES fallback") + "\",\"quickConnectButtonCreated\":true,\"realServerConnectionTested\":false}");
    }
}
