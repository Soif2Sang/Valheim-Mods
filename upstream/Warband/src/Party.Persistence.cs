using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace Warband
{
    internal static partial class Party
    {
        private const string RpcSaved = "Warband_Saved_v1", RpcResume = "Warband_Resume_v1";
        private static PersistentRoster _saved;
        private static string _savePath, _selfKey;
        private static float _nextRestore, _restoreAfter;
        internal static bool HasSavedParty { get { return _saved != null && _saved.Keys.Count > 1; } }

        private static string PeerKey(long peer)
        {
            if (ZNet.instance == null) return null;
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
                if (info.m_characterID.UserID == peer)
                    return PersistentRoster.Identity(info.m_userInfo.m_id.ToString(), info.m_name);
            return null;
        }
        private static bool SavedContext()
        {
            if (ZNet.instance == null || Player.m_localPlayer == null) return false;
            string self = PeerKey(Me);
            long world = ZNet.instance.GetWorldUID();
            if (self == null || world == 0) return false;
            string path = Path.Combine(Path.Combine(Paths.ConfigPath, "Warband/parties"), PersistentRoster.FileName(world, self));
            if (_savePath == path) return true;
            _savePath = path; _selfKey = self; _saved = null;
            _restoreAfter = Time.time + 5f; _nextRestore = 0;
            try { _saved = PersistentRoster.Load(path, self); }
            catch (Exception e) { Plugin.Log.LogWarning("Saved party unavailable: " + e.Message); }
            return true;
        }
        private static void SuspendSaved()
        { _saved = null; _savePath = null; _selfKey = null; _nextRestore = 0; }

        private static void SaveRoster()
        {
            if (_saved == null || _savePath == null) return;
            try { _saved.Save(_savePath); }
            catch (Exception e) { Plugin.Log.LogWarning("Could not save party: " + e.Message); }
        }
        private static void ForgetSaved()
        {
            _saved = null;
            if (_savePath == null) return;
            try { if (File.Exists(_savePath)) File.Delete(_savePath); }
            catch (Exception e) { Plugin.Log.LogWarning("Could not forget party: " + e.Message); }
        }
        private static void RegisterSaved(ZRoutedRpc rpc)
        {
            rpc.Register<ZPackage>(RpcSaved, new Action<long, ZPackage>(OnSaved));
            rpc.Register<ZPackage>(RpcResume, new Action<long, ZPackage>(OnResume));
        }
        // Keep offline identities; only explicit leave/kick removes permanent membership.
        private static void RememberLiveMembers()
        {
            if (!SavedContext() || Leader != Me || Members.Count < 2) return;
            if (_saved == null) _saved = new PersistentRoster { Group = Guid.NewGuid().ToString("N") };
            bool changed = false;
            foreach (Member member in Members)
            {
                string key = PeerKey(member.Peer);
                if (key != null && !_saved.Contains(key) && _saved.Keys.Count < 10) { _saved.Keys.Add(key); changed = true; }
            }
            if (changed && _saved.Valid(_selfKey)) SaveRoster();
        }
        private static ZPackage SavedPacket()
        {
            var pkg = new ZPackage(); pkg.Write(_saved.Group); pkg.Write(_saved.Keys.Count);
            foreach (string key in _saved.Keys) pkg.Write(key);
            return pkg;
        }
        private static void SendSaved()
        {
            RememberLiveMembers();
            if (_saved == null || !_saved.Valid(_selfKey) || Leader != Me) return;
            foreach (Member member in Members)
                if (member.Peer != Me) Send(member.Peer, RpcSaved, SavedPacket());
        }
        private static void RemoveSaved(long peer)
        {
            if (_saved == null) return;
            string key = PeerKey(peer);
            if (key == null) return;
            _saved.Keys.Remove(key);
            // Send before changing leadership or the live member list.
            foreach (Member member in Members)
                if (member.Peer != Me && member.Peer != peer) Send(member.Peer, RpcSaved, SavedPacket());
            if (_saved.Contains(_selfKey)) SaveRoster();
        }
        private static void OnSaved(long sender, ZPackage pkg)
        {
            if (!SavedContext() || !InParty || sender != Leader || Get(sender) == null) return;
            try
            {
                var state = new PersistentRoster { Group = pkg.ReadString() };
                int count = pkg.ReadInt(); if (count < 1 || count > 10) return;
                for (int i = 0; i < count; i++) state.Keys.Add(pkg.ReadString());
                if (!state.Valid(_selfKey)) return;
                _saved = state; SaveRoster();
            }
            catch (Exception e) { Plugin.Log.LogWarning("Invalid saved party message: " + e.Message); }
        }
        private static void TickSaved(float now)
        {
            if (!SavedContext() || now < _nextRestore) return;
            _nextRestore = now + 3f;
            if (Leader == Me && InParty) SendSaved();
            if (_saved == null || !_saved.Valid(_selfKey)) return;
            if (!InParty && now >= _restoreAfter && _pending == null && _joiningLeader == 0 && SentInvites.Count == 0)
            {
                var keys = new List<string>();
                foreach (Online o in OnlinePlayers) { string key = PeerKey(o.Peer); if (key != null) keys.Add(key); }
                if (_saved.IsFirstOnline(_selfKey, keys))
                {
                    Members.Clear(); Members.Add(new Member { Peer = Me, Name = MyName() }); Leader = Me;
                }
            }
            // A leader advertises only to remembered identities, including reconnecting players.
            if (Leader != Me || Members.Count == 0 || _joiningLeader != 0) return;
            foreach (Online o in OnlinePlayers)
                if (_saved.Contains(PeerKey(o.Peer)) && Get(o.Peer) == null)
                {
                    var pkg = new ZPackage(); pkg.Write(_saved.Group); pkg.Write(true); pkg.Write(InParty);
                    Send(o.Peer, RpcResume, pkg);
                }
        }
        private static void OnResume(long sender, ZPackage pkg)
        {
            if (!SavedContext()) return;
            try
            {
                string group = pkg.ReadString(); bool offer = pkg.ReadBool(); string peerKey = PeerKey(sender);
                if (_saved == null || !_saved.CanResume(group, _selfKey, peerKey)) return;
                if (offer)
                {
                    bool established = pkg.ReadBool();
                    if (!_saved.CanAcceptOffer(group, _selfKey, peerKey, established, InParty) || _pending != null || SentInvites.Count > 0) return;
                    _joiningLeader = sender; _joiningAt = Time.time;
                    var request = new ZPackage(); request.Write(group); request.Write(false);
                    Send(sender, RpcResume, request);
                }
                else if (Leader == Me && Members.Count > 0 && Members.Count < Plugin.MaxMembers.Value)
                {
                    Online online = FindOnline(sender); if (online == null) return;
                    var join = new ZPackage(); join.Write(online.Name);
                    OnJoin(sender, new ZPackage(join.GetArray()));
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Invalid party resume message: " + e.Message); }
        }
    }
}
