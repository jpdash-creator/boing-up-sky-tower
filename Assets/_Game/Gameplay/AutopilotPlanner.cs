using System;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Gameplay
{
    // Development-only test tooling (the Autopilot). Plain C#, no Unity types, so it is unit-testable and allocation-free per tick:
    // only AutopilotPlanner.Plan() does real work, once per landing, on buffers it allocated up front.

    /// <summary>One hop: wait, run away from the target (build speed), run toward it, jump, then steer to the target platform in the air.</summary>
    public struct Maneuver
    {
        public bool Valid;
        /// <summary>Platform id the air steering homes on, -1 = none (jump straight up).</summary>
        public int Target;
        /// <summary>After landing on a bounce platform the flight continues toward this platform, -1 = none.</summary>
        public int Target2;
        public int Wait, Back, Run;
        /// <summary>-1 left, +1 right, 0 none.</summary>
        public int RunDir;
        public bool Bias;
    }

    /// <summary>Progress of a Maneuver. Default value = not started.</summary>
    public struct ManeuverCursor
    {
        public int Phase;
        public int Count;
        public int LastGround;
        public int Home;
        public int AirTicks;
        public int GroundWait;
        public bool Airborne;
        public bool Done;
        public Fx ExitBias;
    }

    /// <summary>Models human reaction lag: the value pushed now comes out Delay pushes later (0 = no lag). Fixed ring, no allocation.</summary>
    public sealed class DelayLine
    {
        public const int Size = 32;
        readonly int[] buf = new int[Size];
        int pos;
        public int Delay { get; private set; }

        public void Reset(int delay)
        {
            Delay = delay < 0 ? 0 : (delay >= Size ? Size - 1 : delay);
            for (int i = 0; i < Size; i++) buf[i] = 0;
            pos = 0;
        }

        public int Push(int value)
        {
            buf[pos & (Size - 1)] = value;
            int result = buf[(pos - Delay) & (Size - 1)];
            pos++;
            return result;
        }

        public void CopyFrom(DelayLine other)
        {
            Delay = other.Delay;
            pos = other.pos;
            Array.Copy(other.buf, buf, Size);
        }
    }

    /// <summary>
    /// Turns a Maneuver into input bits tick by tick. The planner simulates candidates with exactly this code, so at skill 0 (no jitter)
    /// the live run reproduces the plan. The air steering is the closed-loop "home on a platform" rule of the LevelBot.
    /// </summary>
    public static class ManeuverRunner
    {
        public const int PhaseWait = 0, PhaseBack = 1, PhaseRun = 2, PhaseJump = 3, PhaseAir = 4;
        public const int MaxAirTicks = 300;
        const int JumpGraceTicks = 30;
        static readonly Fx ExitBiasAmount = Fx.FromRatio(9, 100);
        static readonly Fx HomeDead = Fx.FromRatio(3, 1000);

        public static void Begin(ref ManeuverCursor c, Maneuver m)
        {
            c = new ManeuverCursor();
            c.Home = m.Target;
        }

        /// <summary>Input for the next tick given the state before it. Sets c.Done on landing, death or a jump that never happened.</summary>
        public static int Next(Sim sim, in SimState s, in Maneuver m, ref ManeuverCursor c)
        {
            if (c.Done) return 0;
            if (s.Dead) { c.Done = true; return 0; }
            int runInput = m.RunDir < 0 ? SimInput.Left : (m.RunDir > 0 ? SimInput.Right : 0);
            int backInput = m.RunDir < 0 ? SimInput.Right : (m.RunDir > 0 ? SimInput.Left : 0);
            if (c.Phase < PhaseAir && !s.Grounded) EnterAir(sim, s, m, ref c, true);   // slipped off the edge: just steer to the target
            for (; ; )
            {
                switch (c.Phase)
                {
                    case PhaseWait:
                        if (c.Count < m.Wait) { c.Count++; return 0; }
                        c.Phase = PhaseBack; c.Count = 0;
                        break;
                    case PhaseBack:
                        if (c.Count < m.Back) { c.Count++; return backInput; }
                        c.Phase = PhaseRun; c.Count = 0;
                        break;
                    case PhaseRun:
                        if (c.Count < m.Run) { c.Count++; return runInput; }
                        c.Phase = PhaseJump;
                        break;
                    case PhaseJump:
                        EnterAir(sim, s, m, ref c, false);
                        return runInput | SimInput.Jump;
                    default:
                        return Air(sim, s, m, ref c);
                }
            }
        }

        static void EnterAir(Sim sim, in SimState s, in Maneuver m, ref ManeuverCursor c, bool alreadyAirborne)
        {
            c.Phase = PhaseAir;
            c.LastGround = s.GroundId;
            c.AirTicks = 0;
            c.GroundWait = 0;
            c.Airborne = alreadyAirborne;
            c.Home = m.Target;
            c.ExitBias = Fx.Zero;
            if (m.Bias && m.Target >= 0 && m.Target2 >= 0)
                c.ExitBias = sim.PlatformX(m.Target2, s.Tick + 40) >= sim.PlatformX(m.Target, s.Tick + 40) ? ExitBiasAmount : -ExitBiasAmount;
        }

        static int Air(Sim sim, in SimState s, in Maneuver m, ref ManeuverCursor c)
        {
            if (!s.Grounded) c.Airborne = true;
            else
            {
                // Landed after flying: the hop is over. Still grounded and never flew: waiting for the (delayed) jump to happen.
                if (c.Airborne || ++c.GroundWait > JumpGraceTicks) c.Done = true;
                return 0;
            }
            if (++c.AirTicks > MaxAirTicks) { c.Done = true; return 0; }
            if (s.GroundId != c.LastGround)
            {
                c.LastGround = s.GroundId;
                if (c.Home == s.GroundId && m.Target2 >= 0) c.Home = m.Target2;   // bounced: the flight continues to the next platform
            }
            if (c.Home < 0) return 0;
            return HomeInput(sim, s, c.Home, c.Home == m.Target ? c.ExitBias : Fx.Zero);
        }

        /// <summary>Closed-loop air steering toward a platform: aim for a speed proportional to the remaining distance.</summary>
        public static int HomeInput(Sim sim, in SimState s, int id, Fx bias)
        {
            Fx tx = sim.PlatformX(id, s.Tick + 10);
            Fx desired = Fx.Clamp((tx - s.X) / 14 + bias, -sim.Cfg.MaxSpeed, sim.Cfg.MaxSpeed);
            Fx dv = desired - s.Vx;
            if (dv > HomeDead) return SimInput.Right;
            if (dv < -HomeDead) return SimInput.Left;
            return 0;
        }
    }

    /// <summary>Counts platforms near a point (the death report and the "dense stack" flag).</summary>
    public static class StackCounter
    {
        /// <summary>Half size in units of the square window around the player.</summary>
        public static readonly Fx Window = Fx.FromInt(3);
        /// <summary>This many platforms or more inside the window count as a dense stack (the owner saw 8 to 10 stacked).</summary>
        public const int DenseThreshold = 6;

        /// <summary>Number of platforms within the window around (x, y) at <paramref name="tick"/>; their ids go to <paramref name="ids"/> (up to its length).</summary>
        public static int Count(Sim sim, int tick, Fx x, Fx y, Fx half, int[] ids, out int stored)
        {
            int total = 0;
            stored = 0;
            Platform[] ps = sim.Platforms;
            for (int i = 0; i < ps.Length; i++)
            {
                Fx dy = Fx.Abs(ps[i].Y - y);
                if (dy > half) continue;
                Fx dx = Fx.Abs(sim.PlatformX(i, tick) - x);
                if (dx > half) continue;
                total++;
                if (ids != null && stored < ids.Length) ids[stored++] = i;
            }
            return total;
        }

        public static bool IsDense(int count) { return count >= DenseThreshold; }
    }

    /// <summary>
    /// Plans one hop at a time on the live simulation. It enumerates maneuvers (wait, run away, run toward, jump, home in) toward the platforms
    /// of the next rows, simulates each with the real Sim and keeps the best landings; with Depth 2 or more it also looks one landing further
    /// ahead (beam search) so it avoids dead ends. Same action sets and air steering as LevelBot, but it returns the inputs instead of only the end
    /// state, and it models the human reaction lag (DelayLine) so a plan is exact when the live run adds no jitter.
    /// </summary>
    public sealed class AutopilotPlanner
    {
        static readonly int[] Waits = { 0, 20, 45 };
        static readonly int[] CrumbleWaits = { 0 };
        static readonly int[] Backs = { 0, 6, 14 };
        static readonly int[] Runs = { 0, 6, 14, 28, 40 };
        const int FailScore = int.MinValue;
        const int DoneBonus = 1000000;

        sealed class Slot
        {
            public SimState State;
            public Maneuver M;
            public int Score;
            public long Key;
            public bool Done;
        }

        /// <summary>Landings looked ahead (1 = greedy).</summary>
        public int Depth = 2;
        /// <summary>Best landings kept per node.</summary>
        public int Beam = 3;
        /// <summary>Simulated maneuvers per node and in total per Plan() call (bounds the time of one plan).</summary>
        public int NodeSims = 600;
        public int MaxSims = 3000;
        public int LastSims { get; private set; }

        Sim sim;
        LevelSpec spec;
        LevelData data;
        int sims;
        bool noWall;
        int startBounces;
        int coinWeight;
        SimState scratch;
        readonly DelayLine work = new DelayLine();
        DelayLine live;
        Slot[][] beams;
        int[][] targetBufs;
        readonly int[] nexts = new int[6];

        void EnsureBuffers()
        {
            int depth = Depth < 1 ? 1 : Depth;
            int beam = Beam < 1 ? 1 : Beam;
            if (beams != null && beams.Length == depth && beams[0].Length == beam) return;
            beams = new Slot[depth][];
            targetBufs = new int[depth][];
            for (int d = 0; d < depth; d++)
            {
                beams[d] = new Slot[beam];
                for (int k = 0; k < beam; k++) beams[d][k] = new Slot();
                targetBufs[d] = new int[32];
            }
        }

        /// <summary>Best first hop from a grounded state. Never returns an invalid maneuver for a grounded state (falls back to a jump on the spot).</summary>
        public Maneuver Plan(Sim simulation, LevelSpec levelSpec, LevelData levelData, in SimState start, DelayLine liveDelay)
        {
            sim = simulation;
            spec = levelSpec;
            data = levelData;
            live = liveDelay;
            sims = 0;
            noWall = spec != null && spec.Goal.Type == GoalType.NoWallBounce;
            coinWeight = spec != null && spec.Goal.Type == GoalType.CollectCoins ? 300 : 4;
            EnsureBuffers();

            Maneuver best = new Maneuver();
            Search(0, start, ref best);
            LastSims = sims;
            if (!best.Valid)
            {
                best = new Maneuver();
                best.Valid = true;
                best.Target = -1;
                best.Target2 = -1;
            }
            return best;
        }

        int Search(int depth, in SimState from, ref Maneuver first)
        {
            Slot[] slots = beams[depth];
            int n = Generate(depth, from, 0, slots);
            if (n == 0) n = Generate(depth, from, 1, slots);
            if (n == 0) return FailScore;
            if (depth == beams.Length - 1)
            {
                if (depth == 0) first = slots[0].M;
                return slots[0].Score;
            }
            int bestScore = FailScore;
            int bestIdx = 0;
            for (int i = 0; i < n; i++)
            {
                int sub;
                if (slots[i].Done) sub = slots[i].Score;
                else
                {
                    Maneuver ignored = new Maneuver();
                    int deeper = Search(depth + 1, slots[i].State, ref ignored);
                    sub = deeper == FailScore ? slots[i].Score - 3000 : deeper;   // a safe landing with no way on is worth about three rows less
                }
                if (sub > bestScore) { bestScore = sub; bestIdx = i; }
            }
            if (depth == 0) first = slots[bestIdx].M;
            return bestScore;
        }

        int Generate(int depth, in SimState from, int tier, Slot[] slots)
        {
            int n = 0;
            if (sims >= MaxSims) return 0;
            int nodeStart = sims;
            Platform here = sim.Platforms[from.GroundId];
            int[] waits = here.Kind == PlatformKind.Crumble ? CrumbleWaits : Waits;
            int[] targets = targetBufs[depth];
            int tc = Targets(from, tier, targets);
            for (int ti = 0; ti < tc; ti++)
            {
                int tgt = targets[ti];
                int tdir = sim.PlatformX(tgt, from.Tick + 20) >= from.X ? 1 : -1;
                PlatformKind tk = sim.Platforms[tgt].Kind;
                bool bounce = tk == PlatformKind.Bounce || tk == PlatformKind.BounceCap;
                int nc = bounce ? NextTargets(tgt, nexts) : 0;
                int variants = bounce && nc > 0 ? nc * 2 : 1;
                for (int v = 0; v < variants; v++)
                {
                    int t2 = bounce && nc > 0 ? nexts[v >> 1] : -1;
                    bool bias = bounce && nc > 0 && (v & 1) == 1;
                    for (int wi = 0; wi < waits.Length; wi++)
                        for (int bi = 0; bi < Backs.Length; bi++)
                            for (int ri = 0; ri < Runs.Length; ri++)
                            {
                                if (Backs[bi] > 0 && Runs[ri] == 0) continue;
                                if (sims >= MaxSims || sims - nodeStart >= NodeSims) return n;
                                Maneuver m = new Maneuver();
                                m.Valid = true;
                                m.Target = tgt;
                                m.Target2 = t2;
                                m.Wait = waits[wi];
                                m.Back = Backs[bi];
                                m.Run = Runs[ri];
                                m.RunDir = tdir;
                                m.Bias = bias;
                                n = Evaluate(depth, from, m, tier, slots, n);
                            }
                }
            }
            return n;
        }

        int Evaluate(int depth, in SimState from, Maneuver m, int tier, Slot[] slots, int n)
        {
            sims++;
            CopyInto(from, ref scratch);
            startBounces = from.WallBounces;
            if (depth == 0) work.CopyFrom(live); else work.Reset(live.Delay);
            bool goalDone;
            if (!Simulate(m, ref scratch, out goalDone)) return n;
            int hereRow = sim.Platforms[from.GroundId].Row;
            int landRow = sim.Platforms[scratch.GroundId].Row;
            if (!goalDone)
            {
                if (scratch.GroundId == from.GroundId) return n;
                if (tier == 0)
                {
                    if (landRow < hereRow) return n;
                    if (Prune(scratch)) return n;
                }
            }
            int score = Score(scratch, goalDone);
            long key = Key(scratch);
            // dedupe on the landing spot, keep the better one
            for (int i = 0; i < n; i++)
            {
                if (slots[i].Key != key) continue;
                if (score <= slots[i].Score) return n;
                Fill(slots[i], m, score, key, goalDone);
                SortUp(slots, i);
                return n;
            }
            if (n < slots.Length)
            {
                Fill(slots[n], m, score, key, goalDone);
                SortUp(slots, n);
                return n + 1;
            }
            if (score <= slots[n - 1].Score) return n;
            Fill(slots[n - 1], m, score, key, goalDone);
            SortUp(slots, n - 1);
            return n;
        }

        void Fill(Slot slot, Maneuver m, int score, long key, bool done)
        {
            CopyInto(scratch, ref slot.State);
            slot.M = m;
            slot.Score = score;
            slot.Key = key;
            slot.Done = done;
        }

        /// <summary>Insertion step: moves slot i toward the front while it beats its predecessor.</summary>
        static void SortUp(Slot[] slots, int i)
        {
            while (i > 0 && slots[i].Score > slots[i - 1].Score)
            {
                Slot t = slots[i]; slots[i] = slots[i - 1]; slots[i - 1] = t;
                i--;
            }
        }

        bool Simulate(Maneuver m, ref SimState st, out bool goalDone)
        {
            goalDone = false;
            ManeuverCursor c = new ManeuverCursor();
            ManeuverRunner.Begin(ref c, m);
            int limit = m.Wait + m.Back + m.Run + 2 + ManeuverRunner.MaxAirTicks + 40 + work.Delay;
            for (int i = 0; i < limit; i++)
            {
                int want = ManeuverRunner.Next(sim, st, m, ref c);
                if (c.Done) return !st.Dead && st.Grounded && c.Airborne;
                sim.Step(ref st, work.Push(want));
                if (st.Dead) return false;
                if (noWall && st.WallBounces > startBounces) return false;
                if (spec != null && GoalEvaluator.IsComplete(spec, st)) { goalDone = true; return true; }
                if (c.Phase < ManeuverRunner.PhaseAir && !st.Grounded) return false;   // fell off the start platform before the jump
            }
            return false;
        }

        int Score(in SimState st, bool goalDone)
        {
            int s = sim.Platforms[st.GroundId].Row * 1000;
            s += (st.Coins + st.Gems * 10) * coinWeight + st.Stars * 40 + st.HiddenGems * 3000 - st.Hits * 60 - st.Tick / 16;
            if (spec != null && spec.Goal.Type == GoalType.ComboChain) s += GoalEvaluator.JumpsWithFloors(st, spec.Goal.Value) * 1200;
            if (goalDone) s += DoneBonus;
            return s;
        }

        /// <summary>Goal-specific cuts, as in LevelBot: states that can no longer lead to the goal.</summary>
        bool Prune(in SimState c)
        {
            if (spec == null) return false;
            switch (spec.Goal.Type)
            {
                case GoalType.Speedrun:
                    return c.Tick > spec.Goal.TimeSec * SimConfig.TicksPerSecond;
                case GoalType.FindHiddenGem:
                    return data != null && data.HiddenGemRow >= 0 && c.HiddenGems == 0 && c.Row > data.HiddenGemRow + 1;
                default:
                    return false;
            }
        }

        long Key(in SimState c)
        {
            long vb = (c.Vx.Raw * 20L / Fx.OneRaw) + 16;
            long xb = c.X.Raw * 4L / Fx.OneRaw;
            return c.GroundId + (vb << 20) + (xb << 36);
        }

        /// <summary>Candidate landing platforms: tier 0 = the next three rows plus the other platforms of this row; tier 1 also the three rows below.</summary>
        int Targets(in SimState s, int tier, int[] buf)
        {
            int hereRow = sim.Platforms[s.GroundId].Row;
            int n = 0;
            Platform[] ps = sim.Platforms;
            for (int i = 0; i < ps.Length && n < buf.Length; i++)
            {
                int r = ps[i].Row;
                if (r <= hereRow) continue;
                if (r > hereRow + 3) break;
                buf[n++] = i;
            }
            for (int i = 0; i < ps.Length && n < buf.Length; i++)
            {
                int r = ps[i].Row;
                if (i == s.GroundId) continue;
                if (r > hereRow) break;
                if (r == hereRow || (tier > 0 && r >= hereRow - 3)) buf[n++] = i;
            }
            return n;
        }

        /// <summary>Platforms on the one or two rows above a bounce platform (where the bounce flight should go next).</summary>
        int NextTargets(int bounceId, int[] buf)
        {
            int row = sim.Platforms[bounceId].Row;
            int n = 0;
            for (int i = bounceId + 1; i < sim.Platforms.Length && n < buf.Length; i++)
            {
                int r = sim.Platforms[i].Row;
                if (r > row + 2) break;
                if (r > row) buf[n++] = i;
            }
            return n;
        }

        /// <summary>Copies a state into a reusable one (the arrays are copied, not shared).</summary>
        public static void CopyInto(in SimState from, ref SimState to)
        {
            int[] ct = to.CrumbleTick;
            bool[] pt = to.PickupTaken;
            if (ct == null || ct.Length != from.CrumbleTick.Length) ct = new int[from.CrumbleTick.Length];
            if (pt == null || pt.Length != from.PickupTaken.Length) pt = new bool[from.PickupTaken.Length];
            Array.Copy(from.CrumbleTick, ct, ct.Length);
            Array.Copy(from.PickupTaken, pt, pt.Length);
            to = from;
            to.CrumbleTick = ct;
            to.PickupTaken = pt;
        }
    }
}
