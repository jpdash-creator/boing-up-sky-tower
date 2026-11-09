using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner decision 2026-11-05 ("Fix the limit now"): endless can be PLAYED to floor 100000 and beyond. The played tower is an EndlessWorld: a Stream tower whose heights are
    /// RE-BASED (all heights and platform / gong ids shifted down, floors stay absolute) while the run goes on. These tests pin that a re-base is invisible to the game
    /// logic: the full per-tick state trace of a run with re-bases (also forced in mid-air, also with Rocket / Flight) equals the trace of the same inputs on a tower that never
    /// re-bases, bit for bit (positions minus the origin, ids minus the dropped count), in all 5 regions; that the re-base works at floors 2000, 4000, 7000, 7001 and a
    /// live bot run reaches floor 10000+ with the real threshold; and that chunked generation to floor 100000 stays small in time and memory.
    /// </summary>
    [Trait("Category", "EndlessRebase")]
    public class EndlessRebaseTests
    {
        private readonly ITestOutputHelper output;
        public EndlessRebaseTests(ITestOutputHelper output) { this.output = output; }

        private static readonly int[] OffsetTenThousandths = { 0, 86, 172, 259, 345 };
        private static readonly SignatureKind[] Signatures =
        {
            SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs
        };

        private static LevelSpec SpecFor(int region0, int seed)
        {
            return EndlessTower.MakeSpec(seed, Fx.FromRatio(OffsetTenThousandths[region0], 10000), Signatures[region0], 400);
        }

        /// <summary>A grounded state on a plain platform of row >= <paramref name="row"/> with every time step-up of the scroll speed on (as the cycle tests do).</summary>
        private static SimState StateOn(Sim sim, int row, int tick)
        {
            SimState s = sim.NewState();
            int id = -1;
            for (int i = 0; i < sim.Platforms.Length && id < 0; i++)
                if (sim.Platforms[i].Row >= row && sim.Platforms[i].Kind == PlatformKind.Normal) id = i;
            Assert.True(id >= 0, "a plain platform near row " + row);
            Platform p = sim.Platforms[id];
            s.GroundId = id;
            s.BestGroundId = id;
            s.X = p.X;
            s.Y = p.Y;
            s.MaxY = p.Y;
            s.Row = p.Row;
            s.JumpStartRow = p.Row;
            s.Grounded = true;
            s.Coyote = sim.Cfg.CoyoteTicks;
            s.StartTick = 0;
            s.Tick = tick;
            s.KillY = p.Y - sim.Cfg.StartKillDistance;
            return s;
        }

        /// <summary>Null when run B (re-based world, state b) equals run A (flat tower, state a) after removing the origin and the dropped ids, else a description of the first difference.</summary>
        private static string Diff(in SimState a, in SimState b, EndlessWorld wb)
        {
            long origin = wb.OriginYRaw;
            int drop = wb.DroppedPlatforms;
            if (a.Tick != b.Tick) return "Tick";
            if (a.X.Raw != b.X.Raw) return "X";
            if ((long)a.Y.Raw - origin != b.Y.Raw) return "Y";
            if (a.Vx.Raw != b.Vx.Raw) return "Vx";
            if (a.Vy.Raw != b.Vy.Raw) return "Vy";
            if ((long)a.KillY.Raw - origin != b.KillY.Raw) return "KillY";
            if ((long)a.MaxY.Raw - origin != b.MaxY.Raw) return "MaxY";
            if (a.RocketTicks > 0 && (long)a.RocketTargetY.Raw - origin != b.RocketTargetY.Raw) return "RocketTargetY";
            if (a.GroundId - drop != b.GroundId) return "GroundId " + a.GroundId + " vs " + b.GroundId + " drop " + drop;
            if (a.BestGroundId - drop != b.BestGroundId) return "BestGroundId";
            if (a.Row != b.Row || a.JumpStartRow != b.JumpStartRow || a.RocketTargetRow != b.RocketTargetRow) return "Row";
            if (a.Grounded != b.Grounded || a.Dead != b.Dead || a.Coyote != b.Coyote || a.JumpBuffer != b.JumpBuffer) return "flags";
            if (a.Score != b.Score || a.Coins != b.Coins || a.Combo != b.Combo || a.BestCombo != b.BestCombo || a.Hits != b.Hits || a.Invuln != b.Invuln || a.WallBounces != b.WallBounces) return "scoring";
            if (a.StartTick != b.StartTick || a.LandTick != b.LandTick || a.LastJumpFloors != b.LastJumpFloors || a.JumpHist != b.JumpHist) return "run tracking";
            if (a.TiltDir != b.TiltDir || a.SteerLock != b.SteerLock || a.FloatTicks != b.FloatTicks || a.JumpLock != b.JumpLock || a.GlideTicks != b.GlideTicks || a.KoiStill != b.KoiStill || a.KoiAir != b.KoiAir) return "platform effects";
            if (a.FlightTicks != b.FlightTicks || a.RocketTicks != b.RocketTicks || a.SlowFall != b.SlowFall || a.PwUsed != b.PwUsed || a.Assisted != b.Assisted) return "power-ups";
            if (a.ThornKnocks != b.ThornKnocks || a.SporePops != b.SporePops || a.PufferHops != b.PufferHops || a.Bumps != b.Bumps) return "counters";
            int[] ca = a.CrumbleTick, cb = b.CrumbleTick;
            int common = System.Math.Min(cb.Length, ca.Length - drop);   // the worlds grow in different steps, so only the shared platforms are compared
            if (common < 100) return "CrumbleTick length " + ca.Length + " " + cb.Length + " drop " + drop;
            for (int i = 0; i < common; i++) if (ca[i + drop] != cb[i]) return "CrumbleTick[" + i + "]";
            return null;
        }

        // ------------------------------------------------------------------ (1) bit-identical state traces

        public static IEnumerable<object[]> TraceCases()
        {
            for (int r = 0; r < 5; r++)
            {
                yield return new object[] { r, 2090, 40, false };     // L14 -> L15 at 2101; a re-base at (almost) every chunk
                yield return new object[] { r, 4300, 300, false };    // a later cycle (heights near 15000 u), a re-base every 300 rows
                yield return new object[] { r, 2090, 40, true };      // with Rocket and Flight taps, re-bases also while they run
            }
        }

        [Theory]
        [MemberData(nameof(TraceCases))]
        public void ARebasedRunIsBitIdenticalToTheSameRunOnATowerThatNeverRebases(int region0, int firstFloor, int rebaseEvery, bool powerUps)
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SpecFor(region0, 7000 + region0 * 13 + firstFloor);
            const int floors = 120;

            // reference world A: one flat tower, heights never move (Q16.16 holds it: 4420 rows is about 16000 u)
            LevelData dataA = EndlessTower.BuildFixed(cfg, spec, firstFloor + floors + 100);
            Sim simA = new Sim(cfg, dataA, spec.Difficulty, true, false);
            SimState startA = StateOn(simA, firstFloor, 1800);
            LevelBot bot = new LevelBot(simA, firstFloor + floors, 400000);
            bot.RecordInputs = true;
            BotResult res = bot.SolveFrom(startA);
            Assert.True(res.Solved, "the bot must solve the window (region " + (region0 + 1) + ") best row " + res.BestRow);
            int[] trace = bot.InputTrace;
            Assert.True(trace != null && trace.Length > 200, "trace of " + (trace == null ? -1 : trace.Length) + " ticks");

            // world B: a Stream world that re-bases every 'rebaseEvery' rows (test hook), plus forced re-bases in mid-air
            EndlessWorld wb = new EndlessWorld(cfg, spec);
            wb.Tower.RebaseEveryRows = rebaseEvery;
            wb.PrepareTo(firstFloor + 40);
            Assert.True(wb.Tower.Rebases > 0, "the tower re-based while it was generated up to the start");
            SimState sa = startA;
            SimState sb = StateOn(wb.Sim, firstFloor, 1800);
            Assert.Equal(sa.X.Raw, sb.X.Raw);

            int forcedMidAir = 0, forcedAny = 0, rocketTaps = 0, flightTaps = 0;
            int ticks = 0;

            for (int t = 0; t < trace.Length; t++)
            {
                int input = trace[t];
                if (powerUps && sa.Grounded && !sa.Dead)
                {
                    // even regions tap a Rocket, odd regions a Flight, at the first landing after tick 150
                    if (t >= 150 && rocketTaps + flightTaps == 0) { if (region0 % 2 == 0) { input |= PowerUps.InputBit(PowerUp.Rocket); rocketTaps++; } else { input |= PowerUps.InputBit(PowerUp.Flight); flightTaps++; } }
                }
                simA.Step(ref sa, input);
                wb.Sim.Step(ref sb, input);
                wb.Grow(ref sb);
                ticks++;
                // forced re-bases at awkward moments: in the air, and while a Rocket or Flight burn runs
                bool burning = sb.RocketTicks > 0 || sb.FlightTicks > 0;
                if ((!sb.Grounded && t % 37 == 11) || (burning && t % 9 == 0))
                {
                    int before = wb.RebaseCount;
                    wb.RebaseNow(ref sb);
                    forcedAny++;
                    if (wb.RebaseCount != before && !sb.Grounded) forcedMidAir++;
                }
                string d = Diff(in sa, in sb, wb);
                Assert.True(d == null, "region " + (region0 + 1) + " window " + firstFloor + " every " + rebaseEvery + ": states differ at tick " + t + " (" + d + ")");
                if (sa.Dead) break;
            }
            output.WriteLine("region " + (region0 + 1) + " floor " + firstFloor + " every " + rebaseEvery + (powerUps ? " +power-ups" : "") + ": " + ticks + " ticks, bot nodes " + res.Nodes
                + ", re-bases applied " + wb.RebaseCount + " (forced in mid-air " + forcedMidAir + "), dropped " + wb.DroppedPlatforms + " platforms, origin " + (wb.OriginYRaw / 65536) + " u, rocket taps " + rocketTaps + " flight taps " + flightTaps);
            Assert.True(wb.RebaseCount >= 1, "at least one re-base happened during the run");
            if (!powerUps) Assert.True(forcedMidAir >= 1, "at least one re-base in mid-air");
            Assert.True(!powerUps || rocketTaps + flightTaps >= 1);
        }

        [Fact]
        public void ShiftStateMovesEveryHeightAndIdAndNothingElse()
        {
            SimState s = new SimState();
            s.Y = Fx.FromInt(900); s.KillY = Fx.FromInt(880); s.MaxY = Fx.FromInt(905); s.RocketTargetY = Fx.FromInt(930);
            s.RocketTicks = 5; s.GroundId = 120; s.BestGroundId = 118; s.Row = 777; s.JumpStartRow = 776; s.RocketTargetRow = 790; s.Score = 1234; s.Tick = 4321;
            s.CrumbleTick = new int[150];
            for (int i = 0; i < 150; i++) s.CrumbleTick[i] = i;
            Sim.ShiftState(ref s, 100, Fx.FromInt(850));
            Assert.Equal(Fx.FromInt(50).Raw, s.Y.Raw);
            Assert.Equal(Fx.FromInt(30).Raw, s.KillY.Raw);
            Assert.Equal(Fx.FromInt(55).Raw, s.MaxY.Raw);
            Assert.Equal(Fx.FromInt(80).Raw, s.RocketTargetY.Raw);
            Assert.Equal(20, s.GroundId);
            Assert.Equal(18, s.BestGroundId);
            Assert.Equal(777, s.Row);
            Assert.Equal(776, s.JumpStartRow);
            Assert.Equal(790, s.RocketTargetRow);
            Assert.Equal(1234, s.Score);
            Assert.Equal(4321, s.Tick);
            Assert.Equal(50, s.CrumbleTick.Length);
            Assert.Equal(100, s.CrumbleTick[0]);
            Assert.Equal(149, s.CrumbleTick[49]);
        }

        // ------------------------------------------------------------------ (2) boundaries and a long live run

        private sealed class LongRun
        {
            public int Region, FinalFloor, Rebases, Solves, CheckedBoundaries;
            public long Ms;
            public string Error;
        }

        /// <summary>
        /// A live run through an EndlessWorld exactly as RunSession drives it (the world is grown after landings, re-bases with the real 2500 u threshold), played by the bot
        /// in steps of up to 40 floors (each step is a bot proof from the current landing; the landing state becomes the live state). A re-base is also forced the first
        /// time floors 2000, 4000, 7000 and 7001 are reached.
        /// </summary>
        private LongRun PlayLive(int region0, int goalFloor, bool forceBoundaries)
        {
            Stopwatch sw = Stopwatch.StartNew();
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SpecFor(region0, 20261105 + region0 * 31);
            EndlessWorld world = new EndlessWorld(cfg, spec);
            SimState s = world.NewState();
            LongRun lr = new LongRun { Region = region0 + 1 };
            int[] boundaries = { 2000, 4000, 7000, 7001 };
            int nextBoundary = 0;
            long maxAbsY = 0;
            while (s.Row < goalFloor)
            {
                int target = s.Row + 40;
                if (forceBoundaries && nextBoundary < boundaries.Length && target > boundaries[nextBoundary]) target = boundaries[nextBoundary];
                if (target > goalFloor) target = goalFloor;
                // The proof ends on the first landing at or above the target. A landing on a bounce pad is not grounded (the pad launches at once): ask for one more row then.
                BotResult res = default(BotResult);
                bool ok = false;
                int from = s.Row;
                for (int extra = 0; extra < 14; extra++)
                {
                    LevelBot bot = new LevelBot(world.Sim, target + extra, 300000);
                    res = bot.SolveFrom(s);
                    lr.Solves++;
                    if (res.Solved && res.Final.Grounded && !res.Final.Dead)
                    {
                        // the line may end on a dead end (nothing climbs on from there): accept the landing only when a short proof from it succeeds
                        BotResult more = new LevelBot(world.Sim, res.Final.Row + 5, 40000).SolveFrom(res.Final);
                        if (more.Solved) { ok = true; break; }
                    }
                    if (!res.Solved) break;
                }
                if (!ok) { lr.Error = "region " + lr.Region + ": the bot could not climb from floor " + from + " to " + target + " (best " + res.BestRow + ", " + res.Nodes + " nodes)"; break; }
                s = res.Final;
                world.Grow(ref s);
                if (forceBoundaries && nextBoundary < boundaries.Length && s.Row >= boundaries[nextBoundary])
                {
                    int before = world.RebaseCount;
                    world.RebaseNow(ref s);   // a re-base exactly at the boundary
                    lr.CheckedBoundaries++;
                    nextBoundary++;
                    if (world.RebaseCount == before) { lr.Error = "region " + lr.Region + ": the forced re-base at floor " + s.Row + " dropped nothing"; break; }
                }
                // the window stays small and the Q16.16 range is never approached
                long ay = Math.Abs((long)s.Y.Raw), ak = Math.Abs((long)s.KillY.Raw);
                if (ay > maxAbsY) maxAbsY = ay;
                if (ay > Fx.FromInt(5000).Raw || ak > Fx.FromInt(5000).Raw) { lr.Error = "region " + lr.Region + ": heights grew to " + ay / 65536 + " u at floor " + s.Row; break; }
                if (world.Data.Platforms.Length > 2500) { lr.Error = "region " + lr.Region + ": window of " + world.Data.Platforms.Length + " platforms"; break; }
                // ids and rows stay consistent: the state's platform is a platform of the window at the state's floor
                if (s.GroundId < 0 || s.GroundId >= world.Sim.Platforms.Length || world.Sim.Platforms[s.GroundId].Row != s.Row) { lr.Error = "region " + lr.Region + ": ground id broken at floor " + s.Row; break; }
                if (world.Sim.Platforms[s.GroundId].Id != s.GroundId || Math.Abs((long)s.Y.Raw - world.Sim.Platforms[s.GroundId].Y.Raw) > Fx.FromInt(6).Raw) { lr.Error = "region " + lr.Region + ": ground height broken at floor " + s.Row; break; }
            }
            lr.FinalFloor = s.Row;
            lr.Rebases = world.RebaseCount;
            lr.Ms = sw.ElapsedMilliseconds;
            if (lr.Error == null)
            {
                // the world's absolute heights equal a plain Stream generator's (nothing was corrupted by the player-driven re-bases)
                EndlessTower reference = new EndlessTower(cfg, spec, 100);
                reference.Stream = true;
                reference.GenerateTo(world.Tower.TopRow);
                if (reference.Fingerprint != world.Tower.Fingerprint) lr.Error = "region " + lr.Region + ": the generated tower differs from the reference generator";
                long absY = world.OriginYRaw + s.Y.Raw;
                if (lr.Error == null && absY < (long)(goalFloor * 1.5 * 65536)) lr.Error = "region " + lr.Region + ": absolute height " + absY / 65536 + " u is too low for floor " + s.Row;
            }
            output.WriteLine("region " + lr.Region + ": floor " + lr.FinalFloor + " in " + lr.Ms + " ms, " + lr.Solves + " bot steps, " + lr.Rebases + " re-bases, max |height| " + maxAbsY / 65536 + " u, window " + world.Data.Platforms.Length + " platforms, origin " + world.OriginYRaw / 65536 + " u");
            return lr;
        }

        [Fact]
        public void TheRebaseWorksAtFloors2000_4000_7000_7001_AndABotRunReachesFloor7100InEveryRegion()
        {
            LongRun[] runs = new LongRun[5];
            Parallel.For(0, 5, r => runs[r] = PlayLive(r, 7100, true));
            foreach (LongRun lr in runs)
            {
                Assert.True(lr.Error == null, lr.Error);
                Assert.True(lr.FinalFloor >= 7100, "region " + lr.Region + " stopped at floor " + lr.FinalFloor);
                Assert.Equal(4, lr.CheckedBoundaries);
                Assert.True(lr.Rebases >= 7, "region " + lr.Region + " re-based " + lr.Rebases + " times");
            }
        }

        [Fact]
        public void ALiveBotRunReachesFloor10000WithTheRealThresholdInEveryRegion()
        {
            LongRun[] runs = new LongRun[5];
            Parallel.For(0, 5, r => runs[r] = PlayLive(r, 10050, false));
            foreach (LongRun lr in runs)
            {
                Assert.True(lr.Error == null, lr.Error);
                Assert.True(lr.FinalFloor >= 10050, "region " + lr.Region + " stopped at floor " + lr.FinalFloor);
                Assert.True(lr.Rebases >= 15, "region " + lr.Region + " re-based " + lr.Rebases + " times");   // about every 500 floors
            }
        }

        // ------------------------------------------------------------------ (4) chunked generation: memory and allocation

        [Fact]
        public void TheWorldGrowsInChunksToFloor100000WithABoundedWindowAndLittleAllocationPerGrowth()
        {
            SimConfig cfg = new SimConfig();
            for (int r0 = 0; r0 < 5; r0++)
            {
                GC.Collect();
                long mem0 = GC.GetTotalMemory(true);
                EndlessWorld world = new EndlessWorld(cfg, SpecFor(r0, 90000 + r0));
                SimState s = world.NewState();
                long maxGrowthBytes = 0, totalBytes = 0;
                int growths = 0, maxWindow = 0;
                Stopwatch sw = Stopwatch.StartNew();
                while (world.Tower.TopRow < 100000)
                {
                    // the player stands 59 rows below the top of the tower (the game grows the tower when the player gets within 60 rows) on the platform of that row (as in a live run)
                    int row = world.Tower.TopRow - 59;
                    int id = world.Data.Platforms.Length - 1;
                    while (world.Data.Platforms[id].Row > row) id--;
                    s.GroundId = id; s.BestGroundId = id; s.Row = row;
                    long a0 = GC.GetAllocatedBytesForCurrentThread();
                    bool grew = world.Grow(ref s);
                    long used = GC.GetAllocatedBytesForCurrentThread() - a0;
                    Assert.True(grew || world.Tower.TopRow >= 100000, "the tower must grow when the player is 59 rows below the top");
                    if (grew) { growths++; totalBytes += used; if (used > maxGrowthBytes) maxGrowthBytes = used; }
                    if (world.Data.Platforms.Length > maxWindow) maxWindow = world.Data.Platforms.Length;
                    // ids of the window are its indices, heights stay sorted and small
                    if (growths % 200 == 0)
                    {
                        Platform[] p = world.Data.Platforms;
                        for (int i = 0; i < p.Length; i++)
                        {
                            Assert.Equal(i, p[i].Id);
                            if (i > 0) Assert.True(p[i].Y >= p[i - 1].Y);
                        }
                        Assert.True(p[p.Length - 1].Y.Raw < Fx.FromInt(4000).Raw);
                    }
                }
                sw.Stop();
                long retained = GC.GetTotalMemory(true) - mem0;
                output.WriteLine("region " + (r0 + 1) + ": floor " + world.Tower.TopRow + " in " + growths + " growths, " + sw.ElapsedMilliseconds + " ms, " + world.RebaseCount + " re-bases, window max " + maxWindow
                    + " platforms, per growth: max " + maxGrowthBytes / 1024 + " KB avg " + totalBytes / Math.Max(1, growths) / 1024 + " KB, retained " + retained / 1024 + " KB");
                Assert.True(maxWindow < 2500, "window of " + maxWindow);
                Assert.True(maxGrowthBytes < 400 * 1024, "one growth allocated " + maxGrowthBytes / 1024 + " KB");   // 2 snapshot arrays + Sim + state arrays; stays under the 85 KB large-object limit per array
                Assert.True(retained < 4L * 1024 * 1024, "retained " + retained);
                Assert.True(world.RebaseCount >= 150, "re-based " + world.RebaseCount + " times");
                GC.KeepAlive(world);
            }
        }

        [Fact]
        public void AFlatWorldStillStopsAtMaxRowsAndTheGeneratorDefaultsAreUnchanged()
        {
            SimConfig cfg = new SimConfig();
            EndlessWorld flat = new EndlessWorld(cfg, SpecFor(0, 5), false);
            flat.Tower.GenerateTo(100000);
            Assert.Equal(EndlessTower.MaxRows, flat.Tower.TopRow);
            Assert.Equal(0, flat.Tower.Rebases);
            EndlessTower t = new EndlessTower(cfg, SpecFor(0, 5), 100);
            Assert.False(t.Stream);
            Assert.Equal(int.MaxValue, t.ProtectRow);
            Assert.Equal(0, t.RebaseEveryRows);
        }
    }
}
