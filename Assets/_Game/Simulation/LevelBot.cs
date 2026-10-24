using System.Collections.Generic;

namespace BoingUpSkyTower.Simulation
{
    public struct BotResult
    {
        public bool Solved;
        public int Nodes;
        public int Ticks;
        public int BestRow;
        public int Score;
        public int Coins;
        public int Gems;
        public int HiddenGems;
        public int Hits;
        public int WallBounces;
        public int BestJumpFloors;
        /// <summary>State at the end of the best (solving) line; useful for star checks.</summary>
        public SimState Final;
    }

    /// <summary>
    /// Headless solvability bot (doc 03 section 4 / doc 08 section 4). It searches over landing states: from each
    /// landing it tries scripted actions (wait, run up, jump, steer in the air), simulates them with the real Sim
    /// and recurses on every safe landing. Development tooling, not gameplay code.
    ///
    /// Modes (chosen from the goal): depth-first search for most goals (with goal-aware pruning: no wall bounce,
    /// stay near the hidden gem / combo ladder zone), best-first search on elapsed time for Speedrun, and a
    /// row-layered beam search (MaxCoins) that maximises coins while still completing the goal, used as an oracle
    /// bound for the coin star.
    /// </summary>
    public sealed class LevelBot
    {
        private sealed class ActionSet
        {
            public int[] Waits;
            public int[] Runs;
            public int[] AirHolds;
        }

        private static readonly ActionSet Full = new ActionSet
        {
            Waits = new int[] { 0, 20, 45, 80 },
            Runs = new int[] { 0, 5, 10, 16, 24, 36 },
            AirHolds = new int[] { 0, 8, 16, 28, 45 }
        };
        private static readonly ActionSet Fast = new ActionSet
        {
            Waits = new int[] { 0 },
            Runs = new int[] { 0, 6, 14, 28 },
            AirHolds = new int[] { 0, 12, 30 }
        };
        private static readonly ActionSet Mid = new ActionSet
        {
            Waits = new int[] { 0, 25 },
            Runs = new int[] { 0, 6, 14, 28 },
            AirHolds = new int[] { 0, 10, 24, 45 }
        };
        private static readonly ActionSet Beam = new ActionSet
        {
            Waits = new int[] { 0 },
            Runs = new int[] { 0, 6, 14, 28 },
            AirHolds = new int[] { 0, 10, 24, 45 }
        };
        private static readonly int[] CrumbleWaits = { 0 };
        private const int MaxAirTicks = 300;
        private static readonly int[] HomeRuns = { 0, 6, 14, 28, 40 };
        private static readonly int[] HomeRunsFast = { 0, 8, 20 };
        /// <summary>Ticks to run away from the target first (builds speed on a narrow platform).</summary>
        private static readonly int[] HomeBacks = { 0, 6, 14 };
        private static readonly int[] HomeBacksFast = { 0, 8 };
        /// <summary>Consecutive sideways hops on the same row the depth-first search may chain (bounds recursion depth).</summary>
        private const int MaxSameRowHops = 6;
        /// <summary>
        /// Speedrun best-first search weight: assumed ticks needed per remaining row. With the normal gaps the real figure is
        /// about 40 to 45 ticks per row; a lower (optimistic) guess spreads the search over all rows and exhausts the node budget
        /// (4 of 5 speedrun levels unsolved at 18), 45 solves all five in under 200 nodes. The result is therefore a good
        /// upper bound of the minimum time, not a proven optimum.
        /// </summary>
        private const int TicksPerRowGuess = 45;

