using System.Collections.Generic;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Builds a complete campaign level (platforms, hazards, pickups) from a LevelSpec. Deterministic: the
    /// same spec and SimConfig give identical LevelData on every device. Three independent Rng streams
    /// (platforms, pickups, hazards) are derived from spec.Seed so changing coin density never moves a platform.
    /// Same row geometry as LevelGenerator (doc 08 section 4); see NOTES.md for the rules added on top.
    /// </summary>
    public static class LevelBuilder
    {
        private sealed class Ctx
        {
            public List<Platform> Plats;
            public Fx[] X, Y, W;
            public PlatformKind[] K;

            public void Add(int row, PlatformKind kind, Fx x, Fx y, Fx w, Fx amp, int period, int phase)
            {
                Platform p = new Platform();
                p.Id = Plats.Count;
                p.Row = row;
                p.Kind = kind;
                p.X = x;
                p.Y = y;
                p.Width = w;
                p.Amplitude = amp;
                p.Period = period;
                p.Phase = phase;
                Plats.Add(p);
                X[row] = x; Y[row] = y; W[row] = w; K[row] = kind;
            }
        }

        /// <summary>Rows kept free at the top for the final chunk when chunks are spliced at intervals.</summary>
        private const int ChunkEndReserve = 12;

        private static int PercentLerp(int easy, int hard, Fx d)
        {
            return Fx.Lerp(Fx.FromInt(easy), Fx.FromInt(hard), d).FloorToInt();
        }

        private static bool Overlaps(bool[] locked, int from, int to)
        {
            for (int r = from; r <= to; r++) if (r >= 0 && r < locked.Length && locked[r]) return true;
            return false;
        }

        /// <summary>Largest allowed gap above or below a platform of width w: full jump height from 3.2 wide, shrinking to 55 percent at 1.5 wide.</summary>
        public static Fx SmallPlatformGapCap(Fx maxGap, Fx w)
        {
            Fx full = Fx.FromRatio(32, 10), tiny = Fx.FromRatio(15, 10);
            if (w >= full) return maxGap;
            if (w <= tiny) return maxGap * 55 / 100;
            Fx t = (w - tiny) / (full - tiny);              // 0..1
            return maxGap * (55 + (Fx.FromInt(45) * t).FloorToInt()) / 100;
        }

        /// <summary>
        /// Stack rule (owner report 2026-10-20): at most MaxPerWindow platforms (any x, any kind) with surfaces inside one WindowHeight tall
        /// window. The builder adds optional platforms (dense extras, second platform, hidden gem side platform) only while the window
        /// ending at the new platform holds at most MaxPerWindow - 2 others, which keeps one slot free for the next spine platform.
        /// </summary>
        public static readonly Fx WindowHeight = Fx.FromInt(3);
        public const int MaxPerWindow = 4;

        private static int CountInWindow(List<Platform> plats, Fx y)
        {
            int n = 0;
            for (int i = plats.Count - 1; i >= 0; i--)
            {
                Fx py = plats[i].Y;
                if (py < y - WindowHeight) { if (py < y - WindowHeight - Fx.FromInt(5)) break; continue; }
                if (py <= y) n++;
            }
            return n;
        }

        private static bool OptionalPlatformFits(List<Platform> plats, Fx y)
        {
            return CountInWindow(plats, y) <= MaxPerWindow - 2;
        }

        public static LevelData Build(SimConfig cfg, LevelSpec spec)
        {
            int rows = spec.RowsFor();
            Fx d = spec.Difficulty;
            Fx screen = cfg.ScreenWidth;
            Fx half = cfg.PlayerHalfWidth;
            Fx maxGap = cfg.LayoutMaxJumpHeight() * 9 / 10;
            int idx = spec.LevelIndex;
            Rng rng = new Rng(spec.Seed);

            // ---- template: signature platform share by level (doc 08 section 6) ----
            PlatformKind sigKind = idx >= 3 ? SignatureKinds.PlatformFor(spec.Signature) : PlatformKind.Normal;
            int sigShare = 0;
            if (sigKind != PlatformKind.Normal)
            {
                if (idx == 3) sigShare = cfg.SignatureIntroPercent;
                else sigShare = PercentLerp(cfg.SignatureBasePercent, cfg.SignatureMaxPercent, d);
                if (sigShare > cfg.SignatureMaxPercent) sigShare = cfg.SignatureMaxPercent;
            }

            // ---- plan fixed-position elements: combo ladder, chunks, hidden gem, wave ----
            bool[] locked = new bool[rows + 3];
            LevelData data = new LevelData();
            data.Rows = rows;

            int ladderStart = -1, ladderLen = 0, ladderV = 0;
            if (spec.Goal.Type == GoalType.ComboChain)
            {
                ladderV = spec.Goal.Value < 2 ? 2 : spec.Goal.Value;
                int cnt = spec.Goal.Count < 1 ? 1 : spec.Goal.Count;
                ladderLen = ladderV * cnt + 2;
                ladderStart = rows * 30 / 100;
                if (ladderStart < 5) ladderStart = 5;
                if (ladderStart + ladderLen > rows - 2) ladderStart = rows - 2 - ladderLen;
                for (int r = ladderStart; r < ladderStart + ladderLen; r++) locked[r] = true;
                data.LadderStartRow = ladderStart;
                data.LadderEndRow = ladderStart + ladderLen - 1;
            }

            int[] chunkAt = new int[rows + 3];
            for (int i = 0; i < chunkAt.Length; i++) chunkAt[i] = -1;
            List<Chunk> chunkList = new List<Chunk>();
            if (spec.FixedChunks != null && spec.FixedChunks.Length > 0)
            {
                int n = spec.FixedChunks.Length;
                int row = cfg.ChunkStartRow;
                int ci = 0;
                int lastEnd = 0;
                int guard = 0;
                while (guard++ < 64)
                {
                    Chunk ch = ChunkLibrary.Get(spec.FixedChunks[ci % n]);
                    ci++;
                    if (ch == null) { if (ci >= n && chunkList.Count == 0) break; continue; }
                    int len = ch.Rows.Length;
                    if (row + len > rows - ChunkEndReserve) break;
                    if (Overlaps(locked, row - 1, row + len))
                    {
                        row++;
                        ci--;
                        continue;
                    }
                    chunkAt[row] = chunkList.Count;
                    chunkList.Add(ch);
                    for (int r = row; r < row + len; r++) locked[r] = true;
                    lastEnd = row + len - 1;
                    row += len + cfg.ChunkInterval;
                }
                // plus one chunk at the end of the level ("final rows")
                Chunk fin = ChunkLibrary.Get(spec.FixedChunks[(ci > 0 ? ci - 1 : 0) % n]);
                if (fin != null)
                {
                    int start = rows - fin.Rows.Length + 1;
                    if (start > lastEnd + 3 && !Overlaps(locked, start - 1, rows + 1))
                    {
                        chunkAt[start] = chunkList.Count;
                        chunkList.Add(fin);
                        for (int r = start; r <= rows; r++) locked[r] = true;
                    }
                }
            }

            int gemRow = -1;
            if (spec.Goal.Type == GoalType.FindHiddenGem)
            {
                gemRow = rows / 2;
                while (gemRow < rows - 3 && locked[gemRow]) gemRow++;
                if (locked[gemRow]) { gemRow = rows / 2; while (gemRow > 3 && locked[gemRow]) gemRow--; }
            }

            bool wave = spec.Goal.Type == GoalType.SurviveWave;
            int waveStart = rows * cfg.WaveStartPercent / 100;
            int waveEnd = rows * cfg.WaveEndPercent / 100;
            if (wave) { data.WaveStartRow = waveStart; data.WaveEndRow = waveEnd; }

            // ---- platforms ----
            Ctx ctx = new Ctx();
            ctx.Plats = new List<Platform>(rows * 2 + 8);
            ctx.X = new Fx[rows + 1];
            ctx.Y = new Fx[rows + 1];
            ctx.W = new Fx[rows + 1];
            ctx.K = new PlatformKind[rows + 1];
            ctx.Add(0, PlatformKind.Normal, screen / 2, Fx.Zero, screen, Fx.Zero, 0, 0);

            Fx prevX = screen / 2, prevW = screen, y = Fx.Zero;
            PlatformKind prevKind = PlatformKind.Normal;

            Fx widthEasyLo = Fx.FromInt(6), widthEasyHi = Fx.FromInt(8);
            Fx widthHardLo = Fx.FromRatio(16, 10), widthHardHi = Fx.FromRatio(24, 10);
            Fx gapEasyLo = Fx.FromInt(2), gapEasyHi = Fx.FromInt(3);
            Fx gapHardLo = Fx.FromRatio(38, 10), gapHardHi = Fx.FromRatio(52, 10);
            Fx offEasyLo = Fx.Zero, offEasyHi = Fx.FromRatio(2, 5);
            Fx offHardLo = Fx.FromRatio(1, 5), offHardHi = Fx.FromRatio(9, 10);
            Fx jitterLo = Fx.FromRatio(85, 100), jitterHi = Fx.FromRatio(115, 100);
            Fx minWidth = Fx.FromRatio(14, 10);
            Fx maxWidth = Fx.FromRatio(95, 10);
            Fx hiddenGemX = Fx.Zero, hiddenGemY = Fx.Zero;
            bool hiddenGemPlaced = false;

            int rowNo = 1;
            int pattern = 0, patternLeft = 0;
            while (rowNo <= rows)
            {
                // ---------- combo ladder ----------
                if (rowNo == ladderStart)
                {
                    Fx gap0 = rng.Range(Fx.Lerp(gapEasyLo, gapHardLo, d), Fx.Lerp(gapEasyHi, gapHardHi, d));
                    gap0 = Fx.Min(gap0, cfg.NormalGapMax);
                    // The row below the ladder sits more than one window under its first rung, so no window holds more than MaxPerWindow.
                    gap0 = Fx.Max(gap0, WindowHeight + Fx.FromRatio(5, 100));
                    gap0 = Fx.Min(gap0, cfg.NormalGapMax);
                    if (prevKind == PlatformKind.Sticky) gap0 = Fx.Min(gap0, cfg.StickyNextGapMax);
                    bool lAfterBounce = prevKind == PlatformKind.Bounce || prevKind == PlatformKind.BounceCap;
                    if (lAfterBounce) gap0 = Fx.Min(gap0, cfg.BounceNextGapMax);
                    Fx jm0 = prevKind == PlatformKind.Sticky ? cfg.StickyJumpMultiplier : Fx.One;
                    // Rungs closer than LadderMinStackGap would stack 5+ platforms in a window: such ladders (comboChain value 4+) use
                    // rungs of LadderMinRung and alternate between two columns that are at least 2 u apart edge to edge.
                    Fx rung = cfg.ComboLadderSpan / ladderV;
                    bool zig = rung < cfg.LadderMinStackGap;
                    if (zig && rung < cfg.LadderMinRung) rung = cfg.LadderMinRung;
                    Fx lw = zig ? cfg.LadderZigWidth : cfg.ComboLadderWidth;
                    Fx lim0 = cfg.LayoutMaxReachDx(gap0, jm0) * 6 / 10 + (prevW + lw) / 2 + half;
                    if (lAfterBounce) lim0 = Fx.Min(lim0, cfg.BounceNextDxMax + (prevW + lw) / 2);
                    Fx xl = prevX + Fx.Clamp(screen / 2 - prevX, -lim0, lim0);
                    xl = Fx.Clamp(xl, lw / 2, screen - lw / 2);
                    Fx xOther = xl;
                    if (zig)
                    {
                        Fx colSep = lw + cfg.LadderColumnGap;
                        Fx edge = lw / 2;
                        if (xl < screen / 2) { xl = Fx.Clamp(xl, edge, screen - edge - colSep); xOther = xl + colSep; }
                        else { xl = Fx.Clamp(xl, edge + colSep, screen - edge); xOther = xl - colSep; }
                    }
                    for (int i = 0; i < ladderLen; i++)
                    {
                        y = y + (i == 0 ? gap0 : rung);
                        ctx.Add(rowNo + i, PlatformKind.Normal, (i & 1) == 0 ? xl : xOther, y, lw, Fx.Zero, 0, 0);
                    }
                    xl = (ladderLen & 1) == 1 ? xl : xOther;
                    prevX = xl; prevW = lw; prevKind = PlatformKind.Normal;
                    rowNo += ladderLen;
                    continue;
                }

                // ---------- authored chunk ----------
                if (chunkAt[rowNo] >= 0)
                {
                    Chunk ch = chunkList[chunkAt[rowNo]];
                    bool flip = rng.Next(2) == 1;
                    Fx delta = Fx.Zero;
                    for (int i = 0; i < ch.Rows.Length; i++)
                    {
                        ChunkRow cr = ch.Rows[i];
                        Fx cgap = Fx.Min(Fx.FromRatio(cr.GapHundredths, 100), cfg.NormalGapMax);
                        if (i == 0 && prevKind == PlatformKind.Sticky) cgap = Fx.Min(cgap, cfg.StickyNextGapMax);
                        bool cAfterBounce = i == 0 && (prevKind == PlatformKind.Bounce || prevKind == PlatformKind.BounceCap);
                        if (cAfterBounce) cgap = Fx.Min(cgap, cfg.BounceNextGapMax);
                        Fx cw = Fx.FromRatio(cr.WidthTenths, 10);
                        Fx ax = Fx.FromRatio(cr.XPermille, 1000) * screen;
                        if (flip) ax = screen - ax;
                        Fx cx;
                        if (i == 0)
                        {
                            Fx jm1 = prevKind == PlatformKind.Sticky ? cfg.StickyJumpMultiplier : Fx.One;
                            Fx lim1 = cfg.LayoutMaxReachDx(cgap, jm1) * 6 / 10 + (prevW + cw) / 2 + half;
                            if (cAfterBounce) lim1 = Fx.Min(lim1, cfg.BounceNextDxMax + (prevW + cw) / 2);
                            cx = prevX + Fx.Clamp(ax - prevX, -lim1, lim1);
                            cx = Fx.Clamp(cx, cw / 2, screen - cw / 2);
                            delta = cx - ax;
                        }
                        else
                        {
                            cx = Fx.Clamp(ax + delta, cw / 2, screen - cw / 2);
                        }
                        y = y + cgap;
                        Fx camp = Fx.Zero;
                        int cper = 0, cph = 0;
                        if (cr.Kind == PlatformKind.Moving)
                        {
                            camp = Fx.Min(cfg.ChunkMovingAmplitude, Fx.Min(cx - cw / 2, screen - cw / 2 - cx));
                            if (camp < Fx.Zero) camp = Fx.Zero;
                            cper = cfg.ChunkMovingPeriod;
                            cph = (i * 47) % cper;
                        }
                        else if (cr.Kind == PlatformKind.Vanishing)
                        {
                            cph = (i * 53) % (cfg.VanishVisibleTicks + cfg.VanishHiddenTicks);
                        }
                        ctx.Add(rowNo + i, cr.Kind, cx, y, cw, camp, cper, cph);
                        prevX = cx; prevW = cw; prevKind = cr.Kind;
                    }
                    rowNo += ch.Rows.Length;
                    continue;
                }

                // ---------- generated row ----------
                int row = rowNo;
                // Owner rules (2026-10-19): fixed easy ladder for the first rows, then the layout pattern changes every 6 to 10 rows
                // (about 15 seconds of climbing): ladder, dense patches of small platforms (some crumble), or a zigzag of mid-size ones.
                if (row > LevelGenerator.FixedRows && patternLeft <= 0)
                {
                    pattern = rng.Next(3);
                    patternLeft = rng.Range(6, 11);
                }
                patternLeft--;
                int rowPattern = row > LevelGenerator.FixedRows ? pattern : 0;
                Fx gap = rng.Range(Fx.Lerp(gapEasyLo, gapHardLo, d), Fx.Lerp(gapEasyHi, gapHardHi, d));
                gap = Fx.Min(gap, cfg.NormalGapMax);
                if (prevKind == PlatformKind.Sticky) gap = Fx.Min(gap, cfg.StickyNextGapMax);
                bool afterBounce = prevKind == PlatformKind.Bounce || prevKind == PlatformKind.BounceCap;
                if (afterBounce) gap = Fx.Min(gap, cfg.BounceNextGapMax);

                PlatformKind kind = PlatformKind.Normal;
                if (sigShare > 0 && row != gemRow && rng.Next(100) < sigShare) kind = sigKind;
                // Bounce platforms are never chained: the landing speed on a bounce pad is set by the previous flight,
                // so two pads in a row would make the second one pure luck.
                if (afterBounce && (kind == PlatformKind.Bounce || kind == PlatformKind.BounceCap)) kind = PlatformKind.Normal;

                Fx w = rng.Range(Fx.Lerp(widthEasyLo, widthHardLo, d), Fx.Lerp(widthEasyHi, widthHardHi, d));
                w = w * rng.Range(jitterLo, jitterHi);
                if (rowPattern == 1) w = rng.Range(Fx.Lerp(Fx.FromRatio(26, 10), Fx.FromRatio(15, 10), d), Fx.Lerp(Fx.FromRatio(32, 10), Fx.FromRatio(24, 10), d));
                else if (rowPattern == 2) w = rng.Range(Fx.FromRatio(28, 10), Fx.FromRatio(40, 10));
                w = Fx.Clamp(w, minWidth, maxWidth);
                if (kind == PlatformKind.Moving && w > cfg.DriftMaxWidth) w = cfg.DriftMaxWidth;
                if (row == gemRow && w > Fx.FromRatio(35, 10)) w = Fx.FromRatio(35, 10);
                // Narrow platforms get smaller gaps so one jump always works (owner, 2026-10-20).
                gap = Fx.Min(gap, SmallPlatformGapCap(maxGap, Fx.Min(w, prevW)));
                y = y + gap;

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
                    // Zigzag: sweep to the far side each row; the reach limit still bounds the distance.
                    dir = prevX < screen / 2 ? 1 : -1;
                    dx = Fx.Max(dx, screen / 3);
                    dx = Fx.Min(dx, dxLimit);
                }
                Fx x = prevX + dx * dir;
                if (x < lo || x > hi) x = prevX - dx * dir;
                x = Fx.Clamp(x, lo, hi);

                Fx amp = Fx.Zero;
                int period = 0, phase = 0;
                if (kind == PlatformKind.Moving)
                {
                    Fx room = Fx.Min(x - lo, hi - x);
                    Fx want = spec.Signature == SignatureKind.DriftingPlatforms
                        ? rng.Range(cfg.DriftAmplitudeMin, cfg.DriftAmplitudeMax)
                        : rng.Range(Fx.Half, Fx.One);
                    amp = Fx.Min(want, room);
                    period = 2 * rng.Range(90, 150);
                    phase = rng.Next(period);
                }
                ctx.Add(row, kind, x, y, w, amp, period, phase);

                if (row == gemRow)
                {
                    // Side platform for the hidden gem: same row, reachable from the previous spine platform by the same rule.
                    Fx ws = cfg.HiddenGemPlatformWidth;
                    Fx sep = (w + ws) / 2 + Fx.FromRatio(5, 10);
                    Fx limS = cfg.LayoutMaxReachDx(gap, jm) * 6 / 10 + (prevW + ws) / 2 + half;
                    Fx xs = Fx.Zero;
                    bool found = false;
                    for (int t = 0; t < 4 && !found; t++)
                    {
                        Fx off = t < 2 ? sep : sep + Fx.One;
                        Fx cand = (t & 1) == 0 ? x + off : x - off;
                        cand = Fx.Clamp(cand, ws / 2, screen - ws / 2);
                        if (Fx.Abs(cand - x) >= sep - Fx.FromRatio(5, 100) && Fx.Abs(cand - prevX) <= limS && CountInWindow(ctx.Plats, y) <= MaxPerWindow - 1)
                        {
                            xs = cand; found = true;
                        }
                    }
                    if (found)
                    {
                        ctx.Add(row, PlatformKind.Normal, xs, y, ws, Fx.Zero, 0, 0);
                        hiddenGemX = xs;
                        hiddenGemY = y + cfg.HiddenGemHeight;
                        data.HiddenGemOnSidePlatform = true;
                    }
                    else
                    {
                        hiddenGemX = x;
                        hiddenGemY = y + cfg.HiddenGemHeight;
                    }
                    hiddenGemPlaced = true;
                    data.HiddenGemRow = row;
                    data.HiddenGemX = hiddenGemX;
                }
                else if (rowPattern == 1)
                {
                    // Dense patch: 1 to 3 extra small platforms on the same row. The spine platform above stays reachable; these are
                    // choices, and about one in four crumbles, so the player has to read each jump.
                    int extras = 1 + rng.Next(3);
                    Fx[] usedX = new Fx[4];
                    Fx[] usedW = new Fx[4];
                    usedX[0] = x; usedW[0] = w;
                    int nUsed = 1;
                    for (int e = 0; e < extras; e++)
                    {
                        Fx w2 = rng.Range(Fx.FromRatio(15, 10), Fx.FromRatio(26, 10));
                        Fx x2 = rng.Range(w2 / 2, screen - w2 / 2);
                        bool free = true;
                        for (int u = 0; u < nUsed; u++) if (Fx.Abs(x2 - usedX[u]) < (w2 + usedW[u]) / 2 + Fx.FromRatio(6, 10)) free = false;
                        if (!free || !OptionalPlatformFits(ctx.Plats, y)) continue;
                        Platform ex = new Platform();
                        ex.Id = ctx.Plats.Count;
                        ex.Row = row;
                        ex.X = x2;
                        ex.Y = y;
                        ex.Width = w2;
                        ex.Kind = rng.Next(100) < 25 ? PlatformKind.Crumble : PlatformKind.Normal;
                        ctx.Plats.Add(ex);
                        usedX[nUsed] = x2; usedW[nUsed] = w2; nUsed++;
                    }
                }
                else
                {
                    // Optional second platform on the same row (easy levels give more choices), as in LevelGenerator.
                    int secondChance = (Fx.FromInt(50) * (Fx.One - d)).FloorToInt();
                    if (kind == PlatformKind.Normal && rng.Next(100) < secondChance && OptionalPlatformFits(ctx.Plats, y))
                    {
                        Fx x2 = screen - x;
                        if (Fx.Abs(x2 - x) >= (w + w) / 2 + Fx.One)
                        {
                            Platform extra = new Platform();
                            extra.Id = ctx.Plats.Count;
                            extra.Row = row;
                            extra.Kind = PlatformKind.Normal;
                            extra.X = Fx.Clamp(x2, lo, hi);
                            extra.Y = y;
                            extra.Width = w;
                            ctx.Plats.Add(extra);
                        }
                    }
                }

                prevX = x; prevW = w; prevKind = kind;
                rowNo++;
            }

            data.Platforms = ctx.Plats.ToArray();
            SnapPlatformWidths(cfg, data.Platforms);

            // ---- pickups ----
            data.Pickups = BuildPickups(cfg, spec, ctx, data.Platforms, rows, d, hiddenGemPlaced, hiddenGemX, hiddenGemY);

            // ---- hazards ----
            data.Hazards = BuildHazards(cfg, spec, ctx, rows, d, wave, waveStart, waveEnd, ladderStart, ladderLen);
            return data;
        }

        // ------------------------------------------------------------------------------------------
        /// <summary>Platform widths the art has real, unscaled pieces for (owner, 2026-10-23: platforms are never stretched). The 10 unit floor stays.</summary>
        private static readonly int[] NormalWidthsTenths = { 15, 20, 30, 40, 60, 80 };
        private static readonly int[] SpecialWidthsTenths = { 20, 40 };

        private static Fx SnapWidth(Fx w, PlatformKind kind)
        {
            if (w >= Fx.FromInt(9)) return w;
            bool special = kind == PlatformKind.Crumble || kind == PlatformKind.Moving || kind == PlatformKind.Bounce || kind == PlatformKind.BounceCap;
            int[] set = special ? SpecialWidthsTenths : NormalWidthsTenths;
            Fx best = Fx.FromRatio(set[0], 10);
            Fx bestDist = Fx.Abs(w - best);
            for (int i = 1; i < set.Length; i++)
            {
                Fx c = Fx.FromRatio(set[i], 10);
                Fx dist = Fx.Abs(w - c);
                if (dist <= bestDist) { best = c; bestDist = dist; }   // a tie goes to the wider piece
            }
            return best;
        }

        private static void SnapPlatformWidths(SimConfig cfg, Platform[] plats)
        {
            Fx screen = cfg.ScreenWidth;
            for (int i = 0; i < plats.Length; i++)
            {
                Fx w = SnapWidth(plats[i].Width, plats[i].Kind);
                plats[i].Width = w;
                Fx edge = w / 2 + (plats[i].Kind == PlatformKind.Moving ? plats[i].Amplitude : Fx.Zero);
                plats[i].X = Fx.Clamp(plats[i].X, edge, screen - edge);   // a wider piece (and its swing) must stay inside the walls
            }
        }

        // Pickups. Per row: path coins between the previous and this spine platform (probability
        // PathCoinPercent, 100 = exactly one per row), then a risky coin (edge side-route or high
        // overshoot route) with probability RiskyCoinPercent, half the time as a pair. Both
        // percentages blend easy to hard by d and scale with the level class. Roughly:
        //   coins per row = path(d) + 1.5 * risky(d)  ->  about 70 at d = 0.05 and 115 at d = 1 on 60 rows (class 0 to 2),
        //   x1.6 for complex levels, x1.2 for highly complex (which also have 150 rows).
        // A rare 1-gem pickup (GemChancePercent per level) sits on a risky edge route.
        // ------------------------------------------------------------------------------------------
        private static Pickup[] BuildPickups(SimConfig cfg, LevelSpec spec, Ctx ctx, Platform[] platforms, int rows, Fx d,
            bool hiddenGem, Fx hgx, Fx hgy)
        {
            Rng r = new Rng(spec.Seed ^ 0x2F1A7C35);
            Rng gemRng = new Rng(spec.Seed ^ 0x1B3A5E77);   // separate stream: gems never shift the star layout
            Fx screen = cfg.ScreenWidth;
            Fx wallMin = Fx.FromRatio(7, 10), wallMax = screen - Fx.FromRatio(7, 10);
            List<Pickup> list = new List<Pickup>(rows / 4 + 8);

            int gemRow = -1;
            if (r.Next(100) < cfg.GemChancePercent)
            {
                int lo = 4, hi = rows - 3 < 5 ? 5 : rows - 3;
                gemRow = r.Range(lo, hi);
            }

            // Economy v2: no coin pickups. Three stars on the way up (about a quarter, half and 85 percent of the level), each on a
            // risky spot (beside the platform at the wall, or above the jump path), plus occasional gems of different rarity.
            int[] starRows = { StarRow(rows, 25), StarRow(rows, 55), StarRow(rows, 85) };
            for (int i = 1; i < 3; i++) if (starRows[i] <= starRows[i - 1]) starRows[i] = starRows[i - 1] + 1;
            for (int row = 1; row <= rows; row++)
            {
                for (int si = 0; si < 3; si++)
                {
                    if (starRows[si] != row) continue;
                    if (r.Next(2) == 0) list.Add(MakePickup(cfg, PickupKind.Star, EdgeX(ctx, row, screen, wallMin, wallMax), ctx.Y[row] + Fx.FromRatio(12, 10), si));
                    else
                    {
                        Fx hx = Fx.Lerp(ctx.X[row - 1], ctx.X[row], Fx.FromRatio(6, 10));
                        list.Add(MakePickup(cfg, PickupKind.Star, Fx.Clamp(hx, wallMin, wallMax), ctx.Y[row] + Fx.FromRatio(18, 10), si));
                    }
                }
                if (row != gemRow && gemRng.Next(100) < cfg.GemInsteadOfCoinPercent)
                {
                    Fx px = Fx.Lerp(ctx.X[row - 1], ctx.X[row], Fx.FromRatio(5, 10));
                    Fx py = Fx.Lerp(ctx.Y[row - 1], ctx.Y[row], Fx.FromRatio(5, 10)) + Fx.FromRatio(12, 10);
                    list.Add(MakePickup(cfg, PickupKind.Gem, px, py, DrawRarity(cfg, gemRng)));
                }
                if (row == gemRow) list.Add(MakePickup(cfg, PickupKind.Gem, EdgeX(ctx, row, screen, wallMin, wallMax), ctx.Y[row] + Fx.FromRatio(12, 10), DrawRarity(cfg, gemRng)));
            }

            if (hiddenGem)
            {
                Pickup g = new Pickup();
                g.Kind = PickupKind.HiddenGem;
                g.X = hgx; g.Y = hgy; g.Radius = cfg.HiddenGemRadius;
                list.Add(g);
            }

            // Stars and gems are drawn where the Sim collects them (owner playtest 2026-10-20: a star drawn lifted off its real spot
            // needed two passes). So they must sit in clear air: any that would overlap a platform slab is moved up onto the free space above it.
            for (int i = 0; i < list.Count; i++)
            {
                Pickup pk = list[i];
                if (pk.Kind != PickupKind.Star && pk.Kind != PickupKind.Gem) continue;
                for (int pass = 0; pass < 8; pass++)
                {
                    Fx lift;
                    if (!PickupOverlapsPlatform(platforms, pk, out lift)) break;
                    pk.Y = lift;
                }
                list[i] = pk;
            }

            SeparateGems(list, platforms, cfg.ScreenWidth / 2);

            list.Sort(ComparePickup);
            Pickup[] arr = list.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i].Id = i;
            return arr;
        }

        /// <summary>Half height of the drawn pickup sprite used for the clear-air test, u (the largest gem is 0.65 wide).</summary>
        private static readonly Fx PickupClearHalf = Fx.FromRatio(7, 10);

        /// <summary>
        /// True when a pickup centre lies in the slab zone of a platform (from 0.7 + half sprite below its surface to 0.45 + half sprite above it,
        /// with 0.2 + 1.5 u sideways margin because moving platforms drift). <paramref name="liftY"/> is the first free height above that platform.
        /// </summary>
        private static bool PickupOverlapsPlatform(Platform[] platforms, Pickup pk, out Fx liftY)
        {
            Fx margin = Fx.FromRatio(17, 10);
            for (int i = 0; i < platforms.Length; i++)
            {
                Platform pl = platforms[i];
                if (Fx.Abs(pk.X - pl.X) > pl.Width / 2 + margin) continue;
                if (pk.Y > pl.Y - Fx.FromRatio(7, 10) - PickupClearHalf && pk.Y < pl.Y + Fx.FromRatio(45, 100) + PickupClearHalf)
                {
                    liftY = pl.Y + Fx.FromRatio(45, 100) + PickupClearHalf + Fx.FromRatio(1, 100);
                    return true;
                }
            }
            liftY = pk.Y;
            return false;
        }

        /// <summary>True when the pickup is in clear air (no platform slab zone around it), so the view can draw it at its Sim position.</summary>
        public static bool IsPickupClearOfPlatforms(Platform[] platforms, Pickup pk)
        {
            Fx lift;
            return !PickupOverlapsPlatform(platforms, pk, out lift);
        }

        private static int StarRow(int rows, int percent)
        {
            int row = rows * percent / 100;
            return row < 5 ? 5 : (row > rows - 2 ? rows - 2 : row);
        }

        private static int DrawRarity(SimConfig cfg, Rng rng)
        {
            int roll = rng.Next(100), acc = 0;
            for (int i = 0; i < cfg.GemRarityPercent.Length; i++)
            {
                acc += cfg.GemRarityPercent[i];
                if (roll < acc) return i;
            }
            return 0;
        }

        private static Fx EdgeX(Ctx ctx, int row, Fx screen, Fx wallMin, Fx wallMax)
        {
            bool left = ctx.X[row] < screen / 2;
            Fx ex = left ? ctx.X[row] - ctx.W[row] / 2 - Fx.FromRatio(7, 10) : ctx.X[row] + ctx.W[row] / 2 + Fx.FromRatio(7, 10);
            return Fx.Clamp(ex, wallMin, wallMax);
        }

        /// <summary>Stars, gems and hidden gems keep at least this distance (world units) from each other (owner report 2026-10-21: overlapping pickups).</summary>
        private static readonly Fx PickupMinGap = Fx.FromRatio(11, 10);

        /// <summary>
        /// A gem that sits on top of a star or another gem moves up in steps until the gap is wide enough (stars never move: they are the goal
        /// of the level, and the placement above stays clear of the platforms).
        /// </summary>
        private static void SeparateGems(List<Pickup> list, Platform[] platforms, Fx centreX)
        {
            Fx lo = Fx.FromRatio(12, 10), hi = centreX * 2 - Fx.FromRatio(12, 10);
            Fx step = Fx.FromRatio(6, 10);
            var drop = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                Pickup pk = list[i];
                if (pk.Kind != PickupKind.Gem || ConflictingPickup(list, i, pk) < 0) continue;
                // Nearest free spot on a grid around the gem: rings of growing size, each candidate clear of every platform and of every star and gem.
                bool found = false;
                for (int ring = 1; ring <= 12 && !found; ring++)
                {
                    for (int oy = -ring; oy <= ring && !found; oy++)
                    {
                        for (int ox = -ring; ox <= ring && !found; ox++)
                        {
                            if (System.Math.Max(System.Math.Abs(ox), System.Math.Abs(oy)) != ring) continue;
                            Pickup c = pk;
                            c.X = pk.X + step * ox;
                            c.Y = pk.Y + step * oy;
                            if (c.X < lo || c.X > hi) continue;
                            Fx lift;
                            if (PickupOverlapsPlatform(platforms, c, out lift)) continue;
                            if (ConflictingPickup(list, i, c) >= 0) continue;
                            pk = c;
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    // Crowded spot: accept a candidate that needs a lift off a platform, as long as it is clear of other pickups.
                    for (int ring = 1; ring <= 12 && !found; ring++)
                    {
                        for (int oy = -ring; oy <= ring && !found; oy++)
                        {
                            for (int ox = -ring; ox <= ring && !found; ox++)
                            {
                                if (System.Math.Max(System.Math.Abs(ox), System.Math.Abs(oy)) != ring) continue;
                                Pickup c = pk;
                                c.X = pk.X + step * ox;
                                c.Y = pk.Y + step * oy;
                                if (c.X < lo || c.X > hi) continue;
                                for (int pass = 0; pass < 8; pass++)
                                {
                                    Fx lift;
                                    if (!PickupOverlapsPlatform(platforms, c, out lift)) break;
                                    c.Y = lift;
                                }
                                if (PickupOverlapsPlatform(platforms, c, out _)) continue;
                                if (ConflictingPickup(list, i, c) >= 0) continue;
                                pk = c;
                                found = true;
                            }
                        }
                    }
                }
                if (!found) drop.Add(i);   // no clear spot in a crowded gap: the extra gem is dropped (stars and the hidden gem never are)
                list[i] = pk;
            }
            for (int k = drop.Count - 1; k >= 0; k--) list.RemoveAt(drop[k]);
        }

        /// <summary>Index of a star or gem within the minimum gap of <paramref name="pk"/>, or -1.</summary>
        private static int ConflictingPickup(List<Pickup> list, int self, Pickup pk)
        {
            Fx gapSq = PickupMinGap * PickupMinGap;
            for (int j = 0; j < list.Count; j++)
            {
                if (j == self) continue;
                Pickup o = list[j];
                if (o.Kind == PickupKind.Coin) continue;
                Fx dx = o.X - pk.X, dy = o.Y - pk.Y;
                if (dx * dx + dy * dy < gapSq) return j;
            }
            return -1;
        }

        private static Pickup MakePickup(SimConfig cfg, PickupKind kind, Fx x, Fx y, int tag)
        {
            Pickup p = new Pickup();
            p.Kind = kind;
            p.X = x; p.Y = y;
            p.Radius = kind == PickupKind.Star ? cfg.StarRadius : cfg.GemRadius;
            p.Tag = tag;
            return p;
        }

        private static Pickup MakeCoin(SimConfig cfg, PickupKind kind, Fx x, Fx y)
        {
            Pickup p = new Pickup();
            p.Kind = kind;
            p.X = x; p.Y = y;
            p.Radius = kind == PickupKind.Gem ? cfg.GemRadius : cfg.CoinRadius;
            return p;
        }

        private static void AddEdgeCoins(SimConfig cfg, List<Pickup> list, Ctx ctx, int row, Fx screen, Fx wallMin, Fx wallMax,
            bool pair, PickupKind kind)
        {
            bool left = ctx.X[row] < screen / 2;
            Fx ex = left ? ctx.X[row] - ctx.W[row] / 2 - Fx.FromRatio(7, 10) : ctx.X[row] + ctx.W[row] / 2 + Fx.FromRatio(7, 10);
            ex = Fx.Clamp(ex, wallMin, wallMax);
            Fx ey = ctx.Y[row] + Fx.FromRatio(12, 10);
            list.Add(MakeCoin(cfg, kind, ex, ey));
            if (pair)
            {
                Fx inward = left ? ex + Fx.FromRatio(5, 10) : ex - Fx.FromRatio(5, 10);
                list.Add(MakeCoin(cfg, kind, Fx.Clamp(inward, wallMin, wallMax), ey + Fx.FromRatio(9, 10)));
            }
        }

        private static int ComparePickup(Pickup a, Pickup b)
        {
            if (a.Y.Raw != b.Y.Raw) return a.Y.Raw < b.Y.Raw ? -1 : 1;
            if (a.X.Raw != b.X.Raw) return a.X.Raw < b.X.Raw ? -1 : 1;
            if (a.Kind != b.Kind) return (int)a.Kind < (int)b.Kind ? -1 : 1;
            return 0;
        }

        // ------------------------------------------------------------------------------------------
        // Hazards. Gongs hang in the jump corridor between two consecutive spine platforms, swing
        // sideways by GongAmplitude and rise a little at the swing extremes (pendulum).
        // ------------------------------------------------------------------------------------------
        private static Hazard[] BuildHazards(SimConfig cfg, LevelSpec spec, Ctx ctx, int rows, Fx d,
            bool wave, int waveStart, int waveEnd, int ladderStart, int ladderLen)
        {
            bool gongRegion = spec.LevelIndex >= 3 && spec.Signature == SignatureKind.SwingingGongs;
            if (!gongRegion && !wave) return new Hazard[0];
            Rng r = new Rng(spec.Seed ^ 0x5A3C9E17);
            Fx screen = cfg.ScreenWidth;
            List<Hazard> list = new List<Hazard>();
            for (int row = 2; row <= rows; row++)
            {
                if (ladderStart >= 0 && row >= ladderStart && row < ladderStart + ladderLen) continue;
                bool inWave = wave && row >= waveStart && row <= waveEnd;
                int chance = 0;
                if (inWave) chance = cfg.WaveDensityPercent;
                else if (gongRegion)
                {
                    chance = PercentLerp(cfg.GongDensityEasyPercent, cfg.GongDensityHardPercent, d);
                    if (spec.LevelIndex == 3) chance /= 2;
                }
                if (chance <= 0) continue;
                int count = 0;
                if (r.Next(100) < chance)
                {
                    count = 1;
                    if (inWave && r.Next(100) < chance / 2) count = 2;
                }
                int side = r.Next(2) == 0 ? -1 : 1;
                for (int k = 0; k < count; k++)
                {
                    Fx mx = (ctx.X[row - 1] + ctx.X[row]) / 2;
                    Fx my = (ctx.Y[row - 1] + ctx.Y[row]) / 2;
                    Hazard h = new Hazard();
                    h.Kind = HazardKind.Gong;
                    h.Radius = Fx.Lerp(cfg.GongRadiusEasy, cfg.GongRadiusHard, d);
                    h.Amplitude = r.Range(cfg.GongAmplitudeMin, cfg.GongAmplitudeMax);
                    Fx offset = r.Range(Fx.One, Fx.FromRatio(25, 10));
                    Fx ax = mx + offset * (k == 0 ? side : -side);
                    Fx edge = h.Amplitude + h.Radius;
                    if (edge * 2 < screen) ax = Fx.Clamp(ax, edge, screen - edge);
                    h.X = ax;
                    h.Y = my + Fx.Half;
                    h.Period = 2 * (inWave ? r.Range(55, 90) : r.Range(70, 120));
                    h.Phase = r.Next(h.Period);
                    list.Add(h);
                }
            }
            list.Sort(CompareHazard);
            Hazard[] arr = list.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i].Id = i;
            return arr;
        }

        private static int CompareHazard(Hazard a, Hazard b)
        {
            if (a.Y.Raw != b.Y.Raw) return a.Y.Raw < b.Y.Raw ? -1 : 1;
            if (a.X.Raw != b.X.Raw) return a.X.Raw < b.X.Raw ? -1 : 1;
            if (a.Period != b.Period) return a.Period < b.Period ? -1 : 1;
            if (a.Phase != b.Phase) return a.Phase < b.Phase ? -1 : 1;
            if (a.Amplitude.Raw != b.Amplitude.Raw) return a.Amplitude.Raw < b.Amplitude.Raw ? -1 : 1;
            if (a.Radius.Raw != b.Radius.Raw) return a.Radius.Raw < b.Radius.Raw ? -1 : 1;
            return 0;
        }
    }
}
