using System.Collections.Generic;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Placement rules of the region special platforms (doc 24: the seven region 1 platforms; RegionPlatformSets lists the set of each region, so
    /// regions 2 to 5 only add their kinds there). Shared by LevelBuilder (exact counts per level, planned rows) and EndlessTower (per-row draws).
    /// Rules, all for fairness (CUX-7: hazards telegraph, nothing unavoidable, always beatable):
    ///   levels 1: none; level 2: a light touch (SimConfig.Level2SafeSpecials non-hazard + Level2HazardSpecials hazard-type, no launchers);
    ///   level 3 to 15: SimConfig.SpecialShareLevel3Percent rising linearly to SpecialShareLevel15Percent of the free rows carry one;
    ///   hazard-type kinds (Thorn Bush, Snap Daisy) never sit in adjacent rows, never follow a launcher, are at least HazardEarlyRowsPerHazard rows
    ///   apart up to level HazardEarlyLastLevel; no launcher (flower, cap, dandelion) follows a launcher; the last three rows before the goal and the
    ///   two rows either side of the hidden gem avoid launchers and hazards; rows of a combo ladder, a chunk or the gem row stay plain, and rows beside a ladder or chunk take no hive or launcher.
    /// </summary>
    public static class SpecialPlatforms
    {
        /// <summary>Percent of the free rows that carry a special platform at a campaign level (0 below level 2).</summary>
        public static int SharePercent(SimConfig cfg, int levelIndex)
        {
            if (levelIndex < 3) return 0;
            int idx = levelIndex > 15 ? 15 : levelIndex;
            return cfg.SpecialShareLevel3Percent + (cfg.SpecialShareLevel15Percent - cfg.SpecialShareLevel3Percent) * (idx - 3) / 12;
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
            if (restricted && (hazard || launch)) return false;
            if (hazard && (rowsSinceHazard < spacing || PlatformKinds.IsLaunch(prev) || PlatformKinds.IsHazardType(prev))) return false;
            if (launch && PlatformKinds.IsLaunch(prev)) return false;
            return true;
        }

        private static bool Contains(PlatformKind[] set, PlatformKind k)
        {
            for (int i = 0; i < set.Length; i++) if (set[i] == k) return true;
            return false;
        }

        /// <summary>The kinds a row may be drawn from at a level: the region's new set plus the signature platform from level 3.</summary>
        public static PlatformKind[] Options(LevelSpec spec, PlatformKind sigKind)
        {
            PlatformKind[] set = spec.ActiveNewPlatforms();
            List<PlatformKind> list = new List<PlatformKind>(set);
            if (sigKind != PlatformKind.Normal && spec.LevelIndex >= 3 && !Contains(set, sigKind)) list.Add(sigKind);
            return list.ToArray();
        }

        /// <summary>
        /// Plans the special platform of every generated row of a campaign level (index = row, Normal = none). The number of rows is exact
        /// for the level (it depends on the level only), so every attempt seed of a level has the same count; the rows and kinds are seeded.
        /// <paramref name="locked"/> marks rows owned by a combo ladder or an authored chunk.
        /// </summary>
        public static PlatformKind[] Plan(SimConfig cfg, LevelSpec spec, PlatformKind sigKind, int rows, bool[] locked, int gemRow)
        {
            PlatformKind[] plan = new PlatformKind[rows + 3];
            PlatformKind[] set = spec.ActiveNewPlatforms();
            int idx = spec.LevelIndex;
            if (set.Length == 0 || idx < 2) return plan;

            Rng pr = new Rng(spec.Seed ^ 0x1B873593);
            PlatformKind[] options = Options(spec, sigKind);
            int first = idx == 2 ? 5 : 4;
            List<int> cand = new List<int>(rows);
            for (int r = first; r <= rows; r++)
            {
                if (locked[r] || r == gemRow) continue;
                cand.Add(r);
            }
            int want;
            int hazardRows = 0;
            if (idx == 2) { want = cfg.Level2SafeSpecials + cfg.Level2HazardSpecials; hazardRows = cfg.Level2HazardSpecials; }
            else want = (cand.Count * SharePercent(cfg, idx) + 50) / 100;
            if (want > cand.Count) want = cand.Count;
            for (int i = cand.Count - 1; i > 0; i--)
            {
                int j = pr.Next(i + 1);
                int t = cand[i]; cand[i] = cand[j]; cand[j] = t;
            }
            List<int> chosen = new List<int>(cand.GetRange(0, want));
            chosen.Sort();

            int spacing = HazardSpacing(cfg, idx);
            int[] used = new int[options.Length];
            // Hazard-type and launcher kinds each get their share of the options (2 of 8 in region 1 = a quarter of the special rows), never more:
            // otherwise the "draw the least used kind" rule would hand them every row the adjacency rules let through.
            int hazardKinds = 0, launchKinds = 0;
            for (int o = 0; o < options.Length; o++) { if (PlatformKinds.IsHazardType(options[o])) hazardKinds++; if (PlatformKinds.IsLaunch(options[o])) launchKinds++; }
            int hazardBudget = idx == 2 ? 1000 : (want * hazardKinds + options.Length / 2) / options.Length;
            int launchBudget = (want * launchKinds + options.Length / 2) / options.Length;
            int hazardsPlaced = 0, launchesPlaced = 0;
            int lastHazardRow = -1000;
            PlatformKind prevKind = PlatformKind.Normal;
            int prevRow = -10;
            // Level 2: exactly the hazard rows are marked up front (a random chosen row away from the end/gem zone), the rest take safe kinds only.
            bool[] hazardPick = new bool[rows + 3];
            if (idx == 2)
            {
                List<int> ok = new List<int>();
                foreach (int r in chosen) if (!Restricted(r, rows, gemRow)) ok.Add(r);
                for (int h = 0; h < hazardRows && ok.Count > 0; h++)
                {
                    int pick = pr.Next(ok.Count);
                    hazardPick[ok[pick]] = true;
                    ok.RemoveAt(pick);
                }
            }
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
                    if (beside && (k == PlatformKind.HiveLift || PlatformKinds.IsLaunch(k))) continue;
                    if (idx == 2)
                    {
                        if (PlatformKinds.IsLaunch(k)) continue;                       // level 2: no launchers
                        if (PlatformKinds.IsHazardType(k) != hazardPick[r]) continue;   // marked rows take a hazard-type kind, the others a safe one
                    }
                    if (PlatformKinds.IsHazardType(k) && hazardsPlaced >= hazardBudget) continue;
                    if (PlatformKinds.IsLaunch(k) && launchesPlaced >= launchBudget) continue;
                    if (!Allowed(k, lower, r - lastHazardRow, spacing, restricted)) continue;
                    pool.Add(o);
                }
                if (pool.Count == 0)
                {
                    // Fall back to a kind that is always allowed (never a launcher or hazard): the first safe option.
                    for (int o = 0; o < options.Length; o++)
                        if (!PlatformKinds.IsHazardType(options[o]) && !PlatformKinds.IsLaunch(options[o]) && !(beside && options[o] == PlatformKind.HiveLift)) { pool.Add(o); break; }
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
                default: return w;
            }
        }

        /// <summary>
        /// Vertical gap rule around a Bee Hive Lift: the gap to and from a hive stays in [HiveGapMin, NormalGapMax - bob amplitude], so the
        /// bobbing hive never touches its neighbours and every bob height stays within a jump. Other gaps are returned unchanged.
        /// </summary>
        public static Fx ConstrainGap(SimConfig cfg, PlatformKind kind, PlatformKind prevKind, Fx gap)
        {
            if (kind != PlatformKind.HiveLift && prevKind != PlatformKind.HiveLift) return gap;
            return Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax - cfg.HiveBobAmplitude);
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
            }
        }
    }
}
