using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Creates a sprite renderer under the run world (RunView.NewSprite).</summary>
    public delegate SpriteRenderer SpriteFactory(string name, Sprite sprite, int order);

    /// <summary>
    /// Placeholder look of the seven region 1 special platforms (doc 24) until the OpenArt width sheets arrive: the normal platform view tinted
    /// per kind plus small code-drawn overlays, so they are playable and telegraphed. Every state also differs by SHAPE or MOTION, never by
    /// colour alone (CUX-6): wind arrows that flip (and blink before the flip), bees and an up/down arrow, a closing petal pair with a
    /// 3-pip countdown, a big diagonal arrow with drifting seeds, spikes (triangles) on the thorny half plus a warning triangle, a ladybug
    /// with speed lines when boosted, a tilting slab with slope arrows. With reduced motion nothing drifts, orbits or blinks (static poses).
    /// The real sprites later come from RegionArt.KindSprite (a lookup by PlatformKind); RunView falls back to the normal platform art and
    /// these overlays stay as the animation layer. No allocation per frame: parts are created when a platform view is set up and pooled.
    /// </summary>
    public sealed class SpecialPlatformFx
    {
        const int OverlayOrder = 3;   // above the platform (0) and its pattern strip (1), below pickups (5), gongs (6) and the player (10)

        readonly SpriteFactory make;
        readonly Sprite white;
        readonly bool reduced;
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        SpriteRenderer[][] parts = new SpriteRenderer[0][];
        bool[] painted = new bool[0];   // platform i has painted art (RegionArt.KindPieces): the overlays that would repeat the picture are hidden
        Sprite knob;                    // seesaw pivot knob sprite (own sprite: the leaf tilts, the knob stays) and its top distance below the surface, in baked pixels
        float knobTopDropPx;
        readonly Stack<SpriteRenderer[]>[] arrayPool = new Stack<SpriteRenderer[]>[16];   // released part arrays by length: steady-state play allocates nothing
        static Sprite triSprite, discSprite;

        public SpecialPlatformFx(SpriteFactory factory, Sprite whiteSquare, bool reducedMotion)
        {
            make = factory;
            white = whiteSquare;
            reduced = reducedMotion;
        }

        /// <summary>Seesaw pivot knob of the region art (null = the code-drawn fulcrum).</summary>
        public void SetKnob(Sprite sprite, float topDropPx)
        {
            knob = sprite;
            knobTopDropPx = topDropPx;
        }

        /// <summary>True for the platform kinds this class dresses.</summary>
        public static bool Handles(PlatformKind k) { return PlatformKinds.IsRegion1New(k); }

        /// <summary>Tint of the placeholder base platform (a colour hint only; every kind also has its own shape cue).</summary>
        public static Color Tint(PlatformKind k)
        {
            switch (k)
            {
                case PlatformKind.WindPetal: return new Color(1f, 0.82f, 0.92f, 1f);
                case PlatformKind.HiveLift: return new Color(1f, 0.86f, 0.45f, 1f);
                case PlatformKind.SnapDaisy: return new Color(1f, 1f, 0.78f, 1f);
                case PlatformKind.Dandelion: return new Color(0.96f, 0.96f, 0.75f, 1f);
                case PlatformKind.ThornBush: return new Color(0.55f, 0.78f, 0.5f, 1f);
                case PlatformKind.Ladybug: return new Color(1f, 0.6f, 0.55f, 1f);
                case PlatformKind.Seesaw: return new Color(0.72f, 1f, 0.72f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ sprites (procedural, 1 world unit)

        static Sprite Tri()
        {
            if (triSprite != null) return triSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float half = (1f - (y + 0.5f) / n) * 0.5f * n;       // apex at the top, full width at the bottom
                    bool inside = Mathf.Abs(x + 0.5f - n * 0.5f) <= half;
                    px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            triSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return triSprite;
        }

        static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    px[y * n + x] = dx * dx + dy * dy <= n * n * 0.25f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            discSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return discSprite;
        }

        // ------------------------------------------------------------------ part lists

        const int Wind = 3, Hive = 4, Snap = 5, Dande = 5, Ladybug = 7, Seesaw = 3, ThornExtra = 2, ThornMaxSpikes = 6;

        static int SpikeCount(float width)
        {
            int n = Mathf.RoundToInt(width * 0.5f / 0.5f);
            return Mathf.Clamp(n, 2, ThornMaxSpikes);
        }

        static int PartCount(Platform p)
        {
            switch (p.Kind)
            {
                case PlatformKind.WindPetal: return Wind;
                case PlatformKind.HiveLift: return Hive;
                case PlatformKind.SnapDaisy: return Snap;
                case PlatformKind.Dandelion: return Dande;
                case PlatformKind.ThornBush: return SpikeCount(p.Width.F()) + ThornExtra;
                case PlatformKind.Ladybug: return Ladybug;
                case PlatformKind.Seesaw: return Seesaw;
                default: return 0;
            }
        }

        /// <summary>Sprite of part k of a kind: triangles for arrows, spikes, petals and warnings, discs for bees, seeds and pips, a flat bar for lines.</summary>
        Sprite SpriteOf(PlatformKind kind, int k, int count)
        {
            switch (kind)
            {
                case PlatformKind.WindPetal: return Tri();
                case PlatformKind.HiveLift: return k < 2 ? Disc() : Tri();
                case PlatformKind.SnapDaisy: return k < 3 ? Disc() : Tri();
                case PlatformKind.Dandelion: return k == 0 ? Tri() : Disc();
                case PlatformKind.ThornBush: return k == count - 1 ? white : Tri();   // the last part is the exclamation bar, the one before it the warning triangle
                case PlatformKind.Ladybug: return k < 4 ? Disc() : white;
                case PlatformKind.Seesaw: return Tri();
                default: return white;
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public void Resize(int n)
        {
            if (parts.Length < n) System.Array.Resize(ref parts, n);
            if (painted.Length < n) System.Array.Resize(ref painted, n);
        }

        /// <param name="hasArt">True when RunView drew the painted piece of this platform: only the telegraph overlays the art does not provide stay visible.</param>
        public void Attach(int i, Platform p, bool hasArt)
        {
            Resize(i + 1);
            Release(i);
            painted[i] = hasArt;
            int n = PartCount(p);
            if (n == 0) return;
            if (arrayPool[n] == null) arrayPool[n] = new Stack<SpriteRenderer[]>();
            SpriteRenderer[] list = arrayPool[n].Count > 0 ? arrayPool[n].Pop() : new SpriteRenderer[n];
            for (int k = 0; k < n; k++)
            {
                Sprite sp = SpriteOf(p.Kind, k, n);
                if (hasArt && p.Kind == PlatformKind.Seesaw && k == 0 && knob != null) sp = knob;   // the painted pivot knob replaces the code fulcrum
                SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : make("fx" + p.Id + "_" + k, sp, OverlayOrder);
                sr.gameObject.SetActive(true);
                sr.sprite = sp;
                sr.sortingOrder = OverlayOrder;
                sr.color = Color.white;
                sr.transform.rotation = Quaternion.identity;
                list[k] = sr;
            }
            parts[i] = list;
        }

        public void Release(int i)
        {
            if (i >= parts.Length || parts[i] == null) return;
            SpriteRenderer[] list = parts[i];
            for (int k = 0; k < list.Length; k++)
            {
                list[k].gameObject.SetActive(false);
                pool.Push(list[k]);
            }
            parts[i] = null;
            painted[i] = false;
            arrayPool[list.Length].Push(list);
        }

        // ------------------------------------------------------------------ per frame

        static void Put(SpriteRenderer sr, float x, float y, float sx, float sy, float rotZ, Color c)
        {
            Transform t = sr.transform;
            t.position = new Vector3(x, y, 0f);
            t.localScale = new Vector3(sx, sy, 1f);
            t.rotation = Quaternion.Euler(0f, 0f, rotZ);
            sr.color = c;
        }

        static readonly Color Hidden = new Color(1f, 1f, 1f, 0f);

        /// <summary>Hides a part the painted art already shows (bees, petal pair, spikes, ladybug body, fluffy head).</summary>
        static void Hide(SpriteRenderer sr) { sr.color = Hidden; }

        static float ArrowRot(int dir) { return dir >= 0 ? -90f : 90f; }   // the triangle points up; right = clockwise

        /// <summary>
        /// Positions and animates the parts of platform i. <paramref name="x"/> / <paramref name="y"/> are the current centre and surface height
        /// from the Sim (the Ladybug path and the Bee Hive bob included); <paramref name="baseSr"/> is the normal platform view.
        /// </summary>
        public void Update(int i, Platform p, Sim sim, in SimState s, int tick, float x, float y, SpriteRenderer baseSr)
        {
            if (i >= parts.Length || parts[i] == null) return;
            SpriteRenderer[] ps = parts[i];
            float w = p.Width.F();
            switch (p.Kind)
            {
                case PlatformKind.WindPetal:
                    {
                        int dir = sim.WindDirection(p.Id, tick);
                        bool cue = sim.WindFlipCue(p.Id, tick);
                        int shown = cue ? -dir : dir;   // the arrows flip half a second before the gust does
                        float a = cue ? (reduced ? 0.55f : 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(tick * 0.45f))) : 0.95f;
                        for (int k = 0; k < Wind; k++)
                        {
                            float drift = reduced ? 0f : (((tick * 0.012f + k * 0.33f) % 1f) - 0.5f) * 0.5f * shown;
                            Put(ps[k], x + (k - 1) * 0.3f * Mathf.Min(w, 4f) + drift, y + 0.42f, 0.34f, 0.4f, ArrowRot(shown), new Color(1f, 1f, 1f, a));
                        }
                        break;
                    }
                case PlatformKind.HiveLift:
                    {
                        float rx = Mathf.Max(0.6f, w * 0.5f + 0.1f);
                        for (int k = 0; k < 2; k++)
                        {
                            float ang = reduced ? k * Mathf.PI : tick * 0.12f + k * Mathf.PI;
                            Put(ps[k], x + Mathf.Cos(ang) * rx, y + 0.55f + Mathf.Sin(ang) * 0.22f, 0.2f, 0.2f, 0f, new Color(1f, 0.85f, 0.1f, 1f));
                        }
                        Put(ps[2], x, y + 0.66f, 0.22f, 0.22f, 0f, Color.white);     // up
                        Put(ps[3], x, y + 0.40f, 0.22f, 0.22f, 180f, Color.white);   // down
                        if (painted[i]) { Hide(ps[0]); Hide(ps[1]); }                  // the painted hive has no bees: only the up / down arrows stay
                        break;
                    }
                case PlatformKind.SnapDaisy:
                    {
                        int[] ct = s.CrumbleTick;
                        int phase = sim.SnapPhase(p.Id, tick, ct);
                        int count = sim.SnapCountdown(p.Id, tick, ct);
                        float prog = phase == 2 ? 1f : (phase == 1 ? Mathf.Clamp01((tick - ct[p.Id]) / (float)sim.Cfg.SnapCloseTicks) : 0f);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = phase == 2 ? 0f : (phase == 1 ? (k < count ? 1f : 0.2f) : 0.3f);
                            Put(ps[k], x + (k - 1) * 0.28f, y + 0.72f, 0.16f, 0.16f, 0f, new Color(1f, 1f, 1f, a));
                        }
                        float open = Mathf.Max(0.15f, w * 0.5f - 0.3f);
                        float off = Mathf.Lerp(open, 0.12f, prog);
                        Put(ps[3], x - off, y + 0.3f, 0.5f, 0.5f, -90f, new Color(1f, 1f, 1f, 0.95f));   // left petal points to the middle
                        Put(ps[4], x + off, y + 0.3f, 0.5f, 0.5f, 90f, new Color(1f, 1f, 1f, 0.95f));
                        if (baseSr != null) { Color c = baseSr.color; c.a = phase == 2 ? 0.25f : 1f; baseSr.color = c; }   // shut: you fall through
                        if (painted[i]) { Hide(ps[3]); Hide(ps[4]); }                  // the painted flower row replaces the petal pair; the countdown pips stay
                        break;
                    }
                case PlatformKind.Dandelion:
                    {
                        int dir = p.Dir == 0 ? 1 : p.Dir;
                        Put(ps[0], x + dir * 0.55f, y + 0.95f, 0.55f, 0.55f, dir > 0 ? -45f : 45f, new Color(1f, 0.6f, 0.15f, 1f));   // the big arrow
                        for (int k = 1; k < Dande; k++)
                        {
                            float t = reduced ? 0.25f * k : ((tick * 0.02f + k * 0.31f) % 1f);
                            if (k == 1) Put(ps[k], x, y + 0.4f, 0.55f, 0.55f, 0f, new Color(0.97f, 0.97f, 0.85f, 1f));   // the fluffy head
                            else Put(ps[k], x + dir * t * 1.3f, y + 0.5f + t * 0.9f, 0.12f, 0.12f, 0f, new Color(1f, 1f, 1f, reduced ? 0.8f : 1f - t));
                        }
                        if (painted[i]) Hide(ps[1]);                                   // the painted puffs are the fluffy head; arrow and drifting seeds stay
                        break;
                    }
                case PlatformKind.ThornBush:
                    {
                        int dir = p.Dir == 0 ? 1 : p.Dir;
                        int spikes = ps.Length - ThornExtra;
                        float half = w * 0.5f - 0.15f;
                        for (int k = 0; k < spikes; k++)
                        {
                            float cx = x + dir * (0.15f + (k + 0.5f) * half / spikes);
                            Put(ps[k], cx, y + 0.28f, Mathf.Min(0.42f, half / spikes), 0.5f, 0f, new Color(0.45f, 0.1f, 0.12f, 1f));
                        }
                        float wx = x + dir * (w * 0.25f);
                        bool blink = !reduced && (tick / 20) % 2 == 0;
                        Put(ps[spikes], wx, y + 1.0f, 0.46f, 0.42f, 0f, blink ? new Color(1f, 0.95f, 0.2f, 1f) : new Color(1f, 0.8f, 0.1f, 1f));   // warning triangle
                        Put(ps[spikes + 1], wx, y + 0.95f, 0.06f, 0.17f, 0f, new Color(0.15f, 0.08f, 0.02f, 1f));                                   // exclamation bar
                        if (painted[i]) for (int k = 0; k < spikes; k++) Hide(ps[k]);   // the painted bush has its own thorns: warning icon stays
                        break;
                    }
                case PlatformKind.Ladybug:
                    {
                        float x1 = sim.PlatformX(p.Id, tick + 1, s.CrumbleTick).F();
                        float face = x1 > x + 0.0001f ? 1f : (x1 < x - 0.0001f ? -1f : 1f);
                        Put(ps[0], x, y + 0.32f, 0.58f, 0.46f, 0f, new Color(0.9f, 0.08f, 0.08f, 1f));
                        Put(ps[1], x + face * 0.3f, y + 0.34f, 0.24f, 0.24f, 0f, new Color(0.1f, 0.1f, 0.12f, 1f));
                        Put(ps[2], x - 0.08f, y + 0.4f, 0.11f, 0.11f, 0f, new Color(0.1f, 0.1f, 0.12f, 1f));
                        Put(ps[3], x + 0.1f, y + 0.28f, 0.11f, 0.11f, 0f, new Color(0.1f, 0.1f, 0.12f, 1f));
                        bool boost = sim.LadybugBoosted(s, p.Id);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = boost ? 0.85f : 0f;
                            Put(ps[4 + k], x - face * (0.55f + 0.22f * k), y + 0.2f + 0.1f * k, 0.32f, 0.04f, 0f, new Color(1f, 1f, 1f, a));
                        }
                        if (painted[i])
                        {
                            for (int k = 0; k < 4; k++) Hide(ps[k]);                   // the painted ladybug rider replaces the code body; speed lines stay
                            if (baseSr != null) baseSr.flipX = face > 0f;              // the art looks left: mirror it when the bug runs right
                        }
                        break;
                    }
                case PlatformKind.Seesaw:
                    {
                        int tilt = sim.SeesawTilt(s, p.Id);
                        float maxAngle = Mathf.Atan2(0.25f, Mathf.Max(0.5f, w * 0.5f)) * Mathf.Rad2Deg;   // a 0.25 u dip at the low end
                        float ease = tilt == 0 ? 0f : (reduced ? 1f : Mathf.Clamp01((tick - s.LandTick) / 8f));
                        if (baseSr != null) baseSr.transform.rotation = Quaternion.Euler(0f, 0f, -tilt * maxAngle * ease);
                        if (painted[i] && knob != null)
                        {
                            // The painted leaf tilts, the painted knob (own sprite, uniform scale of the leaf) stays under its middle.
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float kppu = knob.pixelsPerUnit;
                            float kh = knob.rect.height / kppu * ks;
                            Put(ps[0], x, y - knobTopDropPx / kppu * ks - kh * 0.5f, ks, ks, 0f, Color.white);
                        }
                        else Put(ps[0], x, y - 0.5f, 0.5f, 0.32f, 0f, new Color(0.45f, 0.3f, 0.15f, 1f));   // fulcrum
                        for (int k = 0; k < 2; k++)
                        {
                            int side = k == 0 ? -1 : 1;
                            int shown = tilt != 0 ? tilt : side;   // flat: arrows point outward and dim; tilted: both point down the slope
                            float a = tilt != 0 ? 0.95f : 0.35f;
                            Put(ps[1 + k], x + side * Mathf.Max(0.5f, w * 0.5f - 0.45f), y + 0.5f, 0.3f, 0.34f, ArrowRot(shown), new Color(1f, 1f, 1f, a));
                        }
                        break;
                    }
            }
        }
    }
}
