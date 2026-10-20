using System;
using System.Diagnostics;
using SkyHopper.Simulation;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// The decision core of the Autopilot, free of Unity types: call Next(session) once per simulation tick and feed the result to the
    /// session. Plans one hop when the player stands on a platform, then executes it through a reaction-lag line (skill) with timing jitter.
    /// Skill 0 = perfect (no lag, no jitter, the live run equals the plan), 1 = human-like (150 ms lag, up to 3 ticks of timing jitter).
    /// </summary>
    public sealed class AutopilotBrain
    {
        public readonly AutopilotPlanner Planner = new AutopilotPlanner();
        public readonly float Skill;
        public readonly int DelayTicks;
        public readonly int JitterTicks;

        readonly DelayLine delay = new DelayLine();
        RunSession session;
        Maneuver current;
        ManeuverCursor cursor;
        bool active;
        uint rng;

        /// <summary>Hops planned, hops that landed on another platform than the planned one, planner time in ms (statistics only).</summary>
        public int Plans { get; private set; }
        public int Misses { get; private set; }
        public double MaxPlanMs { get; private set; }

        /// <summary>Reaction lag in ticks for a skill: 0 for a perfect player, otherwise 5 (about 80 ms) to 9 (150 ms).</summary>
        public static int DelayTicksFor(float skill)
        {
            if (skill <= 0f) return 0;
            if (skill > 1f) skill = 1f;
            return 5 + (int)Math.Floor(skill * 4f + 0.5f);
        }

        /// <summary>Timing jitter (plus or minus ticks on the wait, run-away and run lengths): 0 for a perfect player, otherwise 1 to 3.</summary>
        public static int JitterTicksFor(float skill)
        {
            if (skill <= 0f) return 0;
            if (skill > 1f) skill = 1f;
            return 1 + (int)Math.Floor(skill * 2f + 0.5f);
        }

        public AutopilotBrain(float skill, int seed, int depth)
        {
            Skill = skill < 0f ? 0f : (skill > 1f ? 1f : skill);
            DelayTicks = DelayTicksFor(Skill);
            JitterTicks = JitterTicksFor(Skill);
            Planner.Depth = depth < 1 ? 1 : depth;
            rng = unchecked((uint)seed * 747796405u + 2891336453u);
            if (rng == 0) rng = 1;
            delay.Reset(DelayTicks);
        }

        void Reset(RunSession ses)
        {
            session = ses;
            active = false;
            cursor = new ManeuverCursor();
            delay.Reset(DelayTicks);
        }

        /// <summary>Input bits for the next tick of <paramref name="ses"/> (already delayed by the reaction lag).</summary>
        public int Next(RunSession ses)
        {
            if (!ReferenceEquals(ses, session)) Reset(ses);
            if (ses.Over) return 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (!active)
                {
                    if (!ses.State.Grounded) return delay.Push(0);   // between hops without a plan (should not happen): coast
                    PlanNow(ses);
                }
                int want = ManeuverRunner.Next(ses.Sim, ses.State, current, ref cursor);
                if (cursor.Done)
                {
                    active = false;
                    if (current.Target >= 0 && ses.State.GroundId != current.Target) Misses++;
                    continue;
                }
                return delay.Push(want);
            }
            return delay.Push(0);
        }

        void PlanNow(RunSession ses)
        {
            long t0 = Stopwatch.GetTimestamp();
            Maneuver m = Planner.Plan(ses.Sim, ses.Spec, ses.Data, ses.State, delay);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms > MaxPlanMs) MaxPlanMs = ms;
            Plans++;
            if (JitterTicks > 0)
            {
                m.Wait = Jittered(m.Wait);
                m.Back = Jittered(m.Back);
                m.Run = Jittered(m.Run);
            }
            current = m;
            ManeuverRunner.Begin(ref cursor, m);
            active = true;
        }

        /// <summary>Length plus a random offset of up to JitterTicks either way; a phase that was used stays at least one tick.</summary>
        int Jittered(int length)
        {
            if (length <= 0) return 0;
            int v = length + RandomOffset();
            return v < 1 ? 1 : v;
        }

        int RandomOffset()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (int)(rng % (uint)(2 * JitterTicks + 1)) - JitterTicks;
        }
    }
}