        private readonly Sim sim;
        private readonly LevelSpec spec;
        private readonly LevelData data;
        private readonly int targetRow;
        private readonly int maxNodes;
        private readonly HashSet<long> visited = new HashSet<long>();
        private int nodes;
        private int bestRow;
        private SimState best;
        private bool noWall;
        /// <summary>Homing jumps are used in campaign mode only; legacy mode (row target) keeps the original scripted search.</summary>
        private bool UseHoming { get { return spec != null; } }
        /// <summary>End a flight as soon as the goal is met (depth-first search); the beam needs grounded landings instead.</summary>
        private bool stopWhenDone = true;
        private int topRow { get { return sim.Platforms[sim.Platforms.Length - 1].Row; } }
        private int comboValue;
        /// <summary>Search cut: states with more gong hits than this are dropped (default unlimited). 0 proves a level can be won without taking damage.</summary>
        public int MaxHits = int.MaxValue;

        /// <summary>Legacy mode: reach <paramref name="targetRow"/> on a level without goals.</summary>
        public LevelBot(Sim sim, int targetRow, int maxNodes)
        {
            this.sim = sim;
            this.targetRow = targetRow;
            this.maxNodes = maxNodes;
        }

        /// <summary>Campaign mode: solve the 1-star goal of <paramref name="spec"/> on a level built from it.</summary>
        public LevelBot(Sim sim, LevelSpec spec, LevelData data, int maxNodes)
        {
            this.sim = sim;
            this.spec = spec;
            this.data = data;
            this.targetRow = spec.RowsFor();
            this.maxNodes = maxNodes;
            noWall = spec.Goal.Type == GoalType.NoWallBounce;
            comboValue = spec.Goal.Value;
        }

        private bool Done(SimState s)
        {
            if (spec == null) return s.Row >= targetRow;
            return GoalEvaluator.IsComplete(spec, s);
        }

        /// <summary>True when the state can no longer lead to the goal (goal-specific cuts).</summary>
        private bool Prune(SimState c)
        {
            if (c.Hits > MaxHits) return true;
            if (spec == null) return false;
            switch (spec.Goal.Type)
            {
                case GoalType.NoWallBounce:
                    return c.WallBounces > 0;
                case GoalType.Speedrun:
                    return c.Tick > spec.Goal.TimeSec * SimConfig.TicksPerSecond;
                case GoalType.FindHiddenGem:
                    return data != null && data.HiddenGemRow >= 0 && c.HiddenGems == 0 && c.Row > data.HiddenGemRow + 1;
                case GoalType.ComboChain:
                    return data != null && data.LadderEndRow >= 0 && c.Row > data.LadderEndRow + 1 && !Done(c);
                default:
                    return false;
            }
        }

        public BotResult Solve()
        {
            if (spec != null && spec.Goal.Type == GoalType.Speedrun) return SolveFast();
            SimState start = sim.NewState();
            best = start;
            bool solved = Search(start, 0);
            return Result(solved);
        }

        private BotResult Result(bool solved)
        {
            BotResult r = new BotResult();
            r.Solved = solved;
            r.Nodes = nodes;
            r.Ticks = best.Tick;
            r.BestRow = bestRow;
            r.Score = best.Score;
            r.Coins = best.Coins;
            r.Gems = best.Gems;
            r.HiddenGems = best.HiddenGems;
            r.Hits = best.Hits;
            r.WallBounces = best.WallBounces;
            r.BestJumpFloors = best.BestJumpFloors;
            r.Final = best;
            return r;
        }

        // ------------------------------------------------------------------ depth-first

        /// <summary>Returns 1 when the child led to the goal, -1 when the node budget is spent, 0 otherwise.</summary>
        private int Visit(SimState s, SimState c, int sameRow)
        {
            if (c.Row <= s.Row && c.GroundId == s.GroundId) return 0;
            if (c.Row < s.Row) return 0;
            if (c.Row == s.Row && sameRow >= MaxSameRowHops) return 0;
            if (Prune(c)) return 0;
            if (!visited.Add(Key(c))) return 0;
            if (Search(c, c.Row == s.Row ? sameRow + 1 : 0)) return 1;
            return nodes > maxNodes ? -1 : 0;
        }

        private bool IsBounce(int id)
        {
            PlatformKind k = sim.Platforms[id].Kind;
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap;
        }

