using System.Collections.Generic;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Endless mode world (owner decision 2026-10-20): a tower of one region's look and platform rules that never ends.
    /// Rows are generated one after the other from the seed, so the tower is PREFIX STABLE: generating more rows never changes
    /// the rows that exist already (the Sim keeps platform ids, and a run can be replayed from the seed alone).
    /// The tower grows in chunks of ChunkRows rows; after each growth <see cref="Data"/> is a fresh snapshot (new arrays), and the
    /// caller builds a new Sim from it and calls <see cref="Sim.GrowState"/>. Growth happens once per chunk, never per frame.
    /// Same row geometry and reach rules as LevelBuilder (generated rows only): no goal, no stars, no chunks, no pickups.
    ///
    /// ENDLESS CYCLE (owner 2026-11-05, doc 24 section "Endless cycle"): the difficulty is stepped by <see cref="LevelForFloor"/>, ONE pure function floor ->
    /// level 1..15. Floors 1-1500 are levels 1..15 of 100 floors each; floors 1501-2500 restart at level 9 (9..14 for 100 floors each, then level 15 held
    /// for 400 floors) and every 1000 floors repeat that, without end. A floor at level N uses EVERYTHING the region's campaign level N uses: layout
    /// difficulty d (LevelSpec.DifficultyForLevel: content difficulty, region offset and the level 3..15 ramp floor), special-platform share
    /// (SpecialPlatforms.RowPercent), hazard spacing, the kind list (SpecialPlatforms.Options), gong density and the scroll speed (Sim). Floor n is row n.
    /// </summary>
    public sealed class EndlessTower
    {
        /// <summary>Rows added per growth step.</summary>
        public const int ChunkRows = 40;
        /// <summary>The tower is kept this many rows above the player.</summary>
        public const int LookaheadRows = 60;
        /// <summary>
        /// Hard cap of the PLAYABLE tower: Q16.16 holds about 32000 u, a row is at most 4.2 u, so rows past 7000 would overflow the Sim (about two hours of
        /// perfect play). The generator itself has no limit: in <see cref="Stream"/> mode it re-bases its coordinates and reaches floor 100000 and beyond.
        /// </summary>
        public const int MaxRows = 7000;

        // ---- the endless cycle (owner 2026-11-05) ----
        /// <summary>Floors per difficulty step.</summary>
        public const int FloorsPerLevel = 100;
        /// <summary>The first climb: floors 1 to 1500 are levels 1 to 15.</summary>
        public const int FirstCycleFloors = 1500;
        /// <summary>Every later cycle is 1000 floors long: levels 9..14 for 100 floors each, then level 15 for 400 floors.</summary>
        public const int CycleFloors = 1000;
        /// <summary>The level a cycle restarts at.</summary>
        public const int ResetLevel = 9;
        /// <summary>The highest level (campaign level 15).</summary>
        public const int MaxLevel = 15;
        /// <summary>The owner's planned end of the scripted cycle; the rule simply goes on repeating beyond it.</summary>
        public const int PlannedLastFloor = 100000;

        /// <summary>
        /// The campaign level (1..15) whose difficulty the endless floor <paramref name="floor"/> (= row, 1-based; the start platform is floor 0) uses.
        /// Pure, allocation free, defined for every int.
        /// </summary>
        public static int LevelForFloor(int floor)
        {
            if (floor <= 1) return 1;
            if (floor <= FirstCycleFloors) return (floor + FloorsPerLevel - 1) / FloorsPerLevel;
            int block = ((floor - FirstCycleFloors - 1) % CycleFloors) / FloorsPerLevel;
            int level = ResetLevel + block;
            return level > MaxLevel ? MaxLevel : level;
        }

        /// <summary>Position of a floor inside its 100-floor level step, 1..100 (floor 0 counts as 1).</summary>
        public static int PositionInLevel(int floor)
        {
            return floor <= 1 ? 1 : (floor - 1) % FloorsPerLevel + 1;
        }

        /// <summary>Recovers the region offset from the base difficulty of an endless run (spec.Difficulty = EffectiveDifficulty(StartBase, offset)).</summary>
        public static Fx OffsetFromStartDifficulty(Fx difficulty)
        {
            Fx start = StartBase;
            if (difficulty <= start) return Fx.Zero;
            Fx off = (difficulty - start) / (Fx.One - start);
            return off > Fx.One ? Fx.One : off;
        }

        /// <summary>Content difficulty of campaign level 1 (0.05): the base the endless run starts from.</summary>
        public static readonly Fx StartBase = Fx.FromRatio(5, 100);

        private readonly SimConfig cfg;
        private readonly LevelSpec spec;
        private Rng rng;
        private Rng hazRng;
        private readonly List<Platform> plats;
        private readonly List<Hazard> hazards = new List<Hazard>();

        private int row;                 // last generated row
        private Fx prevX, prevW, prevY;  // spine platform of the last row
        private PlatformKind prevKind;
        private int pattern, patternLeft;
        private readonly SignatureKind signature;
        private readonly PlatformKind sigKind;
        // Region special platforms (doc 24): own Rng stream, so the base geometry of the tower is independent of them; prefix stable like the rest.
        private Rng specRng;
        /// <summary>Per campaign level 1..15: the layout difficulty (index 0 unused).</summary>
        private readonly Fx[] levelD = new Fx[MaxLevel + 1];
        /// <summary>Per campaign level: the kinds a special row may draw (the region's level-N list).</summary>
        private readonly PlatformKind[][] levelOptions = new PlatformKind[MaxLevel + 1][];
        private int lastHazardRow = -1000;
        private int prevDir;
        // scratch buffers (no allocation per row)
        private readonly int[] pool = new int[32];
        private readonly Fx[] usedX = new Fx[4];
        private readonly Fx[] usedW = new Fx[4];

        // ---- stream mode (generation beyond the Q16.16 range) ----
        /// <summary>
        /// Generator-only mode for towers beyond <see cref="MaxRows"/>: when the stored heights pass <see cref="RebaseAtYRaw"/> the oldest rows are dropped
        /// and every height is shifted down (the dropped amount is kept in <see cref="OriginYRaw"/>), so memory stays small and no coordinate overflows.
        /// <see cref="Data"/> is then only the recent window (ids restart at 0 after a re-base). The game does NOT use it: the Sim cannot re-base a running state.
        /// </summary>
        public bool Stream;
        /// <summary>Stream mode: re-base when the top height passes this (default 12000 u).</summary>
        public int RebaseAtYRaw = Fx.FromInt(12000).Raw;
        /// <summary>Stream mode: rows kept below the top after a re-base.</summary>
        public const int KeepRows = 200;
        /// <summary>Total height (raw Q16.16) removed by re-bases; absolute height = OriginYRaw + stored height.</summary>
        public long OriginYRaw { get; private set; }
        private ulong fp = 14695981039346656037UL;
        private readonly List<long> milestones = new List<long>();

        /// <summary>Rolling hash of everything generated so far (absolute heights), independent of re-bases and snapshots: equal for equal prefixes.</summary>
        public long Fingerprint { get { return (long)fp; } }
        /// <summary>Fingerprint after every 1000th row (index 0 = row 1000), for prefix-stability checks of very long towers.</summary>
        public IList<long> Milestones { get { return milestones; } }

        /// <summary>Latest snapshot. Replaced (new arrays) after every growth; compare <see cref="Version"/> to detect it.</summary>
        public LevelData Data { get; private set; }
        public int Version { get; private set; }
        public int TopRow { get { return row; } }
        public LevelSpec Spec { get { return spec; } }

        public EndlessTower(SimConfig cfg, LevelSpec spec) : this(cfg, spec, ChunkRows + LookaheadRows)
        {
        }

        public EndlessTower(SimConfig cfg, LevelSpec spec, int initialRows)
        {
            this.cfg = cfg;
            this.spec = spec;
            signature = spec.Signature;
            sigKind = SignatureKinds.PlatformFor(spec.Signature);
            rng = new Rng(spec.Seed);
            hazRng = new Rng(spec.Seed ^ 0x5A3C9E17);
            specRng = new Rng(spec.Seed ^ 0x6A09E667);
            for (int n = 1; n <= MaxLevel; n++)
            {
                levelD[n] = LevelSpec.DifficultyForLevel(cfg, n, spec.RegionOffset);
                levelOptions[n] = OptionsForLevel(spec, n);
            }
            plats = new List<Platform>(512);

            Platform start = new Platform();
            start.Id = 0;
            start.Row = 0;
            start.Kind = PlatformKind.Normal;
            start.X = cfg.ScreenWidth / 2;
            start.Y = Fx.Zero;
            start.Width = cfg.ScreenWidth;
            plats.Add(start);
            prevX = start.X;
            prevW = start.Width;
            prevY = Fx.Zero;
            prevKind = PlatformKind.Normal;
            row = 0;
            GenerateTo(initialRows);
        }

        /// <summary>
        /// The spec of an endless run. Difficulty is the scroll-pressure difficulty of the run start (the region offset applied to the base of campaign
        /// level 1, 0.05); the layout difficulty per row comes from the offset and the level of the floor (LevelForFloor, DifficultyAt).
        /// <paramref name="difficultyFloors"/> is kept for the remote config but no longer used: the old 400-floor ramp is replaced by the endless cycle.
        /// </summary>
        public static LevelSpec MakeSpec(int seed, Fx regionOffset, SignatureKind signature, int difficultyFloors)
        {
            LevelSpec spec = new LevelSpec();
            spec.Seed = seed;
            spec.Endless = true;
            spec.RegionOffset = regionOffset;
            spec.EndlessDifficultyFloors = difficultyFloors < 1 ? 400 : difficultyFloors;
            spec.Difficulty = LevelGenerator.EffectiveDifficulty(StartBase, regionOffset);
            spec.LevelIndex = 4;
            spec.Signature = signature;
            spec.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = int.MaxValue };
            return spec;
        }

        /// <summary>The kinds a special row of campaign level <paramref name="level"/> may carry in this region: exactly SpecialPlatforms.Options of that level (level 1 has no hazard types).</summary>
        public static PlatformKind[] OptionsForLevel(LevelSpec spec, int level)
        {
            if (spec.Signature == SignatureKind.None && spec.ActiveNewPlatforms().Length == 0) return new PlatformKind[0];
            LevelSpec ls = new LevelSpec();
            ls.Signature = spec.Signature;
            ls.NewPlatforms = spec.NewPlatforms;
            ls.Goal = spec.Goal;
            ls.LevelIndex = level;
            return SpecialPlatforms.Options(ls, SignatureKinds.PlatformFor(spec.Signature));
        }

        /// <summary>All kinds an endless row may carry in the region (the level 15 list, which is the union of the lists of all levels).</summary>
        public static PlatformKind[] EndlessOptions(LevelSpec spec, PlatformKind sig)
        {
            return OptionsForLevel(spec, MaxLevel);
        }

        /// <summary>Floor from which <paramref name="kind"/> may appear in the endless tower of <paramref name="spec"/> (the start of the first level whose list has it), or -1 when the region does not use it.</summary>
        public static int KindUnlockFloor(SimConfig cfg, LevelSpec spec, PlatformKind kind)
        {
            for (int n = 1; n <= MaxLevel; n++)
            {
                PlatformKind[] o = OptionsForLevel(spec, n);
                for (int i = 0; i < o.Length; i++) if (o[i] == kind) return (n - 1) * FloorsPerLevel;
            }
            return -1;
        }

        /// <summary>Builds a tower with exactly <paramref name="rows"/> rows (tests and golden hashes).</summary>
        public static LevelData BuildFixed(SimConfig cfg, LevelSpec spec, int rows)
        {
            EndlessTower t = new EndlessTower(cfg, spec, rows);
            return t.Data;
        }

        public Sim CreateSim()
        {
            return new Sim(cfg, Data, spec.Difficulty, true, false);
        }

        /// <summary>Layout difficulty of a row, 0..1: the difficulty of the campaign level LevelForFloor(r) in this region (offset applied).</summary>
        public Fx DifficultyAt(int r)
        {
            return levelD[LevelForFloor(r)];
        }

        /// <summary>Grows the tower (in whole chunks) until it reaches <paramref name="playerRow"/> + LookaheadRows. True when it grew.</summary>
        public bool EnsureAhead(int playerRow)
        {
            int need = playerRow + LookaheadRows;
            if (row >= need || row >= MaxRows) return false;
            int target = row;
            while (target < need) target += ChunkRows;
            GenerateTo(target);
            return true;
        }

        /// <summary>Stack rule (LevelBuilder.MaxPerWindow): optional platforms only while the 3 u window ending at y holds at most MaxPerWindow - 2.</summary>
        private bool OptionalFits(Fx y)
        {
            int n = 0;
            for (int i = plats.Count - 1; i >= 0; i--)
            {
                Fx py = plats[i].Y;
                if (py < y - LevelBuilder.WindowHeight) break;
                if (py <= y) n++;
            }
            return n <= LevelBuilder.MaxPerWindow - 2;
        }

        private static int PercentLerp(int easy, int hard, Fx d)
        {
            return Fx.Lerp(Fx.FromInt(easy), Fx.FromInt(hard), d).FloorToInt();
        }

        /// <summary>Generates rows up to and including <paramref name="target"/> (capped at MaxRows unless Stream) and publishes a new snapshot.</summary>
        public void GenerateTo(int target)
        {
            if (!Stream && target > MaxRows) target = MaxRows;
            if (target <= row) return;
            while (row < target)
            {
                AddRow(row + 1);
                if (Stream && prevY.Raw > RebaseAtYRaw) Rebase();
            }
            LevelData d = new LevelData();
            d.Platforms = plats.ToArray();
            d.Hazards = hazards.ToArray();
            d.Rows = row;
            Data = d;
            Version++;
        }

        /// <summary>Stream mode: drops the rows below TopRow - KeepRows and shifts every stored height down, so Q16.16 never overflows.</summary>
        private void Rebase()
        {
            int keepFromRow = row - KeepRows;
            int k = 0;
            while (k < plats.Count && plats[k].Row < keepFromRow) k++;
            if (k == 0 || k >= plats.Count) return;
            int shift = plats[k].Y.Raw;
            plats.RemoveRange(0, k);
            for (int i = 0; i < plats.Count; i++)
            {
                Platform p = plats[i];
                p.Id = i;
                p.Y = Fx.FromRaw(p.Y.Raw - shift);
                plats[i] = p;
            }
            int h = 0;
            while (h < hazards.Count && hazards[h].Y.Raw < shift) h++;
            hazards.RemoveRange(0, h);
            for (int i = 0; i < hazards.Count; i++)
            {
                Hazard z = hazards[i];
                z.Id = i;
                z.Y = Fx.FromRaw(z.Y.Raw - shift);
                hazards[i] = z;
            }
            prevY = Fx.FromRaw(prevY.Raw - shift);
            OriginYRaw += shift;
        }

        private void MixFp(long v)
        {
            unchecked { fp = (fp ^ (ulong)v) * 1099511628211UL; }
        }

        private void AddRow(int r)
        {
            Fx screen = cfg.ScreenWidth;
            Fx half = cfg.PlayerHalfWidth;
            Fx maxGap = cfg.LayoutMaxJumpHeight() * 9 / 10;
            int level = LevelForFloor(r);
            Fx d = levelD[level];

            Fx widthEasyLo = Fx.FromInt(6), widthEasyHi = Fx.FromInt(8);
            Fx widthHardLo = Fx.FromRatio(16, 10), widthHardHi = Fx.FromRatio(24, 10);
            Fx gapEasyLo = Fx.FromInt(2), gapEasyHi = Fx.FromInt(3);
            Fx gapHardLo = Fx.FromRatio(38, 10), gapHardHi = Fx.FromRatio(52, 10);
            Fx offEasyLo = Fx.Zero, offEasyHi = Fx.FromRatio(2, 5);
            Fx offHardLo = Fx.FromRatio(1, 5), offHardHi = Fx.FromRatio(9, 10);
            Fx jitterLo = Fx.FromRatio(85, 100), jitterHi = Fx.FromRatio(115, 100);
            Fx minWidth = Fx.FromRatio(14, 10);
            Fx maxWidth = Fx.FromRatio(95, 10);

            if (r > LevelGenerator.FixedRows && patternLeft <= 0)
            {
                pattern = rng.Next(3);
                patternLeft = rng.Range(6, 11);
            }
            patternLeft--;
            int rowPattern = r > LevelGenerator.FixedRows ? pattern : 0;

            Fx gap = rng.Range(Fx.Lerp(gapEasyLo, gapHardLo, d), Fx.Lerp(gapEasyHi, gapHardHi, d));
            gap = Fx.Min(gap, cfg.NormalGapMax);
            if (prevKind == PlatformKind.Sticky) gap = Fx.Min(gap, cfg.StickyNextGapMax);
            bool afterBounce = PlatformKinds.IsBouncePad(prevKind);
            if (afterBounce) gap = Fx.Min(gap, cfg.BounceNextGapMax);

            // Special platforms of the row: the region's level-N list at the level-N share (the first fixed rows stay plain).
            PlatformKind kind = PlatformKind.Normal;
            if (levelOptions[level].Length > 0)
            {
                if (r > LevelGenerator.FixedRows) kind = PickSpecial(r, level);
            }
            else if (sigKind != PlatformKind.Normal && r > LevelGenerator.FixedRows && level >= 3)
            {
                int sigShare = level == 3 ? cfg.SignatureIntroPercent : PercentLerp(cfg.SignatureBasePercent, cfg.SignatureMaxPercent, d);
                if (sigShare > cfg.SignatureMaxPercent) sigShare = cfg.SignatureMaxPercent;
                if (sigShare > 0 && rng.Next(100) < sigShare) kind = sigKind;
            }
            if (afterBounce && PlatformKinds.IsBouncePad(kind)) kind = PlatformKind.Normal;

            Fx w = rng.Range(Fx.Lerp(widthEasyLo, widthHardLo, d), Fx.Lerp(widthEasyHi, widthHardHi, d));
            w = w * rng.Range(jitterLo, jitterHi);
            if (rowPattern == 1) w = rng.Range(Fx.Lerp(Fx.FromRatio(26, 10), Fx.FromRatio(15, 10), d), Fx.Lerp(Fx.FromRatio(32, 10), Fx.FromRatio(24, 10), d));
            else if (rowPattern == 2) w = rng.Range(Fx.FromRatio(28, 10), Fx.FromRatio(40, 10));
            w = Fx.Clamp(w, minWidth, maxWidth);
            if (kind == PlatformKind.Moving && w > cfg.DriftMaxWidth) w = cfg.DriftMaxWidth;
            if (PlatformKinds.IsRegionNew(kind)) w = SpecialPlatforms.ConstrainWidth(cfg, kind, w);
            w = LevelBuilder.SnapWidth(w, kind);   // only painted widths exist (owner 2026-10-24: endless follows the campaign rules, no stretching)
            if (kind == PlatformKind.Moving && w > cfg.DriftMaxWidth) w = Fx.FromInt(2);
            gap = Fx.Min(gap, LevelBuilder.SmallPlatformGapCap(maxGap, Fx.Min(w, prevW)));
            gap = SpecialPlatforms.ConstrainGap(cfg, kind, prevKind, gap);
            Fx y = prevY + gap;

            Fx jm = prevKind == PlatformKind.Sticky ? cfg.StickyJumpMultiplier : Fx.One;
            Fx offFrac = rng.Range(Fx.Lerp(offEasyLo, offHardLo, d), Fx.Lerp(offEasyHi, offHardHi, d));
            Fx dx = offFrac * screen;
            Fx dxLimit = cfg.LayoutMaxReachDx(gap, jm) * 6 / 10 + (prevW + w) / 2 + half;
            if (afterBounce) dxLimit = Fx.Min(dxLimit, cfg.BounceNextDxMax + (prevW + w) / 2);
            dx = Fx.Min(dx, dxLimit);

            int dir = rng.Next(2) == 0 ? -1 : 1;
            Fx lo = w / 2, hi = screen - w / 2;
            if (rowPattern == 2)
            {
                dir = prevX < screen / 2 ? 1 : -1;
                dx = Fx.Max(dx, screen / 3);
                dx = Fx.Min(dx, dxLimit);
            }
            Fx x = prevX + dx * dir;
            if (x < lo || x > hi) x = prevX - dx * dir;
            x = Fx.Clamp(x, lo, hi);
            if (prevKind == PlatformKind.Dandelion && prevDir != 0) x = SpecialPlatforms.DandelionNextX(cfg, ref specRng, prevX, prevDir, w, screen);
            if (prevKind == PlatformKind.LicoriceBelt && prevDir != 0) x = SpecialPlatforms.BeltNextX(prevX, prevDir, dx, w, screen);
            if (prevKind == PlatformKind.BambooFlick && prevDir != 0) x = SpecialPlatforms.FlickNextX(cfg, ref specRng, prevX, prevDir, w, screen);

            Fx amp = Fx.Zero;
            int period = 0, phase = 0, pdir = 0;
            if (PlatformKinds.IsRegionNew(kind))
            {
                SpecialPlatforms.Params(cfg, ref specRng, kind, x, Fx.Min(x - lo, hi - x), screen, out amp, out period, out phase, out pdir);
                if (PlatformKinds.IsHazardType(kind)) lastHazardRow = r;
            }
            else if (kind == PlatformKind.Moving)
            {
                Fx room = Fx.Min(x - lo, hi - x);
                Fx want = signature == SignatureKind.DriftingPlatforms
                    ? rng.Range(cfg.DriftAmplitudeMin, cfg.DriftAmplitudeMax)
                    : rng.Range(Fx.Half, Fx.One);
                amp = Fx.Min(want, room);
                period = 2 * rng.Range(90, 150);
                phase = rng.Next(period);
            }
            Add(r, kind, x, y, w, amp, period, phase, pdir);

            if (rowPattern == 1)
            {
                // Dense patch: 1 to 3 extra small platforms on the same row (about one in four crumbles); the spine platform stays reachable.
                int extras = 1 + rng.Next(3);
                usedX[0] = x; usedW[0] = w + amp * 2;   // a swinging platform keeps its whole path clear
                int nUsed = 1;
                for (int e = 0; e < extras; e++)
                {
                    Fx w2 = rng.Range(Fx.FromRatio(15, 10), Fx.FromRatio(26, 10));
                    PlatformKind k2 = rng.Next(100) < 25 ? PlatformKind.Crumble : PlatformKind.Normal;
                    w2 = LevelBuilder.SnapWidth(w2, k2);
                    Fx x2 = rng.Range(w2 / 2, screen - w2 / 2);
                    bool free = true;
                    for (int u = 0; u < nUsed; u++) if (Fx.Abs(x2 - usedX[u]) < (w2 + usedW[u]) / 2 + Fx.FromRatio(6, 10)) free = false;
                    if (!free || !OptionalFits(y)) continue;
                    Add(r, k2, x2, y, w2, Fx.Zero, 0, 0);
                    usedX[nUsed] = x2; usedW[nUsed] = w2; nUsed++;
                }
            }
            else
            {
                int secondChance = (Fx.FromInt(50) * (Fx.One - d)).FloorToInt();
                if (kind == PlatformKind.Normal && rng.Next(100) < secondChance)
                {
                    Fx x2 = screen - x;
                    if (Fx.Abs(x2 - x) >= (w + w) / 2 + Fx.One && OptionalFits(y))
                        Add(r, PlatformKind.Normal, Fx.Clamp(x2, lo, hi), y, w, Fx.Zero, 0, 0);
                }
            }

            if (signature == SignatureKind.SwingingGongs && level >= 3 && r > LevelGenerator.FixedRows + 1) AddGong(r, d, x, y, level);

            prevX = x; prevW = w; prevY = y; prevKind = kind; prevDir = pdir;
            row = r;
            if (r % 1000 == 0) milestones.Add((long)fp);
        }

        /// <summary>
        /// Draws the special platform of endless row <paramref name="r"/> (campaign level <paramref name="level"/>): the share is the level's own row ramp
        /// (SpecialPlatforms.RowPercent over the 100 floors of the step), the hazard spacing the level's (SpecialPlatforms.HazardSpacing), the kinds the
        /// region's level-N list, with the same placement rules as the campaign (SpecialPlatforms.Allowed). Normal when the row stays plain.
        /// </summary>
        private PlatformKind PickSpecial(int r, int level)
        {
            int share = SpecialPlatforms.RowPercent(cfg, level, PositionInLevel(r), FloorsPerLevel);
            if (specRng.Next(100) >= share) return PlatformKind.Normal;
            int spacing = SpecialPlatforms.HazardSpacing(cfg, level);
            PlatformKind[] opts = levelOptions[level];
            int n = 0;
            for (int i = 0; i < opts.Length; i++)
            {
                if (!SpecialPlatforms.Allowed(opts[i], prevKind, r - lastHazardRow, spacing, false)) continue;
                pool[n++] = i;
            }
            if (n == 0) return PlatformKind.Normal;
            return opts[pool[specRng.Next(n)]];
        }

        private void Add(int r, PlatformKind kind, Fx x, Fx y, Fx w, Fx amp, int period, int phase, int dir = 0)
        {
            Platform p = new Platform();
            p.Dir = dir;
            p.Id = plats.Count;
            p.Row = r;
            p.Kind = kind;
            p.X = x;
            p.Y = y;
            p.Width = w;
            p.Amplitude = amp;
            p.Period = period;
            p.Phase = phase;
            plats.Add(p);
            MixFp(r); MixFp((int)kind); MixFp(x.Raw); MixFp(OriginYRaw + y.Raw); MixFp(w.Raw); MixFp(amp.Raw); MixFp(period); MixFp(phase); MixFp(dir);
        }

        /// <summary>Gong in the jump corridor between the previous and this spine platform (same rules as LevelBuilder.BuildHazards; level 3 halves the density).</summary>
        private void AddGong(int r, Fx d, Fx x, Fx y, int level)
        {
            int chance = PercentLerp(cfg.GongDensityEasyPercent, cfg.GongDensityHardPercent, d);
            if (level == 3) chance /= 2;
            if (chance <= 0 || hazRng.Next(100) >= chance) return;
            int side = hazRng.Next(2) == 0 ? -1 : 1;
            Fx screen = cfg.ScreenWidth;
            Fx mx = (prevX + x) / 2;
            Fx my = prevY + (y - prevY) / 2;   // not (prevY + y) / 2: that sum wraps past 16384 u (about floor 4500) and misplaced the gongs high in a very tall tower
            Hazard h = new Hazard();
            h.Id = hazards.Count;
            h.Kind = HazardKind.Gong;
            h.Radius = Fx.Lerp(cfg.GongRadiusEasy, cfg.GongRadiusHard, d);
            h.Amplitude = hazRng.Range(cfg.GongAmplitudeMin, cfg.GongAmplitudeMax);
            Fx offset = hazRng.Range(Fx.One, Fx.FromRatio(25, 10));
            Fx ax = mx + offset * side;
            Fx edge = h.Amplitude + h.Radius;
            if (edge * 2 < screen) ax = Fx.Clamp(ax, edge, screen - edge);
            h.X = ax;
            h.Y = my + Fx.Half;
            h.Period = 2 * hazRng.Range(70, 120);
            h.Phase = hazRng.Next(h.Period);
            hazards.Add(h);
            MixFp(-r); MixFp(h.X.Raw); MixFp(OriginYRaw + h.Y.Raw); MixFp(h.Radius.Raw); MixFp(h.Amplitude.Raw); MixFp(h.Period); MixFp(h.Phase);
        }
    }
}
