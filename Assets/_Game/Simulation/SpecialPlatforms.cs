using System.Collections.Generic;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Placement rules of the region special platforms (doc 24: the seven region 1 platforms; RegionPlatformSets lists the set of each region, so
    /// regions 2 to 5 only add their kinds there). Shared by LevelBuilder (exact counts per level, planned rows) and EndlessTower (per-row draws).
    /// Rules (doc 25): the share of rows with a special platform ramps inside each level (SimConfig.RampLowPercent to RampHighPercent), for all
    /// regions; hazard-type kinds (Thorn Bush, Snap Daisy) never sit in adjacent rows, never follow a launcher, are at least
    /// HazardEarlyRowsPerHazard rows apart up to level HazardEarlyLastLevel and are left out of level 1; no launcher follows a launcher; the last three
    /// rows before the goal and the two rows either side of the hidden gem avoid launchers, hazards and vanishing platforms; rows beside a ladder take no
    /// hive, sticky or launcher.
    /// </summary>
    public static class SpecialPlatforms
    {
        /// <summary>Percent of rows that carry a special platform at <paramref name="row"/> of a campaign level: ramps from RampLowPercent at the bottom to RampHighPercent at the top (doc 25).</summary>
        public static int RowPercent(SimConfig cfg, int levelIndex, int row, int rows)
        {
            int idx = levelIndex < 1 ? 1 : (levelIndex > 15 ? 15 : levelIndex);
            int lo = cfg.RampLowPercent[idx], hi = cfg.RampHighPercent[idx];
            if (rows <= 1) return hi;
            int r = row < 1 ? 0 : (row > rows ? rows - 1 : row - 1);
            return lo + (hi - lo) * r / (rows - 1);
        }

        /// <summary>Overall share (percent) a level aims at: the mean of its ramp (reports and tests).</summary>
        public static int SharePercent(SimConfig cfg, int levelIndex)
        {
            int idx = levelIndex < 1 ? 1 : (levelIndex > 15 ? 15 : levelIndex);
            return (cfg.RampLowPercent[idx] + cfg.RampHighPercent[idx]) / 2;
        }

        /// <summary>Endless: share after <paramref name="floorsPastFixed"/> rows; rises from the level 3 share to the level 15 share over EndlessSpecialRampFloors.</summary>
        public static int EndlessSharePercent(SimConfig cfg, int floorsPastFixed)
        {
            int f = floorsPastFixed < 0 ? 0 : (floorsPastFixed > cfg.EndlessSpecialRampFloors ? cfg.EndlessSpecialRampFloors : floorsPastFixed);
            return cfg.SpecialShareLevel3Percent + (cfg.SpecialShareLevel15Percent - cfg.SpecialShareLevel3Percent) * f / cfg.EndlessSpecialRampFloors;
        }

        /// <summary>Minimum row distance between two hazard-type specials at a level (2 = just not adjacent).</summary>
        public static int HazardSpacing(SimConfig cfg, int levelIndex)
        {
            return levelIndex <= cfg.HazardEarlyLastLevel ? cfg.HazardEarlyRowsPerHazard : 2;
        }

        /// <summary>
        /// May kind <paramref name="k"/> sit in a row whose lower neighbour carries <paramref name="prev"/>, with the last hazard-type row
        /// <paramref name="rowsSinceHazard"/> rows below (a large number when none)? <paramref name="restricted"/>: no launcher or hazard here.
        /// </summary>
        public static bool Allowed(PlatformKind k, PlatformKind prev, int rowsSinceHazard, int spacing, bool restricted)
        {
            bool hazard = PlatformKinds.IsHazardType(k);
            bool launch = PlatformKinds.IsLaunch(k);
            bool demanding = PlatformKinds.IsDemanding(k);
            if (restricted && (hazard || launch || demanding)) return false;
            if (hazard && (rowsSinceHazard < spacing || PlatformKinds.IsLaunch(prev) || PlatformKinds.IsHazardType(prev) || PlatformKinds.IsDemanding(prev))) return false;
            if (launch && PlatformKinds.IsLaunch(prev)) return false;
            // doc 30 section 8: a demanding kind (Fairy Ring, Orbit Cap) never touches another one, a hazard or a launch flight
            if (demanding && (PlatformKinds.IsDemanding(prev) || PlatformKinds.IsHazardType(prev) || PlatformKinds.IsLaunch(prev))) return false;
            return true;
        }

        private static bool Contains(PlatformKind[] set, PlatformKind k)
        {
            for (int i = 0; i < set.Length; i++) if (set[i] == k) return true;
            return false;
        }

        /// <summary>
        /// The generic special kinds the regions without their own new kinds use (doc 25): the signature platform plus these. Bounce is a launcher,
        /// Crumble / Vanishing / Moving are timing platforms, Sticky and Slippery change the footing.
        /// </summary>
        private static readonly PlatformKind[] GenericSet =
        {
            PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing, PlatformKind.Slippery, PlatformKind.Sticky, PlatformKind.Bounce
        };

        /// <summary>
        /// The kinds a special row may be drawn from: the region's new set plus its signature platform (region 1), or the signature platform
        /// (listed twice, so it is the most common) plus the generic kinds (regions 2 to 5). Hazard types are left out of level 1 (the tutorial).
        /// </summary>
        public static PlatformKind[] Options(LevelSpec spec, PlatformKind sigKind)
        {
            PlatformKind[] set = spec.ActiveNewPlatforms();
            List<PlatformKind> list = new List<PlatformKind>();
            if (set.Length > 0)
            {
                list.AddRange(set);
                if (sigKind != PlatformKind.Normal && !Contains(set, sigKind)) list.Add(sigKind);
                // doc 30 section 7: a region may keep one or two generic kinds next to its new set (region 2: Crumble and Slippery)
                PlatformKind[] generics = RegionPlatformSets.GenericsFor(spec.Signature);
                for (int i = 0; i < generics.Length; i++) if (!list.Contains(generics[i])) list.Add(generics[i]);
                // the signature platform of a region may stay its most common kind: listed once more (region 3: drifting platforms)
                if (RegionPlatformSets.SignatureListedTwice(spec.Signature) && sigKind != PlatformKind.Normal) list.Add(sigKind);
            }
            else
            {
                if (sigKind != PlatformKind.Normal) { list.Add(sigKind); list.Add(sigKind); }
                for (int i = 0; i < GenericSet.Length; i++) if (GenericSet[i] != sigKind) list.Add(GenericSet[i]);
            }
            if (spec.LevelIndex <= 1) list.RemoveAll(k => PlatformKinds.IsHazardType(k));
            // doc 30 section 4: the Surf Boost Wave can carry a landing player into a wall, so a no-wall-bounce level never has one
            if (spec.Goal != null && spec.Goal.Type == GoalType.NoWallBounce) list.RemoveAll(k => k == PlatformKind.SurfBoostWave);
            return list.ToArray();
        }

        /// <summary>
        /// Plans the special platform of every generated row of a campaign level (index = row, Normal = none). The number of special rows is
        /// the exact sum of the per-row ramp (RowPercent) and depends on the level only, so every attempt seed of a level has the same count;
        /// the rows (drawn without repeats, weighted by the ramp, so the top of the level is denser) and the kinds are seeded.
        /// <paramref name="locked"/> marks rows owned by a combo ladder or an authored chunk.
        /// </summary>
        public static PlatformKind[] Plan(SimConfig cfg, LevelSpec spec, PlatformKind sigKind, int rows, bool[] locked, int gemRow)
        {
            PlatformKind[] plan = new PlatformKind[rows + 3];
            PlatformKind[] options = Options(spec, sigKind);
            int idx = spec.LevelIndex;
            if (options.Length == 0 || spec.Endless) return plan;

            Rng pr = new Rng(spec.Seed ^ 0x1B873593);
            int first = idx <= 1 ? 3 : 4;
            List<int> cand = new List<int>(rows);
            List<int> weight = new List<int>(rows);
            int sumPercent = 0;
            for (int r = first; r <= rows; r++)
            {
                if (locked[r] || r == gemRow) continue;
                int p = RowPercent(cfg, idx, r, rows);
                cand.Add(r);
                weight.Add(p < 1 ? 1 : p);
                sumPercent += p;
            }
            int want = (sumPercent + 50) / 100;
            if (want > cand.Count) want = cand.Count;
            // weighted draw without repeats (integer arithmetic only)
            List<int> chosen = new List<int>(want);
            int total = 0;
            for (int i = 0; i < weight.Count; i++) total += weight[i];
            for (int n = 0; n < want && total > 0; n++)
            {
                int roll = pr.Next(total), acc = 0, pick = 0;
                for (int i = 0; i < cand.Count; i++)
                {
                    acc += weight[i];
                    if (roll < acc) { pick = i; break; }
                }
                chosen.Add(cand[pick]);
                total -= weight[pick];
                cand.RemoveAt(pick);
                weight.RemoveAt(pick);
            }
            chosen.Sort();

            int spacing = HazardSpacing(cfg, idx);
            int[] used = new int[options.Length];
            // Hazard-type and launcher kinds each get their share of the options, never more: otherwise the "draw the least used kind" rule
            // would hand them every row the adjacency rules let through.
            int hazardKinds = 0, launchKinds = 0;
            for (int o = 0; o < options.Length; o++) { if (PlatformKinds.IsHazardType(options[o])) hazardKinds++; if (PlatformKinds.IsLaunch(options[o])) launchKinds++; }
            int hazardBudget = (chosen.Count * hazardKinds + options.Length / 2) / options.Length;
            int launchBudget = (chosen.Count * launchKinds + options.Length / 2) / options.Length;
            int hazardsPlaced = 0, launchesPlaced = 0;
            int lastHazardRow = -1000;
            PlatformKind prevKind = PlatformKind.Normal;
            int prevRow = -10;
            List<int> pool = new List<int>(options.Length);
            for (int ci = 0; ci < chosen.Count; ci++)
            {
                int r = chosen[ci];
                PlatformKind lower = prevRow == r - 1 ? prevKind : PlatformKind.Normal;
                bool restricted = Restricted(r, rows, gemRow);
                bool beside = locked[r - 1] || locked[r + 1];   // next to a ladder or chunk: only kinds without gap or position rules
                pool.Clear();
                for (int o = 0; o < options.Length; o++)
                {
                    PlatformKind k = options[o];
                    if (beside && (k == PlatformKind.HiveLift || k == PlatformKind.Sticky || k == PlatformKind.SproutLift || k == PlatformKind.OrbitCap || k == PlatformKind.SinkingRaft || k == PlatformKind.SandcastleStep || PlatformKinds.IsLaunch(k))) continue;
                    if (restricted && k == PlatformKind.Vanishing) continue;   // the finish never blinks
                    if (PlatformKinds.IsHazardType(k) && hazardsPlaced >= hazardBudget) continue;
                    if (PlatformKinds.IsLaunch(k) && launchesPlaced >= launchBudget) continue;
                    if (!Allowed(k, lower, r - lastHazardRow, spacing, restricted)) continue;
                    pool.Add(o);
                }
                if (pool.Count == 0)
                {
                    // Fall back to a kind that is always allowed (no launcher, hazard, hive, sticky or vanishing): the first such option.
                    for (int o = 0; o < options.Length; o++)
                    {
                        PlatformKind k = options[o];
                        if (PlatformKinds.IsHazardType(k) || PlatformKinds.IsLaunch(k) || PlatformKinds.IsDemanding(k) || k == PlatformKind.HiveLift || k == PlatformKind.Sticky || k == PlatformKind.Vanishing
                            || k == PlatformKind.SproutLift || k == PlatformKind.SinkingRaft || k == PlatformKind.SandcastleStep || k == PlatformKind.SurfBoostWave) continue;
                        pool.Add(o);
                        break;
                    }
                    if (pool.Count == 0) continue;
                }
                // Even mix: draw among the allowed kinds that were used least so far.
                int minUsed = int.MaxValue;
                for (int p = 0; p < pool.Count; p++) if (used[pool[p]] < minUsed) minUsed = used[pool[p]];
                int n = 0;
                for (int p = 0; p < pool.Count; p++) if (used[pool[p]] <= minUsed + 1) pool[n++] = pool[p];
                int o2 = pool[pr.Next(n)];
                used[o2]++;
                plan[r] = options[o2];
                prevKind = options[o2];
                prevRow = r;
                if (PlatformKinds.IsHazardType(options[o2])) { lastHazardRow = r; hazardsPlaced++; }
                if (PlatformKinds.IsLaunch(options[o2])) launchesPlaced++;
            }
            return plan;
        }

        /// <summary>The last three rows before the goal and the two rows either side of the hidden gem avoid launchers and hazards.</summary>
        public static bool Restricted(int row, int rows, int gemRow)
        {
            return row > rows - 3 || (gemRow > 0 && row >= gemRow - 2 && row <= gemRow + 2);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Geometry rules shared by the campaign builder and the endless tower
        // ------------------------------------------------------------------------------------------------------------------

        /// <summary>Per-kind width limits (u): wind petals and seesaws need room, thorn bushes need a fair safe half, ladybugs and dandelions stay compact.</summary>
        public static Fx ConstrainWidth(SimConfig cfg, PlatformKind kind, Fx w)
        {
            switch (kind)
            {
                case PlatformKind.WindPetal: return Fx.Max(w, cfg.WindMinWidth);
                case PlatformKind.ThornBush: return Fx.Max(w, cfg.ThornMinWidth);
                case PlatformKind.Seesaw: return Fx.Clamp(w, cfg.SeesawMinWidth, Fx.FromInt(6));
                case PlatformKind.Ladybug: return Fx.Clamp(w, Fx.FromInt(2), cfg.LadybugMaxWidth);
                case PlatformKind.Dandelion: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.HiveLift: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.SnapDaisy: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.SproutLift: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.FairyRing: return cfg.FairyRingWidth;   // two halves of at least 1.4 u around the hole
                case PlatformKind.OrbitCap: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.DizzySpores: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.SporePop: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(6));   // painted in 2, 3, 4 and 6 u
                case PlatformKind.SurfBoostWave: return Fx.Clamp(w, cfg.SurfMinWidth, Fx.FromInt(6));            // room to build speed: 4 or 6 u
                case PlatformKind.SandcastleStep: return Fx.Clamp(w, cfg.SandcastleMinWidth, Fx.FromInt(6));    // two halves of at least 2 u
                case PlatformKind.ClamShellPad: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.SinkingRaft: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.TurtleShuttle: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.KelpTangle: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.PufferfishPad: return Fx.Clamp(w, cfg.PufferMinWidth, Fx.FromInt(6));         // room to stay clear of the centre while puffed
                default: return w;
            }
        }

        /// <summary>
        /// Vertical gap rule around a Bee Hive Lift: the gap to and from a hive stays in [HiveGapMin, NormalGapMax - bob amplitude], so the
        /// bobbing hive never touches its neighbours and every bob height stays within a jump. Other gaps are returned unchanged.
        /// </summary>
        public static Fx ConstrainGap(SimConfig cfg, PlatformKind kind, PlatformKind prevKind, Fx gap)
        {
            if (kind == PlatformKind.HiveLift || prevKind == PlatformKind.HiveLift) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax - cfg.HiveBobAmplitude);
            // Sprout Lift (doc 30): it rises SproutRise once stood on, so the row above stays at least HiveGapMin away (1.0 u of air when risen); the climb into it is a normal jump.
            if (kind == PlatformKind.SproutLift || prevKind == PlatformKind.SproutLift) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax);
            // Orbit Cap: its height moves by +-OrbitAmplitudeY, so both neighbours keep OrbitGapMin and every height stays within a jump.
            if (kind == PlatformKind.OrbitCap || prevKind == PlatformKind.OrbitCap) gap = Fx.Clamp(gap, cfg.OrbitGapMin, cfg.NormalGapMax - cfg.OrbitAmplitudeY);
            // Sinking Raft (doc 30): it sinks RaftSinkDepth once stood on, so both neighbours keep HiveGapMin and every sunk height stays within a jump.
            if (kind == PlatformKind.SinkingRaft || prevKind == PlatformKind.SinkingRaft) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax - cfg.RaftSinkDepth);
            // Sandcastle Step: the climb onto the raised half is a normal jump 0.9 u shorter, so the gap below it stays within NormalGapMax - SandcastleRise.
            if (kind == PlatformKind.SandcastleStep) gap = Fx.Min(gap, cfg.NormalGapMax - cfg.SandcastleRise);
            return gap;
        }

        /// <summary>The x of the platform after a dandelion: in the direction of its arrow, DandelionNextDxMin to Max away, kept inside the walls.</summary>
        public static Fx DandelionNextX(SimConfig cfg, ref Rng rng, Fx prevX, int prevDir, Fx w, Fx screen)
        {
            Fx dx = rng.Range(cfg.DandelionNextDxMin, cfg.DandelionNextDxMax);
            Fx x = prevX + dx * prevDir;
            return Fx.Clamp(x, w / 2, screen - w / 2);
        }

        /// <summary>
        /// Draws the per-platform parameters of a region 1 special platform. <paramref name="room"/> is the free space either side of x inside
        /// the walls (limits the Ladybug path). Dandelions point toward the middle of the screen (always room for the next platform).
        /// </summary>
        public static void Params(SimConfig cfg, ref Rng rng, PlatformKind kind, Fx x, Fx room, Fx screen, out Fx amp, out int period, out int phase, out int dir)
        {
            amp = Fx.Zero; period = 0; phase = 0; dir = 0;
            switch (kind)
            {
                case PlatformKind.WindPetal:
                    dir = rng.Next(2) == 0 ? -1 : 1;
                    phase = rng.Next(cfg.WindFlipTicks * 2);
                    break;
                case PlatformKind.HiveLift:
                    period = cfg.HivePeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.Ladybug:
                    amp = Fx.Min(rng.Range(cfg.LadybugAmplitudeMin, cfg.LadybugAmplitudeMax), room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = 2 * rng.Range(100, 150);
                    phase = rng.Next(period);
                    break;
                case PlatformKind.ThornBush:
                    dir = rng.Next(2) == 0 ? -1 : 1;
                    break;
                case PlatformKind.Dandelion:
                    dir = x < screen / 2 ? 1 : -1;
                    break;
                case PlatformKind.SnapDaisy:
                    phase = rng.Next(cfg.SnapCloseTicks);   // only a visual offset of the idle animation
                    break;
                case PlatformKind.FairyRing:
                    amp = rng.Range(cfg.FairyHoleMin, cfg.FairyHoleMax);   // half width of the hole
                    break;
                case PlatformKind.OrbitCap:
                    amp = Fx.Min(rng.Range(cfg.OrbitAmplitudeMin, cfg.OrbitAmplitudeMax), room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.OrbitPeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.SporePop:
                    dir = x < screen / 2 ? 1 : -1;   // the fling points toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.SurfBoostWave:
                    dir = x < screen / 2 ? 1 : -1;   // the wave carries toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.SandcastleStep:
                    dir = rng.Next(2) == 0 ? -1 : 1;   // side of the raised half
                    amp = cfg.SandcastleRise;
                    break;
                case PlatformKind.ClamShellPad:
                    phase = rng.Next(cfg.ClamCycleTicks);
                    break;
                case PlatformKind.TurtleShuttle:
                    amp = Fx.Min(cfg.TurtleAmplitude, room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.TurtlePeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.PufferfishPad:
                    phase = rng.Next(cfg.PufferCycleTicks);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Integer sine table (Orbit Cap): 64 steps per lap, linear interpolation, no System.Math, identical on every device
        // ------------------------------------------------------------------------------------------------------------------

        /// <summary>sin(k * pi / 32) in Q16.16 for k = 0 to 16 (one quarter of a lap in 16 steps).</summary>
        private static readonly int[] QuarterSin = { 0, 6424, 12785, 19024, 25080, 30893, 36410, 41576, 46341, 50660, 54491, 57798, 60547, 62714, 64277, 65220, 65536 };

        private static int LapSinRaw(int step)
        {
            int idx = step & 63;
            int q = idx >> 4, r = idx & 15;
            switch (q)
            {
                case 0: return QuarterSin[r];
                case 1: return QuarterSin[16 - r];
                case 2: return -QuarterSin[r];
                default: return -QuarterSin[16 - r];
            }
        }

        private static Fx LapSin(int phase, int period, int quarterOffsetSteps)
        {
            long pos = (long)phase * 64 * 256 / period;   // position in 1/256 steps
            int step = (int)(pos >> 8) + quarterOffsetSteps;
            int frac = (int)(pos & 255);
            int a = LapSinRaw(step), b = LapSinRaw(step + 1);
            return Fx.FromRaw(a + (((b - a) * frac) >> 8));
        }

        /// <summary>sin of the lap position phase / period (phase in 0 to period - 1), -1 to 1.</summary>
        public static Fx Sin(int phase, int period) { return LapSin(phase, period, 0); }

        /// <summary>cos of the lap position phase / period, -1 to 1.</summary>
        public static Fx Cos(int phase, int period) { return LapSin(phase, period, 16); }
    }
}