        /// <summary>Platforms on the one or two rows above a bounce platform (where the bounce flight should go next).</summary>
        private int NextTargets(int bounceId, int[] buf)
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

        private bool Search(SimState s, int sameRow)
        {
            if (s.Row > bestRow) { bestRow = s.Row; if (!Done(best)) best = s; }
            if (Done(s)) { best = s; return true; }
            if (++nodes > maxNodes) return false;

            Platform here = sim.Platforms[s.GroundId];
            int preferred = PreferredDirection(s);
            int[] dirs = { preferred, 0, -preferred };
            int[] waits = here.Kind == PlatformKind.Crumble ? CrumbleWaits : Full.Waits;

            int[] targets = new int[24];
            int tc = UseHoming ? Targets(s, targets) : 0;
            for (int ti = 0; ti < tc; ti++)
            {
                Fx tx = sim.PlatformX(targets[ti], s.Tick + 20);
                int tdir = tx >= s.X ? 1 : -1;
                for (int wi = 0; wi < waits.Length; wi++)
                    for (int bi = 0; bi < HomeBacks.Length; bi++)
                        for (int ri = 0; ri < HomeRuns.Length; ri++)
                        {
                            if (HomeBacks[bi] > 0 && ri == 0) continue;
                            SimState c;
                            if (!TryJump(s, waits[wi], HomeBacks[bi], tdir, HomeRuns[ri], 0, 0, targets[ti], -1, 0, out c)) continue;
                            int v = Visit(s, c, sameRow);
                            if (v != 0) return v > 0;
                        }
            }

            // Bounce targets: land on the pad with speed toward the next platform and steer the bounce flight there.
            int[] nexts = new int[6];
            for (int ti = 0; ti < tc; ti++)
            {
                int tgt = targets[ti];
                if (!IsBounce(tgt)) continue;
                int nc = NextTargets(tgt, nexts);
                Fx tx = sim.PlatformX(tgt, s.Tick + 20);
                int tdir = tx >= s.X ? 1 : -1;
                for (int ni = 0; ni < nc; ni++)
                    for (int bias = 0; bias <= 1; bias++)
                        for (int wi = 0; wi < waits.Length; wi++)
                            for (int bi = 0; bi < HomeBacks.Length; bi++)
                                for (int ri = 0; ri < HomeRuns.Length; ri++)
                                {
                                    if (HomeBacks[bi] > 0 && ri == 0) continue;
                                    SimState c;
                                    if (!TryJump(s, waits[wi], HomeBacks[bi], tdir, HomeRuns[ri], 0, 0, tgt, nexts[ni], bias, out c)) continue;
                                    int v = Visit(s, c, sameRow);
                                    if (v != 0) return v > 0;
                                }
            }

            ActionSet scripted = UseHoming ? Mid : Full;
            int[] swaits = here.Kind == PlatformKind.Crumble ? CrumbleWaits : scripted.Waits;
            for (int wi = 0; wi < swaits.Length; wi++)
            {
                for (int di = 0; di < dirs.Length; di++)
                {
                    for (int ri = 0; ri < scripted.Runs.Length; ri++)
                    {
                        if (dirs[di] == 0 && ri > 0) continue;
                        for (int ai = 0; ai < dirs.Length; ai++)
                        {
                            for (int hi = 0; hi < scripted.AirHolds.Length; hi++)
                            {
                                if (dirs[ai] == 0 && hi > 0) continue;
                                SimState c;
                                if (!TryJump(s, swaits[wi], 0, dirs[di], scripted.Runs[ri], dirs[ai], scripted.AirHolds[hi], -1, -1, 0, out c)) continue;
                                int v = Visit(s, c, sameRow);
                                if (v != 0) return v > 0;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>Steers toward the average X of the next row so the first tries are the likely ones.</summary>
        private int PreferredDirection(SimState s)
        {
            int next = s.Row + 1;
            long sum = 0;
            int count = 0;
            bool gemNext = data != null && data.HiddenGemRow >= 0 && s.HiddenGems == 0 && next == data.HiddenGemRow;
            for (int i = 0; i < sim.Platforms.Length; i++)
            {
                if (sim.Platforms[i].Row == next) { sum += sim.PlatformX(i, s.Tick).Raw; count++; }
                else if (sim.Platforms[i].Row > next) break;
            }
            if (gemNext) return data.HiddenGemX >= s.X ? 1 : -1;
            if (count == 0) return 1;
            Fx target = Fx.FromRaw((int)(sum / count));
            return target >= s.X ? 1 : -1;
        }

        private long Key(SimState c)
        {
            Platform p = sim.Platforms[c.GroundId];
            long vb = (c.Vx.Raw * 20L / Fx.OneRaw) + 16;
            long tb = 0;
            if (p.Kind == PlatformKind.Moving || p.Kind == PlatformKind.Vanishing) tb = (c.Tick / 20) % 24;
            if (sim.Hazards.Length > 0) tb = (c.Tick / 24) % 32;
            long xb = c.X.Raw * 4L / Fx.OneRaw;
            long extra = 0;
            if (spec != null)
            {
                if (spec.Goal.Type == GoalType.ComboChain) extra = GoalEvaluator.JumpsWithFloors(c, comboValue);
                else if (spec.Goal.Type == GoalType.FindHiddenGem) extra = c.HiddenGems;
                else if (spec.Goal.Type == GoalType.Speedrun) extra = c.Tick / 90;
            }
            return c.GroundId + (vb << 20) + (tb << 28) + (xb << 36) + (extra << 44);
        }

        // Scratch state reused by every TryJump so that failed attempts do not allocate; only a landing is cloned.
        private SimState scratch;

        private void LoadScratch(SimState from)
        {
            int[] ct = scratch.CrumbleTick;
            bool[] pt = scratch.PickupTaken;
            if (ct == null || ct.Length != from.CrumbleTick.Length) ct = new int[from.CrumbleTick.Length];
            if (pt == null || pt.Length != from.PickupTaken.Length) pt = new bool[from.PickupTaken.Length];
            System.Array.Copy(from.CrumbleTick, ct, ct.Length);
            System.Array.Copy(from.PickupTaken, pt, pt.Length);
            scratch = from;
            scratch.CrumbleTick = ct;
            scratch.PickupTaken = pt;
        }

        private bool TryJump(SimState from, int wait, int back, int runDir, int runTicks, int airDir, int airHold, int home, int home2, int bias, out SimState result)
        {
            result = from;
            LoadScratch(from);
            int runInput = runDir < 0 ? SimInput.Left : (runDir > 0 ? SimInput.Right : 0);
            int airInput = airDir < 0 ? SimInput.Left : (airDir > 0 ? SimInput.Right : 0);

            for (int i = 0; i < wait; i++)
            {
                sim.Step(ref scratch, 0);
                if (scratch.Dead || !scratch.Grounded) return false;
            }
            int backInput = runDir < 0 ? SimInput.Right : (runDir > 0 ? SimInput.Left : 0);
            for (int i = 0; i < back; i++)
            {
                sim.Step(ref scratch, backInput);
                if (scratch.Dead || !scratch.Grounded) return false;
                if (noWall && scratch.WallBounces > 0) return false;
            }
            for (int i = 0; i < runTicks; i++)
            {
                sim.Step(ref scratch, runInput);
                if (scratch.Dead || !scratch.Grounded) return false;
                if (noWall && scratch.WallBounces > 0) return false;
            }
            sim.Step(ref scratch, runInput | SimInput.Jump);
            if (scratch.Dead || scratch.Grounded) return false;

            int air = 0;
            int curHome = home;
            int lastGround = scratch.GroundId;
            Fx exitBias = Fx.Zero;
            if (home >= 0 && bias != 0 && home2 >= 0)
                exitBias = sim.PlatformX(home2, scratch.Tick + 40) >= sim.PlatformX(home, scratch.Tick + 40) ? ExitBias : -ExitBias;
            while (!scratch.Grounded && !scratch.Dead && air < MaxAirTicks)
            {
                int input = curHome >= 0 ? HomeInput(scratch, curHome, curHome == home ? exitBias : Fx.Zero) : (airHold > 0 && air >= airHold ? 0 : airInput);
                sim.Step(ref scratch, input);
                air++;
                if (scratch.GroundId != lastGround)
                {
                    // landed on a bounce platform: the flight continues toward the next platform
                    lastGround = scratch.GroundId;
                    if (curHome == lastGround && home2 >= 0) curHome = home2;
                }
                if (noWall && scratch.WallBounces > 0) return false;
                if (stopWhenDone ? Done(scratch) : scratch.Row >= topRow) { result = scratch.Clone(); return true; }
            }
            if (scratch.Dead || !scratch.Grounded) return false;
            result = scratch.Clone();
            return true;
        }

        /// <summary>Closed-loop air steering toward a platform: aim for a speed proportional to the remaining distance.</summary>
        private static readonly Fx ExitBias = Fx.FromRatio(9, 100);

        private int HomeInput(SimState c, int id, Fx bias)
        {
            Fx tx = sim.PlatformX(id, c.Tick + 10);
            Fx desired = Fx.Clamp((tx - c.X) / 14 + bias, -sim.Cfg.MaxSpeed, sim.Cfg.MaxSpeed);
            Fx dv = desired - c.Vx;
            Fx dead = Fx.FromRatio(3, 1000);
            if (dv > dead) return SimInput.Right;
            if (dv < -dead) return SimInput.Left;
            return 0;
        }

        /// <summary>Candidate landing platforms ahead: every platform of the next rows (plus the combo row), nearest row first.</summary>
        private int Targets(SimState s, int[] buf)
        {
            int n = 0;
            int ahead = 3;
            int comboRow = (spec != null && spec.Goal.Type == GoalType.ComboChain) ? s.Row + comboValue : -1;
            for (int i = 0; i < sim.Platforms.Length && n < buf.Length; i++)
            {
                int r = sim.Platforms[i].Row;
                if (r <= s.Row - 0 && r != s.Row) continue;
                if ((r > s.Row && r <= s.Row + ahead) || r == comboRow || (r == s.Row && i != s.GroundId)) buf[n++] = i;
                else if (r > s.Row + ahead && r > comboRow) break;
            }
            return n;
        }

        // ------------------------------------------------------------------ non-lazy expansion (best-first, beam)

        private void Expand(SimState s, ActionSet set, bool scripted, List<SimState> output)
        {
            Platform here = sim.Platforms[s.GroundId];
            int preferred = PreferredDirection(s);
            int[] dirs = { preferred, 0, -preferred };
            int[] waits = here.Kind == PlatformKind.Crumble ? CrumbleWaits : set.Waits;
            int[] targets = new int[24];
            int tc = Targets(s, targets);
            for (int ti = 0; ti < tc; ti++)
            {
                Fx tx = sim.PlatformX(targets[ti], s.Tick + 20);
                int tdir = tx >= s.X ? 1 : -1;
                int[] hbacks = set == Beam ? HomeBacks : HomeBacksFast;
                int[] hruns = set == Beam ? HomeRuns : HomeRunsFast;
                for (int wi = 0; wi < waits.Length; wi++)
                    for (int bi = 0; bi < hbacks.Length; bi++)
                    for (int ri = 0; ri < hruns.Length; ri++)
                    {
                        if (hbacks[bi] > 0 && ri == 0) continue;
                        SimState c;
                        if (!TryJump(s, waits[wi], hbacks[bi], tdir, hruns[ri], 0, 0, targets[ti], -1, 0, out c)) continue;
                        if (c.Row <= s.Row && c.GroundId == s.GroundId) continue;
                        if (c.Row < s.Row) continue;
                        if (Prune(c)) continue;
                        output.Add(c);
                    }
            }
            int[] nexts = new int[6];
            for (int ti = 0; ti < tc; ti++)
            {
                int tgt = targets[ti];
                if (!IsBounce(tgt)) continue;
                int nc = NextTargets(tgt, nexts);
                Fx tx = sim.PlatformX(tgt, s.Tick + 20);
                int tdir = tx >= s.X ? 1 : -1;
                for (int ni = 0; ni < nc; ni++)
                    for (int bias = 0; bias <= 1; bias++)
                        for (int wi = 0; wi < waits.Length; wi++)
                            for (int ri = 0; ri < HomeRunsFast.Length; ri++)
                            {
                                SimState c;
                                if (!TryJump(s, waits[wi], 0, tdir, HomeRunsFast[ri], 0, 0, tgt, nexts[ni], bias, out c)) continue;
                                if (c.Row <= s.Row && c.GroundId == s.GroundId) continue;
                                if (c.Row < s.Row) continue;
                                if (Prune(c)) continue;
                                output.Add(c);
                            }
            }
            if (!scripted) return;
            for (int wi = 0; wi < waits.Length; wi++)
                for (int di = 0; di < dirs.Length; di++)
                    for (int ri = 0; ri < set.Runs.Length; ri++)
                    {
                        if (dirs[di] == 0 && ri > 0) continue;
                        for (int ai = 0; ai < dirs.Length; ai++)
                            for (int hi = 0; hi < set.AirHolds.Length; hi++)
                            {
                                if (dirs[ai] == 0 && hi > 0) continue;
                                SimState c;
                                if (!TryJump(s, waits[wi], 0, dirs[di], set.Runs[ri], dirs[ai], set.AirHolds[hi], -1, -1, 0, out c)) continue;
                                if (c.Row <= s.Row && c.GroundId == s.GroundId) continue;
                                if (c.Row < s.Row) continue;
                                if (Prune(c)) continue;
                                output.Add(c);
                            }
                    }
        }

        // ------------------------------------------------------------------ best-first on time (Speedrun)

        private sealed class HeapNode
        {
            public SimState S;
            public int F;
        }

        private static void HeapPush(List<HeapNode> heap, HeapNode n)
        {
            heap.Add(n);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heap[parent].F <= heap[i].F) break;
                HeapNode t = heap[parent]; heap[parent] = heap[i]; heap[i] = t;
                i = parent;
            }
        }

        private static HeapNode HeapPop(List<HeapNode> heap)
        {
            HeapNode top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            int count = heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < count && heap[l].F < heap[m].F) m = l;
                if (r < count && heap[r].F < heap[m].F) m = r;
                if (m == i) break;
                HeapNode t = heap[m]; heap[m] = heap[i]; heap[i] = t;
                i = m;
            }
            return top;
        }

        private BotResult SolveFast()
        {
            List<HeapNode> heap = new List<HeapNode>();
            SimState start = sim.NewState();
            best = start;
            HeapNode root = new HeapNode();
            root.S = start;
            HeapPush(heap, root);
            List<SimState> kids = new List<SimState>();
            while (heap.Count > 0 && nodes < maxNodes)
            {
                HeapNode n = HeapPop(heap);
                SimState s = n.S;
                if (s.Row > bestRow) { bestRow = s.Row; best = s; }
                if (Done(s)) { best = s; return Result(true); }
                nodes++;
                kids.Clear();
                Expand(s, Fast, true, kids);
                for (int i = 0; i < kids.Count; i++)
                {
                    SimState c = kids[i];
                    if (!visited.Add(Key(c))) continue;
                    HeapNode h = new HeapNode();
                    h.S = c;
                    h.F = c.Tick + TicksPerRowGuess * (targetRow - c.Row);
                    HeapPush(heap, h);
                }
            }
            return Result(false);
        }

        // ------------------------------------------------------------------ beam search: coin oracle

        private int Score(SimState s)
        {
            int progress = 0;
            if (spec != null)
            {
                if (spec.Goal.Type == GoalType.FindHiddenGem && s.HiddenGems > 0) progress = 1;
                else if (spec.Goal.Type == GoalType.ComboChain)
                {
                    int have = GoalEvaluator.JumpsWithFloors(s, comboValue);
                    int need = spec.Goal.Count > 0 ? spec.Goal.Count : 1;
                    progress = have > need ? need : have;
                }
            }
            return progress * 1000000 + (s.Coins + s.Gems * 100) * 16 - s.Tick / 8;
        }

        /// <summary>
        /// Maximises coins on a run that completes the goal and reaches the top row (row-layered beam, width states per row).
        /// Solved = such a run was found; Coins = the best coin count found (a lower bound of the true maximum).
        /// </summary>
        public BotResult MaxCoins(int beamWidth)
        {
            return MaxCoinsFrom(sim.NewState(), beamWidth);
        }

        /// <summary>
        /// As MaxCoins, but continues from <paramref name="from"/> (for example the end state of Solve, where the goal is
        /// already met) and only has to climb to the top row. An airborne start state is first settled onto a platform.
        /// </summary>
        public BotResult MaxCoinsFrom(SimState from, int beamWidth)
        {
            return MaxCoinsCore(from, beamWidth, false);
        }

        /// <summary>
        /// Maximises the coins held at the moment the goal is met (the run ends there, owner decision 2026-10-19), without
        /// requiring the top row. This is the star-2 bound for goals that complete before the top (CollectCoins,
        /// ComboChain, FindHiddenGem). Solved = a state meeting the goal was found; Coins = coins at that moment
        /// (a lower bound of the true maximum).
        /// </summary>
        public BotResult MaxCoinsAtGoal(int beamWidth)
        {
            return MaxCoinsCore(sim.NewState(), beamWidth, true);
        }

        private BotResult MaxCoinsCore(SimState from, int beamWidth, bool atGoal)
        {
            stopWhenDone = atGoal;
            int maxRow = sim.Platforms[sim.Platforms.Length - 1].Row;
            List<SimState>[] buckets = new List<SimState>[maxRow + 2];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<SimState>();
            SimState start = from.Clone();
            for (int guard = 0; !start.Grounded && !start.Dead && guard < 400; guard++) sim.Step(ref start, 0);
            best = start;
            if (start.Dead || !start.Grounded) return Result(false);
            buckets[start.Row].Add(start);
            bool found = false;
            SimState bestFinal = start;
            HashSet<long> seen = new HashSet<long>();
            List<SimState> kids = new List<SimState>();
            for (int r = start.Row; r <= maxRow; r++)
            {
                List<SimState> list = buckets[r];
                if (list.Count == 0) continue;
                list.Sort((a, b) => Score(b).CompareTo(Score(a)));
                seen.Clear();
                int taken = 0;
                for (int i = 0; i < list.Count && taken < beamWidth; i++)
                {
                    SimState s = list[i];
                    if (!seen.Add(Key(s))) continue;
                    taken++;
                    nodes++;
                    if (atGoal ? Done(s) : (s.Row >= maxRow && Done(s)))
                    {
                        if (!found || s.Coins > bestFinal.Coins) bestFinal = s;
                        found = true;
                        continue;
                    }
                    if (s.Row > bestRow) { bestRow = s.Row; if (!found) best = s; }
                    kids.Clear();
                    Expand(s, Beam, false, kids);
                    for (int k = 0; k < kids.Count; k++)
                    {
                        if (atGoal && Done(kids[k]))
                        {
                            if (!found || kids[k].Coins > bestFinal.Coins) bestFinal = kids[k];
                            found = true;
                            continue;
                        }
                        if (kids[k].Row > r) buckets[kids[k].Row].Add(kids[k]);
                    }
                }
                list.Clear();
            }
            if (found) best = bestFinal;
            return Result(found);
        }
    }
}
