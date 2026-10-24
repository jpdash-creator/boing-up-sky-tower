using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Stand-in pictures for the layout and screenshot tests while the real polygon art does not exist yet: a Momo built from the polygon parts of docs/19 section B
    /// (one fifth of the real canvas, 273 x 330) and gear pieces painted on the same polygons, installed through the test hooks of the catalog (nothing is written to
    /// Resources). <see cref="Install"/> pretends every piece with a number up to six has art.
    /// </summary>
    public static class TestRigArt
    {
        public const int W = 273, H = 330;
        static readonly List<Object> keep = new List<Object>();
        static readonly Dictionary<string, Sprite> parts = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> gear = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        // ---- shapes, coordinates in canvas pixels with y down (the same as docs/19) ----

        delegate float Sd(float x, float y);

        static float Ellipse(float cx, float cy, float rx, float ry, float x, float y)
        {
            float u = (x - cx) / rx, v = (y - cy) / ry;
            return (Mathf.Sqrt(u * u + v * v) - 1f) * Mathf.Min(rx, ry);
        }

        static float Box(float x0, float y0, float x1, float y1, float r, float x, float y)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Texture2D Paint(string name, Color fill, Sd shape)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color ink = new Color(0.07f, 0.12f, 0.30f, 1f);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = shape(x + 0.5f, H - (y + 0.5f));
                    Color c = Color.clear;
                    if (d <= -2f) c = fill;
                    else if (d < 0.5f) c = Color.Lerp(fill, ink, Mathf.Clamp01((d + 2f) / 2.5f));
                    else if (d < 1.5f) c = new Color(ink.r, ink.g, ink.b, 1.5f - d);
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            keep.Add(tex);
            return tex;
        }

        static Sprite SpriteOf(Texture2D tex, Rect? crop = null)
        {
            Rect r = crop ?? new Rect(0, 0, tex.width, tex.height);
            Sprite s = Sprite.Create(tex, r, crop.HasValue ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.5f), 10f);
            s.name = tex.name;
            keep.Add(s);
            return s;
        }

        static Sprite CharPart(string part)
        {
            Sprite s;
            if (parts.TryGetValue(part, out s)) return s;
            Color fur = new Color(0.62f, 0.80f, 1f, 1f), dark = new Color(0.45f, 0.62f, 0.92f, 1f);
            Texture2D t = null;
            switch (part)
            {
                case "back": t = Paint("t_back", new Color(0.92f, 0.96f, 1f, 1f), (x, y) => Ellipse(136f, 215f, 105f, 52f, x, y)); break;
                case "legL": t = Paint("t_legL", dark, (x, y) => Mathf.Min(Box(94f, 236f, 130f, 296f, 17f, x, y), Ellipse(108f, 307f, 27f, 14f, x, y))); break;
                case "legR": t = Paint("t_legR", dark, (x, y) => Mathf.Min(Box(143f, 236f, 179f, 296f, 17f, x, y), Ellipse(165f, 307f, 27f, 14f, x, y))); break;
                case "body": t = Paint("t_body", fur, (x, y) => Box(78f, 188f, 195f, 264f, 26f, x, y)); break;
                case "handL": t = Paint("t_handL", dark, (x, y) => Box(50f, 200f, 84f, 266f, 17f, x, y)); break;
                case "handR": t = Paint("t_handR", dark, (x, y) => Box(189f, 200f, 223f, 266f, 17f, x, y)); break;
                case "head": t = Paint("t_head", fur, (x, y) => Ellipse(136f, 118f, 105f, 94f, x, y)); break;
            }
            s = t != null ? SpriteOf(t) : null;
            parts[part] = s;
            return s;
        }

        static Color RarityColor(GearDef d)
        {
            Color c = GearUi.Fill(d.Rarity);
            return Color.Lerp(c, Color.white, (d.Number % 4) * 0.08f);
        }

        static Sprite GearPart(GearDef d, string part)
        {
            string key = d.Id + "/" + part;
            Sprite s;
            if (gear.TryGetValue(key, out s)) return s;
            Color c = RarityColor(d);
            Texture2D t = null;
            switch (part)
            {
                case "head": t = Paint(key, c, (x, y) => Mathf.Min(Box(30f, 22f, 243f, 92f, 30f, x, y), Ellipse(136f, 24f, 70f, 36f, x, y))); break;
                case "body": t = Paint(key, c, (x, y) => Box(74f, 184f, 199f, 268f, 28f, x, y)); break;
                case "sleeveL": t = Paint(key, c, (x, y) => Box(46f, 196f, 88f, 246f, 18f, x, y)); break;
                case "sleeveR": t = Paint(key, c, (x, y) => Box(185f, 196f, 227f, 246f, 18f, x, y)); break;
                case "bootL": t = Paint(key, c, (x, y) => Mathf.Min(Box(90f, 282f, 134f, 308f, 10f, x, y), Ellipse(108f, 309f, 31f, 16f, x, y))); break;
                case "bootR": t = Paint(key, c, (x, y) => Mathf.Min(Box(139f, 282f, 183f, 308f, 10f, x, y), Ellipse(165f, 309f, 31f, 16f, x, y))); break;
            }
            s = t != null ? SpriteOf(t) : null;
            gear[key] = s;
            return s;
        }

        static List<GearPartEntry> GearPartsOf(GearDef d)
        {
            var list = new List<GearPartEntry>();
            foreach (string p in GearCatalog.PartNames(d.Slot)) list.Add(new GearPartEntry { Part = p, Sprite = GearPart(d, p) });
            return list;
        }

        /// <summary>A tile picture: the painted part cropped tight, like the real icon builder does with the composite.</summary>
        static Sprite Icon(GearDef d)
        {
            Sprite s;
            if (icons.TryGetValue(d.Id, out s)) return s;
            string main = GearCatalog.PartNames(d.Slot)[0];
            Texture2D tex = GearPart(d, main).texture;
            Rect crop;
            switch (d.Slot)
            {
                case GearSlot.Head: crop = new Rect(28, H - 96, 217, 74); break;
                case GearSlot.Body: crop = new Rect(72, H - 272, 129, 92); break;
                default: crop = new Rect(88, H - 328, 98, 50); break;
            }
            s = SpriteOf(tex, crop);
            icons[d.Id] = s;
            return s;
        }

        /// <summary>Momo gets the stand-in polygon parts, pieces up to number six get stand-in art.</summary>
        public static void Install()
        {
            GearCatalog.ArtProbe = d => d.Number <= 6;
            GearParts.Override = GearPartsOf;
            GearParts.ClearCache();
            GearIcons.Override = Icon;
            CharacterCatalog.Momo.PartLoader = CharPart;
            CharacterCatalog.Momo.ClearCache();
        }

        public static void Remove()
        {
            GearCatalog.ArtProbe = null;
            GearParts.Override = null;
            GearParts.ClearCache();
            GearIcons.Override = null;
            GearIcons.ClearCache();
            CharacterCatalog.Momo.PartLoader = null;
            CharacterCatalog.Momo.ClearCache();
            foreach (Object o in keep) if (o != null) Object.Destroy(o);
            keep.Clear();
            parts.Clear();
            gear.Clear();
            icons.Clear();
        }
    }
}
