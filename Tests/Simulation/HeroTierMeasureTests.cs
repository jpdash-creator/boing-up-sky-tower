using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyClimbers.Economy;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Measurement (not a pass/fail gate): how hard is Region 1 level 15 for each tier of a hero line, played with that hero's EFFECTIVE (capped) stats.
    /// Metrics per hero over N random attempt layouts of the level:
    ///   ok%      share of homing jump options (next 3 rows x back-off x run-up) that land alive on a higher row, probed from every spine platform (lower = harder to play);
    ///   solved%  the bot finds a winning line within the node budget;  nodes  bot search effort (higher = harder);
    ///   clean%   the bot finds a winning line with ZERO gong hits (MaxHits = 0).
    /// Writes Tests/Simulation/HERO_TIER_REPORT.md when UPDATE_GOLDEN=1. Filter: dotnet test --filter Category=HeroTier.
    /// </summary>
    [Trait("Category", "HeroTier")]
    public class HeroTierMeasureTests
    {
        private readonly ITestOutputHelper output;
        public HeroTierMeasureTests(ITestOutputHelper output) { this.output = output; }

        public sealed class Result
        {
            public string Id, Name;
            public CharacterStats Stats;
            public StatSheet Sheet;
            public double Ok, Solved, Nodes, Clean, Ticks;
        }

        static string[] Heroes()
        {
            string env = Environment.GetEnvironmentVariable("HERO_IDS");
            if (!string.IsNullOrEmpty(env)) return env.Split(',');
            return new[] { "momo", "momo_u", "momo_r", "momo_e", "momo_l", "pip", "pip_l" };
        }

        static Result MeasureHero(CharacterSpec hero, LoadedLevel ll, int seeds, int launchTenths = 0)
        {
            SimConfig cfg = new SimConfig();
            CharacterStats st = hero.Stats ?? CharacterStats.Default;
            SimConfig run = cfg.Clone();
            st.ApplyTo(run);   // exactly what GameSession.BuildRunCfg does
            if (launchTenths > 0) run.JumpBase = Fx.FromRatio(launchTenths, 600);   // fractional launch (tenths of u/s), as ApplyTo does with whole numbers: ratio launch / 60 ticks
            var r = new Result { Id = hero.Id, Name = hero.Name, Stats = st, Sheet = st.Sheet(cfg) };
            double ok = 0, nodes = 0, solved = 0, clean = 0, ticks = 0;
            int[] clocks = { 0, 101, 233 };
            for (int i = 0; i < seeds; i++)
            {
                LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 300));
                LevelData d = LevelBuilder.Build(run, s);
                Sim sim = new Sim(run, d, s);
                LevelBot probe = new LevelBot(sim, s, d, 1000);
                double okSum = 0; int okCount = 0, lastRow = -1;
                for (int pi = 1; pi < d.Platforms.Length; pi++)
                {
                    Platform p = d.Platforms[pi];
                    if (p.Row == lastRow || p.Row > d.Rows - 3) continue;
                    lastRow = p.Row;
                    foreach (int t0 in clocks)
                    {
                        SimState ss = sim.NewState();
                        ss.Tick = t0; ss.StartTick = t0;
                        ss.GroundId = pi; ss.Grounded = true; ss.Row = p.Row; ss.BestGroundId = pi;
                        ss.X = sim.PlatformX(pi, t0); ss.Y = p.Y; ss.MaxY = p.Y; ss.KillY = p.Y - Fx.FromInt(60);
                        int tried, prog;
                        probe.MeasureOptions(ss, out tried, out prog);
                        if (tried > 0) { okSum += prog / (double)tried; okCount++; }
                    }
                }
                ok += okSum / Math.Max(1, okCount);
                int budget = SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60);
                BotResult br = new LevelBot(sim, s, d, budget).Solve();
                nodes += br.Nodes; solved += br.Solved ? 1 : 0; ticks += br.Ticks;
                BotResult bc = new LevelBot(sim, s, d, budget) { MaxHits = 0 }.Solve();
                clean += bc.Solved ? 1 : 0;
            }
            r.Ok = ok / seeds * 100; r.Nodes = nodes / seeds; r.Solved = solved / seeds * 100; r.Clean = clean / seeds * 100; r.Ticks = ticks / seeds;
            return r;
        }

        /// <summary>One stat at a time across its legal range on Momo's baseline: which stat raises and which lowers the share of safe jumps on level 15.</summary>
        [Fact]
        public void SweepEachStatOnLevel15()
        {
            string root = ContentLoader.FindRoot();
            if (root == null || Environment.GetEnvironmentVariable("HERO_SWEEP") != "1") return;
            LoadedLevel l15 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 15);
            int seeds = int.Parse(Environment.GetEnvironmentVariable("HERO_SEEDS") ?? "12");
            var cases = new List<KeyValuePair<string, CharacterStats>>();
            cases.Add(new KeyValuePair<string, CharacterStats>("baseline", new CharacterStats()));
            foreach (int v in new[] { 17, 21, 23 }) cases.Add(new KeyValuePair<string, CharacterStats>("Jump launch " + v, new CharacterStats { JumpLaunch = v }));
            foreach (int v in new[] { 8, 10, 11, 12 }) cases.Add(new KeyValuePair<string, CharacterStats>("Speed " + v, new CharacterStats { MaxSpeed = v }));
            foreach (int v in new[] { 27, 35, 45 }) cases.Add(new KeyValuePair<string, CharacterStats>("Control " + v, new CharacterStats { Control = v }));
            foreach (int v in new[] { 69, 85, 100 }) cases.Add(new KeyValuePair<string, CharacterStats>("Wall share " + v, new CharacterStats { WallBoostHundredths = v }));
            var res = new Result[cases.Count];
            Parallel.For(0, cases.Count, i =>
            {
                var spec = new CharacterSpec { Id = "sweep", Name = cases[i].Key, Stats = cases[i].Value };
                res[i] = MeasureHero(spec, l15, seeds);
            });
            var md = new StringBuilder();
            md.AppendLine("| change (others at Momo) | stop dist | full-speed jump | ok % |");
            md.AppendLine("|---|---|---|---|");
            foreach (Result r in res) md.AppendLine("| " + r.Name + " | " + r.Sheet.StopDistance.ToString("F2") + " | " + r.Sheet.FullSpeedJump.ToString("F2") + " | " + r.Ok.ToString("F1") + " |");
            output.WriteLine(md.ToString());
        }

        /// <summary>Candidate Momo ladders (effective whole-number stats, jump launch / speed / control / wall share) measured on level 15. Set HERO_LADDER=1.</summary>
        [Fact]
        public void MeasureProposedLadders()
        {
            string root = ContentLoader.FindRoot();
            if (root == null || Environment.GetEnvironmentVariable("HERO_LADDER") != "1") return;
            LoadedLevel l15 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 15);
            int seeds = int.Parse(Environment.GetEnvironmentVariable("HERO_SEEDS") ?? "16");
            var cases = new List<KeyValuePair<string, CharacterStats>>();
            Action<string, int, int, int, int> add = (n, j, s, c, w) => cases.Add(new KeyValuePair<string, CharacterStats>(n, new CharacterStats { JumpLaunch = j, MaxSpeed = s, Control = c, WallBoostHundredths = w }));
            add("NOW Common", 19, 9, 30, 77);
            add("NOW Uncommon", 19, 11, 30, 74);
            add("NOW Rare", 19, 12, 35, 78);
            add("NOW Epic", 19, 12, 39, 80);
            add("NOW Legendary", 21, 12, 45, 87);
            add("A0 Common", 19, 9, 30, 77);
            add("A1 Uncommon", 20, 9, 29, 85);
            add("A2 Rare", 20, 10, 28, 92);
            add("A3 Epic", 21, 10, 28, 96);
            add("A4 Legendary", 21, 10, 27, 100);
            add("B1 Uncommon", 20, 9, 30, 85);
            add("B2 Rare", 21, 9, 30, 92);
            add("B3 Epic", 21, 10, 30, 96);
            add("B4 Legendary", 22, 10, 30, 100);
            var res = new Result[cases.Count];
            Parallel.For(0, cases.Count, i => { res[i] = MeasureHero(new CharacterSpec { Id = "ladder", Name = cases[i].Key, Stats = cases[i].Value }, l15, seeds); });
            var md = new StringBuilder();
            double baseOk = res[0].Ok;
            md.AppendLine("| step | j/s/c/w | Jump | Wall bounce | Air steer | Braking | Turn ground | Turn air | Time to stop | Stop dist | Time to top | Full launch | Half jump | Full jump | Sticky jump | Bounce launch | Bounce height | safe % | FI |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (Result r in res)
            {
                StatSheet s = r.Sheet;
                md.AppendLine("| " + r.Name + " | " + r.Stats.JumpLaunch + "/" + r.Stats.MaxSpeed + "/" + r.Stats.Control + "/" + r.Stats.WallBoostHundredths
                    + " | " + s.Jump.ToString("F1") + " | " + s.WallBounce.ToString("F1") + " | " + s.AirSteering.ToString("F1") + " | " + s.Braking.ToString("F1") + " | " + s.TurnAroundGround.ToString("F1") + " | " + s.TurnAroundAir.ToString("F1")
                    + " | " + s.TimeToStop.ToString("F2") + " | " + s.StopDistance.ToString("F2") + " | " + s.TimeToTopSpeed.ToString("F2") + " | " + s.FullSpeedLaunch.ToString("F1") + " | " + s.HalfSpeedJump.ToString("F2") + " | " + s.FullSpeedJump.ToString("F2")
                    + " | " + s.StickyJump.ToString("F2") + " | " + s.BounceLaunch.ToString("F1") + " | " + s.BounceHeight.ToString("F2") + " | " + r.Ok.ToString("F1") + " | " + (r.Ok / baseOk).ToString("F2") + " |");
            }
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
                File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "HERO_LADDER_REPORT.md"), md.ToString());
            output.WriteLine(md.ToString());
        }

        /// <summary>Ladder F (launch in tenths): measured on level 15 against Momo's baseline. Set HERO_LADDERF=1.</summary>
        [Fact]
        public void MeasureLadderF()
        {
            string root = ContentLoader.FindRoot();
            if (root == null || Environment.GetEnvironmentVariable("HERO_LADDERF") != "1") return;
            LoadedLevel l15 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 15);
            int seeds = int.Parse(Environment.GetEnvironmentVariable("HERO_SEEDS") ?? "24");
            // name, launch tenths, speed, control, wall, predicted FI
            var cases = new[]
            {
                new { N = "Momo default", T = 190, S = 9, C = 30, W = 77, P = 1.00 },
                new { N = "G Common", T = 190, S = 9, C = 30, W = 77, P = 1.00 },
                new { N = "G Uncommon", T = 209, S = 9, C = 34, W = 82, P = 1.00 },
                new { N = "G Rare", T = 216, S = 9, C = 38, W = 88, P = 1.03 },
                new { N = "G Elite", T = 219, S = 10, C = 41, W = 94, P = 1.00 },
                new { N = "H Legendary (22.8/11/41)", T = 228, S = 11, C = 41, W = 100, P = 1.06 },
                new { N = "H Legendary alt (22.8/10/42)", T = 228, S = 10, C = 42, W = 100, P = 1.08 },
            };
            var res = new Result[cases.Length];
            Parallel.For(0, cases.Length, i =>
            {
                var c = cases[i];
                var st = new CharacterStats { JumpLaunch = 19, MaxSpeed = c.S, Control = c.C, WallBoostHundredths = c.W };
                res[i] = MeasureHero(new CharacterSpec { Id = "f", Name = c.N, Stats = st }, l15, seeds, c.T);
            });
            double baseOk = res[0].Ok;
            var md = new StringBuilder();
            md.AppendLine("| step | launch/speed/control/wall | safe % | measured FI | predicted FI | solved % | clean % | nodes |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < cases.Length; i++)
                md.AppendLine("| " + cases[i].N + " | " + (cases[i].T / 10.0).ToString("F1") + "/" + cases[i].S + "/" + cases[i].C + "/" + cases[i].W + " | " + res[i].Ok.ToString("F1") + " | " + (res[i].Ok / baseOk).ToString("F3")
                    + " | " + cases[i].P.ToString("F2") + " | " + res[i].Solved.ToString("F0") + " | " + res[i].Clean.ToString("F0") + " | " + res[i].Nodes.ToString("F0") + " |");
            output.WriteLine(md.ToString());
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
                File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "HERO_LADDER_H_REPORT.md"), md.ToString());
        }

        [Fact]
        public void MeasureLevel15PerHeroTier()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            LoadedLevel l15 = ContentLoader.LoadRegions(root, 1, 1).First(l => l.Spec.LevelIndex == 15);
            int seeds = int.Parse(Environment.GetEnvironmentVariable("HERO_SEEDS") ?? "8");
            string[] ids = Heroes();
            Result[] res = new Result[ids.Length];
            Parallel.For(0, ids.Length, i => { res[i] = MeasureHero(CharacterDefs.GetIncludingInactive(ids[i]), l15, seeds); });

            var md = new StringBuilder();
            md.AppendLine("# Hero tier measurement, Region 1 level 15 (" + seeds + " random attempt layouts per hero)");
            md.AppendLine();
            md.AppendLine("Played with each hero's effective (capped) stats. ok% = share of jump options that land on a higher row (lower = harder); solved% / clean% = bot wins within the node budget, clean = with zero gong hits; nodes = bot effort (higher = harder).");
            md.AppendLine();
            md.AppendLine("| hero | jump | speed | control | wall | stop dist | full-speed jump | ok % | solved % | clean % | nodes |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (Result r in res)
                md.AppendLine("| " + r.Name + " | " + r.Stats.JumpLaunch + " | " + r.Stats.MaxSpeed + " | " + r.Stats.Control + " | " + r.Stats.WallBoostHundredths + " | " + r.Sheet.StopDistance.ToString("F2") + " | " + r.Sheet.FullSpeedJump.ToString("F2")
                    + " | " + r.Ok.ToString("F1") + " | " + r.Solved.ToString("F0") + " | " + r.Clean.ToString("F0") + " | " + r.Nodes.ToString("F0") + " |");
            output.WriteLine(md.ToString());
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
                File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "HERO_TIER_REPORT.md"), md.ToString());
        }
    }
}
