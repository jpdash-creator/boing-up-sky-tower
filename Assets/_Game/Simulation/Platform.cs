namespace SkyHopper.Simulation
{
    public enum PlatformKind
    {
        Normal = 0,
        Moving = 1,
        Crumble = 2,
        Bounce = 3,
        Vanishing = 4,
        Sticky = 5,
        Slippery = 6,
        /// <summary>Region 2 signature: launches higher than Bounce (SimConfig.BounceCapMultiplier).</summary>
        BounceCap = 7
    }

    /// <summary>A one-way platform. Y is the top surface; X is the centre (base centre for moving platforms).</summary>
    public struct Platform
    {
        public int Id;
        public int Row;
        public PlatformKind Kind;
        public Fx X;
        public Fx Y;
        public Fx Width;
        /// <summary>Moving platforms: horizontal amplitude and triangle-wave period in ticks.</summary>
        public Fx Amplitude;
        public int Period;
        public int Phase;
    }
}
