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
    /// Difficulty d(row) = base + offset * (1 - base) with base = min(1, row / DifficultyFloors), so it starts at the region offset
    /// and reaches 1 at the endless difficulty floor (doc 09 D16).
    /// </summary>
    public sealed class EndlessTower
    {
        /// <summary>Rows added per growth step.</summary>
        public const int ChunkRows = 40;
        /// <summary>The tower is kept this many rows above the player.</summary>
        public const int LookaheadRows = 60;
        /// <summary>Hard cap: Q16.16 holds about 32000 u, a row is at most 4.2 u, so rows past 7000 would overflow. Unreachable in practice.</summary>
        public const int MaxRows = 7000;
        /// <summary>Floors a newly unlocked kind needs to reach its full draw weight (weight 1 at its unlock floor, EndlessFadeFloors from then on).</summary>
        public const int EndlessFadeFloors = 10;

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
        private readonly PlatformKind[] specialOptions;
        /// <summary>Per option: the floor (rows past the fixed start) from which the kind may be drawn; the new kinds phase in one by one (UnlockFloors).</summary>
        private readonly int[] unlockFloor;
        private int lastHazardRow = -1000;
        private int prevDir;

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
            specialOptions = EndlessOptions(spec, sigKind);
            unlockFloor = UnlockFloors(cfg, spec, specialOptions);
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
        /// The spec of an endless run. Difficulty is the scroll-pressure difficulty of the run (the region offset applied to a base of
        /// 0.05, like a first campaign level); the layout difficulty per row comes from the offset and the floor (DifficultyAt).
        /// </summary>
        public static LevelSpec MakeSpec(int seed, Fx regionOffset, SignatureKind signature, int difficultyFloors)
        {
            LevelSpec spec = new LevelSpec();
            spec.Seed = seed;
            spec.Endless = true;
            spec.RegionOffset = regionOffset;
            spec.EndlessDifficultyFloors = difficultyFloors < 1 ? 400 : difficultyFloors;
            spec.Difficulty = LevelGenerator.EffectiveDifficulty(Fx.FromRatio(5, 100), regionOffset);
            spec.LevelIndex = 4;
            spec.Signature = signature;
            spec.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = int.MaxValue };
            return spec;
        }

        /// <summary>The kinds an endless row may carry: the region's new set plus its signature platform (endless has no level 2 light touch, it ramps by floor).</summary>
        public static PlatformKind[] EndlessOptions(LevelSpec spec, PlatformKind sig)
        {
            // Owner 2026-11-02: endless uses ALL the platforms of the region, the same kinds as its campaign levels (SpecialPlatforms.Options): the region's
            // new set, its signature platform and the generic kinds it keeps. A spec without a signature or new set has no specials.
            if (spec.Signature == SignatureKind.None && spec.ActiveNewPlatforms().Length == 0) return new PlatformKind[0];
            return SpecialPlatforms.Options(spec, sig);
        }

        /// <summary>
        /// The floor from which each option may appear (index = option index): the signature platform and the sand slab / rotting log Crumble are there from the
        /// start, the other kinds phase in one by one every SimConfig.EndlessKindStepFloors floors, easy ones first (plain timing kinds, then launchers and
        /// demanding kinds, then hazard types), so the mix changes a little every 10 floors and never in one jump.
        /// </summary>
        public static int[] UnlockFloors(SimConfig cfg, LevelSpec spec, PlatformKind[] options)
        {
            PlatformKind sig = SignatureKinds.PlatformFor(spec.Signature);
            int[] floor = new int[options.Length];
            List<PlatformKind> later = new List<PlatformKind>();
            for (int cls = 0; cls < 3; cls++)
                for (int i = 0; i < options.Length; i++)
                {
                    PlatformKind k = options[i];
                    if (k == sig || k == PlatformKind.Crumble || (sig == PlatformKind.Normal && k == PlatformKind.Moving) || later.Contains(k)) continue;   // region 5 (gong objects, no signature platform): the rope-bridge Moving platform is there from the start
                    int c = PlatformKinds.IsHazardType(k) ? 2 : (PlatformKinds.IsLaunch(k) || PlatformKinds.IsDemanding(k) ? 1 : 0);
                    if (c == cls) later.Add(k);
                }
            for (int i = 0; i < options.Length; i++)
            {
                int n = later.IndexOf(options[i]);
                floor[i] = n < 0 ? 0 : (n + 1) * cfg.EndlessKindStepFloors;
            }
            return floor;
        }

        /// <summary>Floor from which <paramref name="kind"/> may appear in the endless tower of <paramref name="spec"/>, or -1 when the region does not use it.</summary>
        public static int KindUnlockFloor(SimConfig cfg, LevelSpec spec, PlatformKind kind)
        {
            PlatformKind[] options = EndlessOptions(spec, SignatureKinds.PlatformFor(spec.Signature));
            int[] floor = UnlockFloors(cfg, spec, options);
            for (int i = 0; i < options.Length; i++) if (options[i] == kind) return floor[i];
            return -1;
        }

        /// <summary>Minimum rows between two hazard-type specials after <paramref name="f"/> floors: 6 at the start, one row less about every 37 floors, 2 from EndlessSpecialRampFloors on (a smooth ramp, no step).</summary>
        public static int HazardSpacingAt(SimConfig cfg, int f)
        {
            int ramp = cfg.EndlessSpecialRampFloors;
            int ff = f < 0 ? 0 : (f > ramp ? ramp : f);
            return cfg.HazardEarlyRowsPerHazard - (cfg.HazardEarlyRowsPerHazard - 2) * ff / ramp;
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

        /// <summary>Difficulty of a row, 0..1 (region offset applied).</summary>
        public Fx DifficultyAt(int r)
        {
            int floors = spec.EndlessDifficultyFloors < 1 ? 400 : spec.EndlessDifficultyFloors;
            Fx b = r >= floors ? Fx.One : Fx.FromRatio(r, floors);
            return LevelGenerator.EffectiveDifficulty(b, spec.RegionOffset);
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

        /// <summary>Generates rows up to and including <paramref name="target"/> (capped at MaxRows) and publishes a new snapshot.</summary>
        public void GenerateTo(int target)
        {
            if (target > MaxRows) target = MaxRows;
            if (target <= row) return;
            while (row < target) AddRow(row + 1);
            LevelData d = new LevelData();
            d.Platforms = plats.ToArray();
            d.Hazards = hazards.ToArray();
            d.Rows = row;
            Data = d;
            Version++;
        }

        private void AddRow(int r)
        {
            Fx screen = cfg.ScreenWidth;
            Fx half = cfg.PlayerHalfWidth;
            Fx maxGap = cfg.LayoutMaxJumpHeight() * 9 / 10;
            Fx d = DifficultyAt(r);

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

            // Signature platforms: the share grows with d like a campaign level 4+ (the first fixed rows stay plain).
            PlatformKind kind = PlatformKind.Normal;
            if (specialOptions.Length > 0)
            {
                if (r > LevelGenerator.FixedRows) kind = PickSpecial(r);
            }
            else if (sigKind != PlatformKind.Normal && r > LevelGenerator.FixedRows)
            {
                int sigShare = PercentLerp(cfg.SignatureBasePercent, cfg.SignatureMaxPercent, d);
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
                Fx[] usedX = new Fx[4];
                Fx[] usedW = new Fx[4];
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

            if (signature == SignatureKind.SwingingGongs && r > LevelGenerator.FixedRows + 1) AddGong(r, d, x, y);

            prevX = x; prevW = w; prevY = y; prevKind = kind; prevDir = pdir;
            row = r;
        }

        /// <summary>
        /// Draws the special platform of endless row <paramref name="r"/> from the per-floor ramp (share 15 percent rising to 55 percent over
        /// EndlessSpecialRampFloors) and the same placement rules as the campaign (SpecialPlatforms.Allowed). Normal when the row stays plain.
        /// </summary>
        private PlatformKind PickSpecial(int r)
        {
            int f = r - LevelGenerator.FixedRows;
            int share = SpecialPlatforms.EndlessSharePercent(cfg, f);
            if (specRng.Next(100) >= share) return PlatformKind.Normal;
            int spacing = HazardSpacingAt(cfg, f);
            // A kind that has just unlocked fades in: its weight grows by one tenth of the full weight per floor, so the mix shifts a little every floor
            // instead of jumping when the kind arrives (full weight EndlessFadeFloors floors after its unlock floor).
            int n = 0, total = 0;
            int[] pool = new int[specialOptions.Length];
            int[] weight = new int[specialOptions.Length];
            for (int i = 0; i < specialOptions.Length; i++)
            {
                if (f < unlockFloor[i] || !SpecialPlatforms.Allowed(specialOptions[i], prevKind, r - lastHazardRow, spacing, false)) continue;
                int w = f - unlockFloor[i] + 1;
                if (w > EndlessFadeFloors) w = EndlessFadeFloors;
                pool[n] = i; weight[n] = w; total += w; n++;
            }
            if (n == 0) return PlatformKind.Normal;
            int roll = specRng.Next(total), acc = 0;
            for (int j = 0; j < n; j++)
            {
                acc += weight[j];
                if (roll < acc) return specialOptions[pool[j]];
            }
            return specialOptions[pool[n - 1]];
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
        }

        /// <summary>Gong in the jump corridor between the previous and this spine platform (same rules as LevelBuilder.BuildHazards).</summary>
        private void AddGong(int r, Fx d, Fx x, Fx y)
        {
            int chance = PercentLerp(cfg.GongDensityEasyPercent, cfg.GongDensityHardPercent, d);
            if (chance <= 0 || hazRng.Next(100) >= chance) return;
            int side = hazRng.Next(2) == 0 ? -1 : 1;
            Fx screen = cfg.ScreenWidth;
            Fx mx = (prevX + x) / 2;
            Fx my = (prevY + y) / 2;
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
        }
    }
}
