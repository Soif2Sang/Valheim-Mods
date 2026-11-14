using System;
using QuickConnectCrossplayFix;
class IdentityTests
{
    static void Check(bool ok) { if (!ok) throw new Exception("Identity normalization failed"); }
    static void Main()
    {
        Check(Identity.Canonicalize("playfab/abcdef1234567890") == "playfab/ABCDEF1234567890");
        Check(Identity.Canonicalize("playfab/ABCDEF1234567890") == "playfab/ABCDEF1234567890");
        Check(Identity.Canonicalize("PLAYFAB/abcdef1234567890") == "playfab/ABCDEF1234567890");
        foreach(string key in new[] { null, "", "host/example.org:2456", "steam/1234", "playfab/opaque-identity", "playfab/ghijkl1234567890", "playfab/abcdef" })
            Check(Identity.Canonicalize(key) == key);
        Console.WriteLine("PASS: PlayFab hexadecimal destination canonicalized; other identities and invalid inputs preserved.");
    }
}
