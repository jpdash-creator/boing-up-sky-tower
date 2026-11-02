using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Creates a sprite renderer under the run world (RunView.NewSprite).</summary>
    public delegate SpriteRenderer SpriteFactory(string name, Sprite sprite, int order);

    /// <summary>
    /// Presentation of the region 1 to 3 special platforms (doc 24, doc 30). Owner decision 2026-11-01: platform behaviour is discovered by the player, so NO
    /// code-drawn cues are drawn on any platform (no arrows, bees, pips, warning icons, speed lines). What stays is the painted sprite and the motion
    /// that is the mechanic itself: the Snap Daisy dims and squashes while it closes, the Seesaw leaf tilts around its painted knob, the Ladybug mirrors
    /// to its running direction. Without painted art the platform falls back to the tinted normal platform. One pooled sprite: the painted Seesaw knob.
    /// </summary>
    public sealed class SpecialPlatformFx
    {
        const int OverlayOrder = 3;   // above the platform (0), below pickups (5), gongs (6) and the player (10)

        readonly SpriteFactory make;
        readonly bool reduced;
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        SpriteRenderer[] parts = new SpriteRenderer[0];   // painted Seesaw knob per platform (null for every other kind)
        bool[] painted = new bool[0];   // platform i has painted art (RegionArt.KindPieces)
        Sprite knob;                    // seesaw pivot knob sprite (own sprite: the leaf tilts, the knob stays) and its top distance below the surface, in baked pixels
        float knobTopDropPx;
        Sprite stem;                    // sprout lift stem sprite (painted, behind the platform) and its top distance below the surface, in baked pixels
        float stemTopDropPx;

        public SpecialPlatformFx(SpriteFactory factory, bool reducedMotion)
        {
            make = factory;
            reduced = reducedMotion;
        }

        /// <summary>Seesaw pivot knob of the region art (null = no knob).</summary>
        public void SetKnob(Sprite sprite, float topDropPx)
        {
            knob = sprite;
            knobTopDropPx = topDropPx;
        }

        /// <summary>Sprout Lift stem of the region art (null = no stem).</summary>
        public void SetStem(Sprite sprite, float topDropPx)
        {
            stem = sprite;
            stemTopDropPx = topDropPx;
        }

        /// <summary>True for the platform kinds this class dresses.</summary>
        public static bool Handles(PlatformKind k) { return PlatformKinds.IsRegionNew(k); }

        /// <summary>Tint of the placeholder base platform (a colour hint for the placeholder; no drawn cues).</summary>
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
                // Region 2 placeholders (Mushroom Grove: purple, teal, glow cyan, warm pink) until the painted pieces of the T73 packet exist
                case PlatformKind.SquishCap: return new Color(0.85f, 0.7f, 1f, 1f);
                case PlatformKind.SproutLift: return new Color(0.6f, 0.95f, 0.75f, 1f);
                case PlatformKind.PuffFloat: return new Color(0.9f, 0.85f, 1f, 1f);
                case PlatformKind.FairyRing: return new Color(0.75f, 0.6f, 0.95f, 1f);
                case PlatformKind.OrbitCap: return new Color(0.55f, 0.95f, 0.95f, 1f);
                case PlatformKind.DizzySpores: return new Color(0.85f, 0.6f, 0.45f, 1f);
                case PlatformKind.SporePop: return new Color(1f, 0.7f, 0.8f, 1f);
                // Region 3 placeholders (Sunny Beach: aqua, sand, coral pink, driftwood, sea green, kelp green, pufferfish gold) until the painted pieces are imported
                case PlatformKind.SurfBoostWave: return new Color(0.55f, 0.9f, 0.95f, 1f);
                case PlatformKind.SandcastleStep: return new Color(0.98f, 0.86f, 0.62f, 1f);
                case PlatformKind.ClamShellPad: return new Color(1f, 0.72f, 0.75f, 1f);
                case PlatformKind.SinkingRaft: return new Color(0.8f, 0.6f, 0.4f, 1f);
                case PlatformKind.TurtleShuttle: return new Color(0.55f, 0.85f, 0.5f, 1f);
                case PlatformKind.KelpTangle: return new Color(0.35f, 0.6f, 0.35f, 1f);
                case PlatformKind.PufferfishPad: return new Color(1f, 0.88f, 0.45f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public void Resize(int n)
        {
            if (parts.Length < n) System.Array.Resize(ref parts, n);
            if (painted.Length < n) System.Array.Resize(ref painted, n);
        }

        /// <param name="hasArt">True when RunView drew the painted piece of this platform (the squash of the Snap Daisy only applies to painted art).</param>
        public void Attach(int i, Platform p, bool hasArt)
        {
            Resize(i + 1);
            Release(i);
            painted[i] = hasArt;
            // The only extra sprite: the painted Seesaw pivot knob (art, not a cue). Nothing else is drawn on top of a special platform.
            Sprite part = !hasArt ? null : (p.Kind == PlatformKind.Seesaw ? knob : (p.Kind == PlatformKind.SproutLift ? stem : null));
            if (part == null) return;
            SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : make("fx" + p.Id, part, OverlayOrder);
            sr.gameObject.SetActive(true);
            sr.sprite = part;
            sr.sortingOrder = p.Kind == PlatformKind.SproutLift ? -1 : OverlayOrder;   // the stem is behind the cap
            sr.color = Color.white;
            sr.transform.rotation = Quaternion.identity;
            parts[i] = sr;
        }

        public void Release(int i)
        {
            if (i >= parts.Length) return;
            if (parts[i] != null)
            {
                parts[i].gameObject.SetActive(false);
                pool.Push(parts[i]);
                parts[i] = null;
            }
            painted[i] = false;
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Ease out back from t = 0 (flat) to t = 1 (open): overshoots a little then settles at 1.</summary>
        static float SpringBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return Mathf.Lerp(0.3f, 1f, 1f + c3 * u * u * u + c1 * u * u);
        }

        /// <summary>
        /// Platform motion that IS the mechanic (no drawn cues): Snap Daisy dims / squashes while it closes, the Seesaw tilts, the Ladybug mirrors
        /// to its running direction. <paramref name="x"/> / <paramref name="y"/> are the current centre and surface height from the Sim.
        /// </summary>
        public void Update(int i, Platform p, Sim sim, in SimState s, int tick, float x, float y, SpriteRenderer baseSr)
        {
            if (i >= painted.Length) return;
            float w = p.Width.F();
            switch (p.Kind)
            {
                case PlatformKind.SnapDaisy:
                    {
                        int[] ct = s.CrumbleTick;
                        int phase = sim.SnapPhase(p.Id, tick, ct);
                        if (baseSr != null) { Color c = baseSr.color; c.a = phase == 2 ? 0.25f : 1f; baseSr.color = c; }   // shut: you fall through
                        if (painted[i] && baseSr != null)
                        {
                            // Squash (a deliberate animation, not a stretched image: the bake itself is uniform): flat while counting until 70 percent, then down to 0.45
                            // by the close, flat (0.3) and dim while shut, and a springy rebound (ease out back) around the reopen. Reduced motion: no squash, the dim only.
                            // The walking surface stays where it is: the centre rises by the share of the sprite that sits below the surface.
                            int since = ct[p.Id] >= 0 ? tick - ct[p.Id] : -1;
                            int closeT = sim.Cfg.SnapCloseTicks, shutEnd = closeT + sim.Cfg.SnapReopenTicks;
                            float late = phase == 2 ? 1f : (phase == 1 ? Mathf.Clamp01((since - closeT * 0.7f) / (closeT * 0.3f)) : 0f);
                            float k = 1f;
                            if (!reduced)
                            {
                                if (phase == 1) k = Mathf.Lerp(1f, 0.45f, late);
                                else if (phase == 2) k = since >= shutEnd - 8 ? SpringBack((since - (shutEnd - 8)) / 22f) : Mathf.Lerp(0.45f, 0.3f, Mathf.Clamp01((since - closeT) / 12f));
                                else if (since >= shutEnd && since < shutEnd + 14) k = SpringBack((since - (shutEnd - 8)) / 22f);
                            }
                            Transform bt = baseSr.transform;
                            Vector3 ls = bt.localScale;
                            bt.localScale = new Vector3(ls.x, ls.x * k, ls.z);
                            Vector3 bp = bt.position;
                            bp.y += (y - bp.y) * (1f - k);   // (y - bp.y) is the distance from the sprite centre up to the surface
                            bt.position = bp;
                        }
                        break;
                    }
                case PlatformKind.Ladybug:
                    {
                        if (painted[i] && baseSr != null)
                        {
                            float x1 = sim.PlatformX(p.Id, tick + 1, s.CrumbleTick).F();
                            float face = x1 > x + 0.0001f ? 1f : (x1 < x - 0.0001f ? -1f : 1f);
                            baseSr.flipX = face > 0f;   // the art looks left: mirror it when the bug runs right
                        }
                        break;
                    }
                case PlatformKind.TurtleShuttle:
                    {
                        if (painted[i] && baseSr != null)
                        {
                            // The painted turtle looks RIGHT: it faces the way of its next glide (right from the end of the left pause to the end of the right glide, then left),
                            // so it never swims backwards. The art is mirrored about the shell centre; the position itself comes from the Sim (TurtleOffset).
                            int period = p.Period > 0 ? p.Period : sim.Cfg.TurtlePeriodTicks;
                            int glide = sim.Cfg.TurtleGlideTicks;
                            int pause = (period - 2 * glide) / 2;
                            int ph = (tick + p.Phase) % period;
                            baseSr.flipX = ph >= pause + glide;
                        }
                        break;
                    }
                case PlatformKind.SproutLift:
                    {
                        SpriteRenderer st = parts[i];
                        if (st != null && stem != null)
                        {
                            // The painted stem hangs under the cap (own sprite, same uniform scale as the cap) and rises with it.
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float sppu = stem.pixelsPerUnit;
                            float sh = stem.rect.height / sppu * ks;
                            Transform t = st.transform;
                            t.position = new Vector3(x, y - stemTopDropPx / sppu * ks - sh * 0.5f, 0f);
                            t.localScale = new Vector3(ks, ks, 1f);
                        }
                        break;
                    }
                case PlatformKind.Seesaw:
                    {
                        int tilt = sim.SeesawTilt(s, p.Id);
                        float maxAngle = Mathf.Atan2(0.25f, Mathf.Max(0.5f, w * 0.5f)) * Mathf.Rad2Deg;   // a 0.25 u dip at the low end
                        float ease = tilt == 0 ? 0f : (reduced ? 1f : Mathf.Clamp01((tick - s.LandTick) / 8f));
                        if (baseSr != null) baseSr.transform.rotation = Quaternion.Euler(0f, 0f, -tilt * maxAngle * ease);
                        SpriteRenderer kn = parts[i];
                        if (kn != null && knob != null)
                        {
                            // The painted leaf tilts, the painted knob (own sprite, uniform scale of the leaf) stays under its middle.
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float kppu = knob.pixelsPerUnit;
                            float kh = knob.rect.height / kppu * ks;
                            Transform t = kn.transform;
                            t.position = new Vector3(x, y - knobTopDropPx / kppu * ks - kh * 0.5f, 0f);
                            t.localScale = new Vector3(ks, ks, 1f);
                        }
                        break;
                    }
            }
        }
    }
}
