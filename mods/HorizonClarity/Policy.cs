using System;
namespace HorizonClarity
{
    internal static class Policy
    {
        internal static float Fog(float native, float multiplier) { return native * multiplier; }
        internal static float Clip(float native, float minimum) { return Math.Max(native, minimum); }
    }
}
