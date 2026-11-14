using System;
using System.IO;
using Warband;

class WarbandPersistenceTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "warband-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string a = PersistentRoster.Identity("Steam_testA", "Alice"), b = PersistentRoster.Identity("Steam_testB", "Bob");
            Check(a != b && a != PersistentRoster.Identity("Steam_testA", "Other"), "Stable character identity");
            Check(PersistentRoster.Identity(null, "Alice") == null, "No name-only restore");
            string file = Path.Combine(dir, PersistentRoster.FileName(101, a));
            Check(file != Path.Combine(dir, PersistentRoster.FileName(102, a)), "World isolation");
            Check(file != Path.Combine(dir, PersistentRoster.FileName(101, b)), "Character isolation");
            var roster = new PersistentRoster { Group = Guid.NewGuid().ToString("N") };
            roster.Keys.Add(a); roster.Keys.Add(b); roster.Save(file);
            var loaded = PersistentRoster.Load(file, a);
            Check(loaded.CanResume(roster.Group, a, b), "Rejoin after restart with a new connection ID");
            Check(!loaded.CanResume(Guid.NewGuid().ToString("N"), a, b), "Reject another group");
            Check(!loaded.CanResume(roster.Group, a, "stranger"), "Reject strangers");
            Check(!loaded.CanResume(roster.Group, a, a), "Reject self");
            Check(loaded.CanAcceptOffer(roster.Group, a, b, true, false), "Existing group takes priority on reconnect");
            Check(!loaded.CanAcceptOffer(roster.Group, a, b, true, true), "Do not replace a live party");
            Check(loaded.CanAcceptOffer(roster.Group, a, b, false, false) != loaded.CanAcceptOffer(roster.Group, b, a, false, false), "Simultaneous restore cannot elect both leaders");
            Check(loaded.IsFirstOnline(a, new string[0]), "First player can wait alone");
            Check(loaded.IsFirstOnline(a, new[] { b }) != loaded.IsFirstOnline(b, new[] { a }), "One deterministic restoring leader");
            // Disconnection does not mutate the permanent roster; explicit exclusion does.
            Check(PersistentRoster.Load(file, a).Contains(b), "Offline member retained");
            loaded.Keys.Remove(b); loaded.Save(file);
            Check(!PersistentRoster.Load(file, a).CanResume(roster.Group, a, b), "Kick survives disk round trip");
            File.Delete(file);
            Check(PersistentRoster.Load(file, a) == null, "Explicit leave survives restart");
            roster.Keys.Add(a); Check(!roster.Valid(a), "Duplicate identities rejected");
            File.WriteAllText(file, "not xml");
            bool corrupt = false; try { PersistentRoster.Load(file, a); } catch { corrupt = true; }
            Check(corrupt, "Corrupt save rejected");
            Console.WriteLine("PASS: party disk round trip, world/character isolation, stable identities, election, leave/kick, invalid data.");
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(dir)).TrimEnd(Path.DirectorySeparatorChar) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
                throw new Exception("Unexpected test directory");
            Directory.Delete(dir, true);
        }
    }
}
