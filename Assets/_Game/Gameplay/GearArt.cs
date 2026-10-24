using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Placeholder art for the 32 gear pieces and the layering on Momo (docs/15 section 5), drawn in code so it costs no OpenArt credit and adds no import step.
    /// Every piece is a flat shape with a dark outline in a colour per rarity plus 0 to 3 light stripes (the shape cue: the stripe count equals the rarity step,
    /// so rarity never relies on colour alone). Real art replaces <see cref="PieceSprite"/> later; the rig below stays.
    /// Sprites are built lazily the first time a screen shows them (never inside a run frame) and cached.
    /// </summary>
    public static class GearArt
    {
        /// <summary>Where a slot's sprite sits on Momo, as fractions of Momo's sprite rectangle (x from the left, y from the top): centre and size.</summary>
        public struct RigRect
        {
            public float Cx, Cy, W, H;
            public RigRect(float cx, float cy, float w, float h) { Cx = cx; Cy = cy; W = w; H = h; }
        }

        // Measured by eye from Momo's sprite (head centre x 0.40, scarf under the chin, legs at x 0.26 and 0.52). The owner checks these anchors on the phone.
        static readonly RigRect[] MomoRig =
        {
            new RigRect(0.40f, 0.165f, 0.52f, 0.25f),   // hat: sits on the head tuft between the ears
            new RigRect(0.38f, 0.775f, 0.54f, 0.23f),   // shirt: under the scarf over the torso and the upper arms
            new RigRect(0.40f, 0.895f, 0.42f, 0.15f),   // pants: the hips and the upper legs
            new RigRect(0.39f, 0.925f, 0.54f, 0.13f),   // boots: both feet
        };

        /// <summary>Draw order from the back: pants, boots, shirt, hat (docs/15 5.1).</summary>
        public static readonly GearSlot[] DrawOrder = { GearSlot.Pants, GearSlot.Boots, GearSlot.Shirt, GearSlot.Hat };

        public static RigRect Rig(GearSlot slot) { return MomoRig[(int)slot]; }

        static readonly Color[] RarityFill =
        {
            new Color(0.62f, 0.80f, 0.64f, 1f),   // Common: grey green
            new Color(0.40f, 0.66f, 1.00f, 1f),   // Rare: blue
            new Color(0.74f, 0.50f, 0.96f, 1f),   // Epic: purple
            new Color(1.00f, 0.80f, 0.26f, 1f),   // Legendary: gold
        };

        public static Color Fill(Rarity r) { return RarityFill[(int)r]; }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const float MomoW = 1365f, MomoH = 1649f, PxPerMomoPx = 0.32f;   // piece textures are drawn at about the size they appear in the menu

        /// <summary>The piece's sprite. Its pixel aspect equals the rig rectangle on Momo's real pixels, so drawing it into that rectangle never distorts it.</summary>
        public static Sprite PieceSprite(GearDef def)
        {
            if (def == null) return null;
            Sprite s;
            if (cache.TryGetValue(def.Id, out s) && s != null) return s;
            s = Build(def);
            cache[def.Id] = s;
            return s;
        }

        // ---- shapes: signed distance in units of the texture height, x measured from the left in the same units ----

        delegate float Sdf(float x, float y, float aspect);

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float dx = Mathf.Abs(x - cx) - (hw - r), dy = Mathf.Abs(y - cy) - (hh - r);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        // y runs from the bottom (0) to the top (1) inside a piece; x from 0 to aspect.
        static Sdf ShapeOf(GearSlot slot, int design)
        {
            switch (slot)
            {
                case GearSlot.Hat:
                    if (design == 0)   // round cap with a brim
                        return (x, y, a) => Mathf.Min(Mathf.Max(Ellipse(x, y, a * 0.5f, 0.40f, a * 0.28f, 0.52f), 0.30f - y), Box(x, y, a * 0.5f, 0.17f, a * 0.46f, 0.14f, 0.12f));
                    return (x, y, a) => Mathf.Min(Box(x, y, a * 0.5f, 0.56f, a * 0.19f, 0.42f, 0.10f), Box(x, y, a * 0.5f, 0.15f, a * 0.45f, 0.13f, 0.10f));   // tall hat
                case GearSlot.Shirt:
                    if (design == 0)   // tee with short sleeves
                        return (x, y, a) => Mathf.Min(Box(x, y, a * 0.5f, 0.50f, a * 0.29f, 0.46f, 0.12f), Mathf.Min(Box(x, y, a * 0.12f, 0.70f, a * 0.11f, 0.22f, 0.09f), Box(x, y, a * 0.88f, 0.70f, a * 0.11f, 0.22f, 0.09f)));
                    return (x, y, a) => Mathf.Max(Mathf.Min(Box(x, y, a * 0.5f, 0.50f, a * 0.34f, 0.46f, 0.12f), Mathf.Min(Box(x, y, a * 0.10f, 0.62f, a * 0.09f, 0.30f, 0.08f), Box(x, y, a * 0.90f, 0.62f, a * 0.09f, 0.30f, 0.08f))), -Ellipse(x, y, a * 0.5f, 0.96f, a * 0.10f, 0.20f));   // vest with a collar notch
                case GearSlot.Pants:
                    if (design == 0)   // shorts
                        return (x, y, a) => Mathf.Min(Box(x, y, a * 0.5f, 0.80f, a * 0.46f, 0.20f, 0.10f), Mathf.Min(Box(x, y, a * 0.29f, 0.46f, a * 0.19f, 0.46f, 0.10f), Box(x, y, a * 0.71f, 0.46f, a * 0.19f, 0.46f, 0.10f)));
                    return (x, y, a) => Mathf.Min(Box(x, y, a * 0.5f, 0.84f, a * 0.46f, 0.16f, 0.10f), Mathf.Min(Box(x, y, a * 0.29f, 0.42f, a * 0.17f, 0.50f, 0.08f), Box(x, y, a * 0.71f, 0.42f, a * 0.17f, 0.50f, 0.08f)));
                default:   // boots: one shoe per foot
                    if (design == 0)
                        return (x, y, a) => Mathf.Min(Box(x, y, a * 0.24f, 0.45f, a * 0.21f, 0.42f, 0.14f), Box(x, y, a * 0.76f, 0.45f, a * 0.21f, 0.42f, 0.14f));
                    return (x, y, a) => Mathf.Min(Box(x, y, a * 0.24f, 0.55f, a * 0.19f, 0.46f, 0.10f), Box(x, y, a * 0.76f, 0.55f, a * 0.19f, 0.46f, 0.10f));
            }
        }

        static readonly Color Rim = new Color(0.07f, 0.09f, 0.20f, 1f);

        static Sprite Build(GearDef def)
        {
            RigRect rig = MomoRig[(int)def.Slot];
            int w = Mathf.Max(48, Mathf.RoundToInt(rig.W * MomoW * PxPerMomoPx));
            int h = Mathf.Max(32, Mathf.RoundToInt(rig.H * MomoH * PxPerMomoPx));
            float aspect = (float)w / h;
            Sdf shape = ShapeOf(def.Slot, def.Design);
            Color fill = RarityFill[(int)def.Rarity];
            if (def.Design == 1) fill = Color.Lerp(fill, new Color(0.15f, 0.15f, 0.30f, 1f), 0.22f);   // the second design is a shade darker
            Color band = Color.Lerp(fill, Color.white, 0.78f);
            int stripes = RarityInfo.Stripes(def.Rarity);
            const float rimW = 0.045f;
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float fillA = 0f, rimA = 0f, bandA = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float x = (i + 0.25f + sx * 0.5f) / h, y = (j + 0.25f + sy * 0.5f) / h;   // y from the bottom, in texture heights
                            float d = shape(x, y, aspect);
                            if (d <= -rimW)
                            {
                                fillA += 0.25f;
                                if (InStripe(y, stripes)) bandA += 0.25f;
                            }
                            else if (d <= 0f) rimA += 0.25f;
                        }
                    }
                    float a = fillA + rimA;
                    if (a <= 0f) { px[j * w + i] = new Color32(0, 0, 0, 0); continue; }
                    float bandShare = fillA > 0f ? bandA / fillA : 0f;
                    Color body = Color.Lerp(fill, band, bandShare);
                    Color c = Color.Lerp(Rim, body, fillA / a);
                    px[j * w + i] = new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f), (byte)Mathf.RoundToInt(c.b * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = def.Id };
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.name = def.Id;
            return sp;
        }

        /// <summary>Light horizontal bands: none for Common, one for Rare, two for Epic, three for Legendary (countable at any size).</summary>
        static bool InStripe(float y, int count)
        {
            if (count <= 0) return false;
            float thick = 0.055f;
            switch (count)
            {
                case 1: return Mathf.Abs(y - 0.50f) < thick;
                case 2: return Mathf.Abs(y - 0.38f) < thick || Mathf.Abs(y - 0.62f) < thick;
                default: return Mathf.Abs(y - 0.28f) < thick || Mathf.Abs(y - 0.50f) < thick || Mathf.Abs(y - 0.72f) < thick;
            }
        }

        // ---------------- layering on Momo ----------------

        /// <summary>Tint colour of a hero (placeholder look: Momo multiplied by it).</summary>
        public static Color HeroTint(HeroDef hero)
        {
            if (hero == null) return Color.white;
            int t = hero.Tint;
            return new Color(((t >> 16) & 255) / 255f, ((t >> 8) & 255) / 255f, (t & 255) / 255f, 1f);
        }

        /// <summary>
        /// Builds Momo (tinted for the hero) with the hero's worn pieces layered on top, inside <paramref name="parent"/> at the given height; the width follows
        /// Momo's aspect, so nothing is stretched. Returns the container. A missing sprite leaves a plain Momo (CUX-10), never an error.
        /// </summary>
        public static RectTransform BuildDressedMomo(Transform parent, string name, Sprite momo, HeroDef hero, ProgressService progress, string heroIdForGear, float height)
        {
            RectTransform box = UiKit.Rect(parent, name);
            float aspect = momo != null ? momo.rect.width / momo.rect.height : 0.83f;
            box.sizeDelta = new Vector2(height * aspect, height);
            Image body = UiKit.AddImage(box, "Momo", momo, momo != null ? GearArt.HeroTint(hero) : new Color(1f, 1f, 1f, 0.2f));
            body.preserveAspect = true;
            UiKit.Stretch(body.rectTransform);
            if (progress != null)
            {
                foreach (GearSlot slot in DrawOrder)
                {
                    GearDef piece = progress.GearDefs.Get(progress.EquippedGear(heroIdForGear, slot));
                    if (piece == null) continue;
                    AddPiece(box, piece);
                }
            }
            return box;
        }

        /// <summary>Adds one piece as a child image placed by the rig (anchored by fractions, so it follows any size).</summary>
        public static Image AddPiece(RectTransform momoBox, GearDef piece)
        {
            Image img = UiKit.AddImage(momoBox, "Gear_" + ProgressService.SlotKey(piece.Slot), PieceSprite(piece), Color.white);
            img.preserveAspect = false;   // the sprite's pixel aspect equals the rig rectangle, so this is a uniform scale
            RigRect r = MomoRig[(int)piece.Slot];
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(r.Cx - r.W * 0.5f, 1f - (r.Cy + r.H * 0.5f));
            rt.anchorMax = new Vector2(r.Cx + r.W * 0.5f, 1f - (r.Cy - r.H * 0.5f));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        /// <summary>The same layering for the run: child sprite renderers of Momo's transform, positioned from Momo's sprite bounds so squash and facing carry over.</summary>
        public static SpriteRenderer[] AttachToRunPlayer(Transform player, Sprite momo, string heroId, ProgressService progress, int baseOrder)
        {
            var made = new List<SpriteRenderer>(4);
            if (player == null || momo == null || progress == null) return made.ToArray();
            Vector3 size = momo.bounds.size, center = momo.bounds.center;
            int n = 1;
            foreach (GearSlot slot in DrawOrder)
            {
                GearDef piece = progress.GearDefs.Get(progress.EquippedGear(heroId, slot));
                if (piece == null) continue;
                Sprite ps = PieceSprite(piece);
                if (ps == null) continue;
                RigRect r = MomoRig[(int)slot];
                var go = new GameObject("Gear_" + ProgressService.SlotKey(slot));
                go.transform.SetParent(player, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ps;
                sr.sortingOrder = baseOrder + n++;
                // Momo's sprite is drawn at (size) in the parent's local units; the piece fills r.W x r.H of it.
                float lx = center.x + (r.Cx - 0.5f) * size.x, ly = center.y + (0.5f - r.Cy) * size.y;
                go.transform.localPosition = new Vector3(lx, ly, 0f);
                go.transform.localScale = new Vector3(r.W * size.x / ps.bounds.size.x, r.H * size.y / ps.bounds.size.y, 1f);
                made.Add(sr);
            }
            return made.ToArray();
        }

        /// <summary>Colour tint of the run's player for a hero (white for Momo).</summary>
        public static Color PlayerTint(string heroId)
        {
            return HeroTint(HeroCatalog.BuiltIn().Get(heroId));
        }
    }
}
