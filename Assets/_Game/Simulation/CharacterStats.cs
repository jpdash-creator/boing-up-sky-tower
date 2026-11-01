using System;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// The six stats stored per character (docs/29). Everything else a character does is derived from these and the global <see cref="SimConfig"/>.
    /// The defaults are Momo's baseline and equal the SimConfig defaults exactly, so a character without its own stats plays like Momo (golden hashes and bot proofs unchanged).
    /// No percent anywhere: plain numbers, speeds in units per second, accelerations in units per second squared.
    /// </summary>
    public sealed class CharacterStats
    {
        /// <summary>Upward launch speed of a standing jump, u/s.</summary>
        public int JumpLaunch = 19;
        /// <summary>Top sideways speed, u/s.</summary>
        public int MaxSpeed = 9;
        /// <summary>Ground acceleration, u/s2. Air steering, braking and turn-around follow it.</summary>
        public int Control = 30;
        /// <summary>Share of horizontal speed kept after a wall bounce, in hundredths (77 = 0.77).</summary>
        public int WallBoostHundredths = 77;
        /// <summary>Flat extra coins added to the payout of every cleared level.</summary>
        public int CoinFind;
        /// <summary>Flat extra gems added to the reward of every cleared level.</summary>
        public int GemFind;

        public static readonly CharacterStats Default = new CharacterStats();

        // Limits (docs/29 section 7). Minimums are Momo's baseline: a character only adds, so no proven level becomes unreachable. Maximums are proposals pending owner sign-off.
        public const int JumpLaunchMin = 19, JumpLaunchMax = 23;
        public const int MaxSpeedMin = 9, MaxSpeedMax = 12;
        public const int ControlMin = 30, ControlMax = 45;
        public const int WallBoostMin = 77, WallBoostMax = 100;
        public const int CoinFindMax = 50, GemFindMax = 5;

        /// <summary>Null when the stats are inside the limits, else a short reason.</summary>
        public string Validate()
        {
            if (JumpLaunch < JumpLaunchMin || JumpLaunch > JumpLaunchMax) return "JumpLaunch " + JumpLaunch + " outside " + JumpLaunchMin + ".." + JumpLaunchMax;
            if (MaxSpeed < MaxSpeedMin || MaxSpeed > MaxSpeedMax) return "MaxSpeed " + MaxSpeed + " outside " + MaxSpeedMin + ".." + MaxSpeedMax;
            if (Control < ControlMin || Control > ControlMax) return "Control " + Control + " outside " + ControlMin + ".." + ControlMax;
            if (WallBoostHundredths < WallBoostMin || WallBoostHundredths > WallBoostMax) return "WallBoostHundredths " + WallBoostHundredths + " outside " + WallBoostMin + ".." + WallBoostMax;
            if (CoinFind < 0 || CoinFind > CoinFindMax) return "CoinFind " + CoinFind + " outside 0.." + CoinFindMax;
            if (GemFind < 0 || GemFind > GemFindMax) return "GemFind " + GemFind + " outside 0.." + GemFindMax;
            return null;
        }

        /// <summary>Writes the character's physics into a (cloned) run config. Fixed-point ratios keep the baseline bit-identical to the SimConfig defaults.</summary>
        public void ApplyTo(SimConfig cfg)
        {
            cfg.JumpBase = Fx.FromRatio(JumpLaunch, 60);
            cfg.MaxSpeed = Fx.FromRatio(MaxSpeed, 60);
            cfg.Accel = Fx.FromRatio(Control, 3600);
            cfg.WallBoost = Fx.FromRatio(WallBoostHundredths, 100);
        }

        /// <summary>The numbers the character card shows and the advanced view lists, for these stats under the given global config.</summary>
        public StatSheet Sheet(SimConfig globals)
        {
            SimConfig cfg = globals.Clone();
            ApplyTo(cfg);
            double g = Ups2(cfg.Gravity);
            double launch = Ups(cfg.JumpBase), speed = Ups(cfg.MaxSpeed), accel = Ups2(cfg.Accel);
            double factor = Ratio(cfg.JumpSpeedFactor);
            var s = new StatSheet();
            s.Jump = launch * launch / (2.0 * g);
            s.Speed = speed;
            s.Control = accel;
            s.WallBounce = speed * Ratio(cfg.WallBoost);
            s.CoinFind = CoinFind;
            s.GemFind = GemFind;
            s.AirSteering = accel * Ratio(cfg.AirControl);
            s.Braking = accel * Ratio(cfg.BrakeFactor);
            s.TurnAroundGround = accel * 2.0;
            s.TurnAroundAir = s.AirSteering * 2.0;
            s.TimeToStop = speed / s.Braking;
            s.StopDistance = speed * speed / (2.0 * s.Braking);
            s.TimeToTopSpeed = speed / accel;
            s.FullSpeedLaunch = launch + factor * speed;
            s.HalfSpeedJump = Sq(launch + factor * speed * 0.5) / (2.0 * g);
            s.FullSpeedJump = Sq(s.FullSpeedLaunch) / (2.0 * g);
            s.StickyJump = Sq(launch * Ratio(cfg.StickyJumpMultiplier)) / (2.0 * g);
            s.BounceLaunch = launch * Ratio(cfg.BounceMultiplier);
            s.BounceHeight = Sq(s.BounceLaunch) / (2.0 * g);
            s.Gravity = g;
            return s;
        }

        static double Ups(Fx v) { return v.Raw / 65536.0 * SimConfig.TicksPerSecond; }
        static double Ups2(Fx v) { return v.Raw / 65536.0 * SimConfig.TicksPerSecond * SimConfig.TicksPerSecond; }
        static double Ratio(Fx v) { return v.Raw / 65536.0; }
        static double Sq(double v) { return v * v; }
    }

    /// <summary>Main stats first (what the card shows), then the derived advanced values (docs/29 sections 2 and 3). Display only; the sim uses fixed-point.</summary>
    public struct StatSheet
    {
        public double Jump, Speed, Control, WallBounce;
        public int CoinFind, GemFind;
        public double AirSteering, Braking, TurnAroundGround, TurnAroundAir;
        public double TimeToStop, StopDistance, TimeToTopSpeed;
        public double FullSpeedLaunch, HalfSpeedJump, FullSpeedJump, StickyJump;
        public double BounceLaunch, BounceHeight, Gravity;
    }
}
