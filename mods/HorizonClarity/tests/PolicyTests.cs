using System;
using HorizonClarity;
class PolicyTests
{
    static void Main()
    {
        if (Policy.Fog(0.004f, 0.25f) != 0.001f || Policy.Fog(0f, 0.25f) != 0f || Policy.Fog(0.004f, 1f) != 0.004f) throw new Exception("Fog scaling");
        if (Policy.Clip(2000, 4000) != 4000 || Policy.Clip(6000, 4000) != 6000) throw new Exception("Camera minimum");
        Console.WriteLine("PASS: native fog scaled, zero/identity preserved, existing longer camera range retained.");
    }
}
