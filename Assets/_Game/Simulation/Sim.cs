namespace SkyHopper.Simulation
{
    /// <summary>Input bit flags for one tick. Jump is an edge: set it only on the tick of the tap.</summary>
    public static class SimInput
    {
        public const int Left = 1;
        public const int Right = 2;
        public const int Jump = 4;
    }

    /// <summary>All mutable simulation state. Copy with Clone() (the CrumbleTick and PickupTaken arrays are the only reference members).</summary>
    public struct SimState
    {
        public int Tick;
        public Fx X, Y, Vx, Vy;
        public bool Grounded;
        /// <summary>Platform last stood on (kept while in coyote time and while airborne).</summary>
        public int GroundId;
        public int Coyote;
        public int JumpBuffer;
        public int JumpStartRow;
        /// <summary>Highest platform row landed on.</summary>
        public int Row;
        public int Combo, BestCombo, Score, WallBounces;
        public Fx KillY, MaxY;
        /// <summary>Tick of the first jump of the run, or -1 before it. The screen starts scrolling then (owner, 2026-10-19).</summary>
        public int StartTick;
        public bool Dead;
        /// <summary>Per platform: tick of first landing on a crumble platform, or -1.</summary>
        public int[] CrumbleTick;

        // ---- T3 run tracking (plain value fields except the PickupTaken array) ----
        /// <summary>Coins collected this run (landing rules plus pickups). Not part of Score.</summary>
        public int Coins;
        /// <summary>Bits of the collectible stars taken (bit i = star i) and how many.</summary>
        public int StarMask, Stars;
        public int Gems;
        /// <summary>Hidden gems collected (FindHiddenGem goal). Counted separately from Gems.</summary>
        public int HiddenGems;
        /// <summary>Gong hits taken.</summary>
        public int Hits;
        /// <summary>Remaining ticks of hit/revive invulnerability.</summary>
        public int Invuln;
        public int RevivesUsed;
        /// <summary>Platform id of the highest row landed on (revive point).</summary>
        public int BestGroundId;
        /// <summary>Floors gained by the most recent landing, measured from the platform jumped from.</summary>
        public int LastJumpFloors, BestJumpFloors;
        /// <summary>
        /// Histogram of landing jumps by floors gained: 8 buckets of 8 bits (bucket k-1 = jumps that gained k floors,
        /// the last bucket also counts 8 or more; saturates at 255). Read it with GoalEvaluator.JumpsWithFloors.
        /// </summary>
        public ulong JumpHist;
        /// <summary>Per pickup: collected flag (index = pickup id).</summary>
        public bool[] PickupTaken;

        public SimState Clone()
        {
            SimState c = this;
            if (CrumbleTick != null) c.CrumbleTick = (int[])CrumbleTick.Clone();
            if (PickupTaken != null) c.PickupTaken = (bool[])PickupTaken.Clone();
            return c;
        }
    }

    /// <summary>
    /// Deterministic fixed-step simulation of one run. Stateless apart from the level: all mutable
    /// state lives in SimState so replays, bots and the server can branch and re-simulate cheaply.
    /// Platforms must be sorted by Y ascending (the generator guarantees this); pickups and hazards too.
    /// Step never allocates.
    /// </summary>
    public sealed class Sim
    {
        public readonly SimConfig Cfg;
        public readonly LevelData Level;
        public readonly Platform[] Platforms;
        public readonly Hazard[] Hazards;
        public readonly Pickup[] Pickups;
        public readonly bool Endless;
        /// <summary>Campaign scroll ramp (BossChase): endless step formula clamped to SimConfig.CampaignRampCapPercent.</summary>
        public readonly bool CampaignRamp;
        private readonly Fx scrollPerTick;
        private readonly Fx maxPickupRadius;

        /// <param name="difficulty">0..1 level difficulty; scales the scroll pressure.</param>
        public Sim(SimConfig cfg, Platform[] platforms, Fx difficulty, bool endless)
            : this(cfg, LevelData.FromPlatforms(platforms), difficulty, endless, false)
        {
        }

        /// <summary>Campaign constructor: level from LevelBuilder, difficulty and ramp taken from the spec.</summary>
        public Sim(SimConfig cfg, LevelData level, LevelSpec spec)
            : this(cfg, level, spec.Difficulty, false, spec.Ramp)
        {
        }

        public Sim(SimConfig cfg, LevelData level, Fx difficulty, bool endless, bool campaignRamp)
        {
            Cfg = cfg;
            Level = level;
            Platforms = level.Platforms;
            Hazards = level.Hazards;
            Pickups = level.Pickups;
            Endless = endless;
            CampaignRamp = campaignRamp;
            scrollPerTick = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, difficulty) * cfg.ScrollBasePercent / 100 / SimConfig.TicksPerSecond;
            Fx maxR = Fx.Zero;
            for (int i = 0; i < Pickups.Length; i++) if (Pickups[i].Radius > maxR) maxR = Pickups[i].Radius;
            maxPickupRadius = maxR;
        }

        public SimState NewState()
        {
            Platform start = Platforms[0];
            SimState s = new SimState();
            s.X = start.X;
            s.Y = start.Y;
            s.Grounded = true;
            s.GroundId = 0;
            s.BestGroundId = 0;
            s.Coyote = Cfg.CoyoteTicks;
            s.MaxY = start.Y;
            s.StartTick = -1;
            s.KillY = start.Y - Cfg.StartKillDistance;
            s.CrumbleTick = new int[Platforms.Length];
            for (int i = 0; i < s.CrumbleTick.Length; i++) s.CrumbleTick[i] = -1;
            s.PickupTaken = new bool[Pickups.Length];
            return s;
        }

        /// <summary>
        /// Endless mode: after the tower grew (a new Sim was built from the longer level, same prefix), extends the per-platform and
        /// per-pickup arrays of a state taken from the shorter one. Allocates, so call it once per growth step, never per tick.
        /// </summary>
        public void GrowState(ref SimState s)
        {
            if (s.CrumbleTick == null || s.CrumbleTick.Length < Platforms.Length)
            {
                int[] c = new int[Platforms.Length];
                int have = s.CrumbleTick == null ? 0 : s.CrumbleTick.Length;
                for (int i = 0; i < c.Length; i++) c[i] = i < have ? s.CrumbleTick[i] : -1;
                s.CrumbleTick = c;
            }
            if (s.PickupTaken == null || s.PickupTaken.Length < Pickups.Length)
            {
                bool[] p = new bool[Pickups.Length];
                if (s.PickupTaken != null) System.Array.Copy(s.PickupTaken, p, s.PickupTaken.Length);
                s.PickupTaken = p;
            }
        }

        /// <summary>
        /// Minimal revive: if dead, puts the player back on the highest platform landed on, clears speed,
        /// grants a short invulnerability and counts the revive. UI and payment are the caller's job.
        /// </summary>
        public void Revive(ref SimState s)
        {
            if (!s.Dead) return;
            Platform p = Platforms[s.BestGroundId];
            s.Dead = false;
            s.GroundId = s.BestGroundId;
            s.X = PlatformX(s.BestGroundId, s.Tick);
            s.Y = p.Y;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            s.Coyote = Cfg.CoyoteTicks;
            s.JumpBuffer = 0;
            s.JumpStartRow = p.Row;
            s.MaxY = p.Y;
            s.KillY = p.Y - Cfg.ReviveKillDistance;
            if (s.StartTick < 0) s.StartTick = s.Tick;
            s.Invuln = Cfg.ReviveInvulnTicks;
            s.Combo = 0;
            s.RevivesUsed++;
        }

        /// <summary>Position of a hazard at a tick (triangle wave on X, small pendulum rise at the extremes).</summary>
        public void HazardPosition(int id, int tick, out Fx x, out Fx y)
        {
            Hazard h = Hazards[id];
            int half = h.Period / 2;
            int phase = (tick + h.Phase) % h.Period;
            int tri = phase < half ? phase : h.Period - phase;
            Fx sway = Fx.FromRatio(tri, half) * 2 - Fx.One;
            x = h.X + sway * h.Amplitude;
            y = h.Y + Fx.Abs(sway) * h.Amplitude * Cfg.GongArcRisePercent / 100;
        }

        public Fx PlatformX(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.Moving) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx t = Fx.FromRatio(tri, half);
            return p.X + (t * 2 - Fx.One) * p.Amplitude;
        }

        public bool IsSolid(int id, int tick, int[] crumbleTick)
        {
            Platform p = Platforms[id];
            if (p.Kind == PlatformKind.Vanishing)
            {
                return (tick + p.Phase) % (Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks) < Cfg.VanishVisibleTicks;
            }
            if (p.Kind == PlatformKind.Crumble)
            {
                int landed = crumbleTick[id];
                return landed < 0 || tick - landed < Cfg.CrumbleTicks;
            }
            return true;
        }

        /// <summary>Index of the first platform whose Y is at or above y.</summary>
        private int LowerBound(Fx y)
        {
            int lo = 0, hi = Platforms.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Platforms[mid].Y < y) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private Fx CurrentMaxSpeed(int row)
        {
            Fx max = Cfg.MaxSpeed;
            if (Endless)
            {
                int pct = Cfg.RampPercentAt(row) * Cfg.PlayerSpeedSharePercent / 100;
                max = max * (100 + pct) / 100;
            }
            return max;
        }

        public void Step(ref SimState s, int input)
        {
            if (s.Dead) return;
            int tick = s.Tick + 1;
            int dir = ((input & SimInput.Right) != 0 ? 1 : 0) - ((input & SimInput.Left) != 0 ? 1 : 0);
            Fx maxSpeed = CurrentMaxSpeed(s.Row);

            // --- horizontal acceleration ---
            PlatformKind surface = Platforms[s.GroundId].Kind;
            Fx accel = Cfg.Accel;
            Fx friction = Cfg.Friction;
            if (!s.Grounded)
            {
                accel = accel * Cfg.AirControl;
            }
            else if (surface == PlatformKind.Slippery)
            {
                accel = accel * Cfg.SlipperyAccel;
                friction = friction * Cfg.SlipperyFriction;
            }
            if (dir != 0)
            {
                Fx a = accel;
                if (s.Vx.Sign() != 0 && s.Vx.Sign() != dir) a = a * 2;
                s.Vx = s.Vx + a * dir;
            }
            else if (s.Grounded)
            {
                if (s.Vx > friction) s.Vx = s.Vx - friction;
                else if (s.Vx < -friction) s.Vx = s.Vx + friction;
                else s.Vx = Fx.Zero;
            }
            s.Vx = Fx.Clamp(s.Vx, -maxSpeed, maxSpeed);

            // --- jump ---
            if ((input & SimInput.Jump) != 0) s.JumpBuffer = Cfg.JumpBufferTicks;
            if (s.JumpBuffer > 0 && (s.Grounded || s.Coyote > 0))
            {
                Fx v0 = Cfg.JumpSpeed(Fx.Abs(s.Vx));
                if (surface == PlatformKind.Sticky) v0 = v0 * Cfg.StickyJumpMultiplier;
                s.Vy = v0;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpBuffer = 0;
                s.JumpStartRow = Platforms[s.GroundId].Row;
                if (s.StartTick < 0) s.StartTick = tick;
            }

            // --- movement ---
            Fx prevY = s.Y;
            Fx prevX = s.X;
            if (s.Grounded)
            {
                if (Platforms[s.GroundId].Kind == PlatformKind.Moving)
                {
                    s.X = s.X + PlatformX(s.GroundId, tick) - PlatformX(s.GroundId, s.Tick);
                }
                s.X = s.X + s.Vx;
            }
            else
            {
                s.Vy = s.Vy - Cfg.Gravity;
                s.Y = s.Y + s.Vy;
                s.X = s.X + s.Vx;
            }

            // --- walls ---
            Fx minX = Cfg.PlayerHalfWidth;
            Fx maxX = Cfg.ScreenWidth - Cfg.PlayerHalfWidth;
            if (s.X < minX)
            {
                s.X = minX;
                if (s.Vx < Fx.Zero) { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
            }
            else if (s.X > maxX)
            {
                s.X = maxX;
                if (s.Vx > Fx.Zero) { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
            }

            // --- ground / landing ---
            if (s.Grounded)
            {
                Platform g = Platforms[s.GroundId];
                bool solid = IsSolid(s.GroundId, tick, s.CrumbleTick);
                if (!solid || Fx.Abs(s.X - PlatformX(s.GroundId, tick)) > g.Width / 2 + Cfg.LandingMargin)
                {
                    s.Grounded = false;
                    s.Vy = Fx.Zero;
                    s.Coyote = Cfg.CoyoteTicks;
                    s.JumpStartRow = g.Row;
                }
            }
            else
            {
                if (s.Coyote > 0) s.Coyote--;
                if (s.Vy < Fx.Zero)
                {
                    int best = -1;
                    for (int i = LowerBound(s.Y); i < Platforms.Length && Platforms[i].Y <= prevY; i++)
                    {
                        Platform p = Platforms[i];
                        if (!IsSolid(i, tick, s.CrumbleTick)) continue;
                        if (Fx.Abs(s.X - PlatformX(i, tick)) > p.Width / 2 + Cfg.LandingMargin) continue;
                        if (best < 0 || p.Y > Platforms[best].Y) best = i;
                    }
                    if (best >= 0) Land(ref s, best, tick);
                }
            }

            // --- pickups and hazards ---
            if (Pickups.Length > 0) CollectPickups(ref s, prevX, prevY + Cfg.PlayerCenterY);
            if (Hazards.Length > 0) HitHazards(ref s, tick);

            // --- scroll pressure and death ---
            if (s.Y > s.MaxY) s.MaxY = s.Y;
            if (s.StartTick >= 0)
            {
                Fx scroll = scrollPerTick;
                // The scroll speed steps up every ScrollStepSeconds of play (Cfg.ScrollStepPercent each, capped).
                int steps = (tick - s.StartTick) / (Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
                int stepPct = CampaignRamp ? 0 : steps * Cfg.ScrollStepPercent;   // boss levels already ramp with the row
                if (stepPct > Cfg.ScrollStepCapPercent) stepPct = Cfg.ScrollStepCapPercent;
                scroll = scroll * (100 + stepPct) / 100;
                if (Endless)
                {
                    scroll = scroll * (100 + Cfg.RampPercentAt(s.Row)) / 100;
                }
                else if (CampaignRamp)
                {
                    int pct = Cfg.RampPercentAt(s.Row);
                    if (pct > Cfg.CampaignRampCapPercent) pct = Cfg.CampaignRampCapPercent;
                    scroll = scroll * (100 + pct) / 100;
                }
                s.KillY = s.KillY + scroll;
            }
            Fx follow = s.MaxY - Cfg.KillDistance;
            if (follow > s.KillY) s.KillY = follow;
            if (s.Y < s.KillY) s.Dead = true;

            if (s.JumpBuffer > 0) s.JumpBuffer--;
            if (s.Invuln > 0) s.Invuln--;
            s.Tick = tick;
        }

        private static long Dist2(Fx ax, Fx ay, Fx bx, Fx by)
        {
            long dx = (long)ax.Raw - bx.Raw;
            long dy = (long)ay.Raw - by.Raw;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Squared distance from point (px, py) to the segment a-b, in raw Q16.16 units squared. The foot parameter is computed in Q16 integer
        /// math, so the result is exact on every platform.
        /// </summary>
        private static long SegmentDist2(Fx ax, Fx ay, Fx bx, Fx by, Fx px, Fx py)
        {
            long dx = (long)bx.Raw - ax.Raw, dy = (long)by.Raw - ay.Raw;
            long dd = dx * dx + dy * dy;
            if (dd == 0) return Dist2(ax, ay, px, py);
            long dot = ((long)px.Raw - ax.Raw) * dx + ((long)py.Raw - ay.Raw) * dy;
            if (dot <= 0) return Dist2(ax, ay, px, py);
            if (dot >= dd) return Dist2(bx, by, px, py);
            long t = (dot << 16) / dd;   // 0..65536
            long fx = ax.Raw + ((dx * t) >> 16), fy = ay.Raw + ((dy * t) >> 16);
            long ex = fx - px.Raw, ey = fy - py.Raw;
            return ex * ex + ey * ey;
        }

        /// <summary>
        /// Collects every pickup the player centre touched during this tick: the path from the previous tick position
        /// (prevCx, prevCy) to the current one is tested against the pickup circle, so a fast pass cannot jump over a star between ticks.
        /// </summary>
        private void CollectPickups(ref SimState s, Fx prevCx, Fx prevCy)
        {
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Pickups.Length;
            Fx lowY = Fx.Min(cy, prevCy), highY = Fx.Max(cy, prevCy);
            Fx from = lowY - maxPickupRadius;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Pickups[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = highY + maxPickupRadius;
            for (int i = lo; i < Pickups.Length && Pickups[i].Y <= to; i++)
            {
                if (s.PickupTaken[i]) continue;
                long r = Pickups[i].Radius.Raw;
                if (SegmentDist2(prevCx, prevCy, cx, cy, Pickups[i].X, Pickups[i].Y) > r * r) continue;
                s.PickupTaken[i] = true;
                switch (Pickups[i].Kind)
                {
                    case PickupKind.Coin: s.Coins++; break;
                    case PickupKind.Star: s.StarMask |= 1 << Pickups[i].Tag; s.Stars++; break;
                    case PickupKind.Gem:
                        {
                            int tier = Pickups[i].Tag < 0 ? 0 : (Pickups[i].Tag >= Cfg.GemValues.Length ? Cfg.GemValues.Length - 1 : Pickups[i].Tag);
                            s.Gems += Cfg.GemValues[tier];
                            break;
                        }
                    default: s.HiddenGems++; break;
                }
            }
        }

        /// <summary>Largest vertical distance between a hazard anchor and any point the hazard or the player can occupy while overlapping.</summary>
        private static readonly Fx HazardQueryMargin = Fx.FromInt(4);

        private void HitHazards(ref SimState s, int tick)
        {
            if (s.Invuln > 0) return;
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Hazards.Length;
            Fx from = cy - HazardQueryMargin;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Hazards[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = cy + HazardQueryMargin;
            for (int i = lo; i < Hazards.Length && Hazards[i].Y <= to; i++)
            {
                Fx gx, gy;
                HazardPosition(i, tick, out gx, out gy);
                long r = (long)Hazards[i].Radius.Raw + Cfg.PlayerHitRadius.Raw;
                if (Dist2(cx, cy, gx, gy) > r * r) continue;
                s.Vx = cx >= gx ? Cfg.GongKnockbackVx : -Cfg.GongKnockbackVx;
                if (!s.Grounded) s.Vy = cy >= gy ? Cfg.GongKnockbackVy : -Cfg.GongKnockbackVy;
                s.Invuln = Cfg.GongInvulnTicks;
                s.Hits++;
                return;
            }
        }

        private void Land(ref SimState s, int id, int tick)
        {
            Platform p = Platforms[id];
            s.Y = p.Y;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            s.GroundId = id;
            s.Coyote = Cfg.CoyoteTicks;

            int oldRow = s.Row;
            int gained = p.Row - s.JumpStartRow;
            bool newRow = p.Row > oldRow;
            bool qualifies = gained >= Cfg.ComboMinFloors;
            bool continuesCombo = qualifies && s.Combo > 0;

            if (newRow)
            {
                s.Score += (p.Row - oldRow) * Cfg.FloorPoints;
                s.Row = p.Row;
                s.BestGroundId = id;
            }
            if (qualifies)
            {
                s.Combo++;
                if (s.Combo > s.BestCombo) s.BestCombo = s.Combo;
                s.Score += s.Combo * s.Combo * Cfg.ComboPointsMultiplier;
            }
            else
            {
                s.Combo = 0;
            }

            // Doc 08 section 7 coin rules (campaign only; endless has its own rule, D3).
            if (newRow && !Endless)
            {
                s.Coins += continuesCombo ? Cfg.ComboLandingCoins : Cfg.LandingCoins;
                int newFloors = p.Row - oldRow;
                int extra = (gained < newFloors ? gained : newFloors) - 1;
                if (extra > 0) s.Coins += extra * Cfg.ExtraFloorCoins;
            }

            s.LastJumpFloors = gained;
            if (gained > s.BestJumpFloors) s.BestJumpFloors = gained;
            if (gained >= 1)
            {
                int shift = (gained > 8 ? 7 : gained - 1) * 8;
                if (((s.JumpHist >> shift) & 0xFFUL) < 255UL) s.JumpHist += 1UL << shift;
            }

            if (p.Kind == PlatformKind.Crumble && s.CrumbleTick[id] < 0) s.CrumbleTick[id] = tick;
            if (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap)
            {
                Fx mul = p.Kind == PlatformKind.Bounce ? Cfg.BounceMultiplier : Cfg.BounceCapMultiplier;
                s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * mul;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpStartRow = p.Row;
            }
        }
    }
}
