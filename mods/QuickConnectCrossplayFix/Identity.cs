using System;

namespace QuickConnectCrossplayFix
{
    internal static class Identity
    {
        internal static string Canonicalize(string key)
        {
            if (key == null || key.Length != 24 || !key.StartsWith("playfab/", StringComparison.OrdinalIgnoreCase)) return key;
            for (int i = 8; i < key.Length; i++)
                if (!Uri.IsHexDigit(key[i])) return key;
            return "playfab/" + key.Substring(8).ToUpperInvariant();
        }
    }
}
