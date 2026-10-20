using SkyHopper.Simulation;

namespace SkyHopper.Gameplay
{
    /// <summary>Fixed-point to float for RENDERING only. Never feed floats back into the Simulation.</summary>
    public static class FxExt
    {
        public static float F(this Fx v)
        {
            return v.Raw / 65536f;
        }
    }
}
