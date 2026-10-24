using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Temporary gear that Flight (wings) and Rocket (a rocket pack) put on Momo while the power-up runs (owner 2026-10-24). The pictures are drawn
    /// in code (signed distance shapes with a navy outline) until painted art exists; they are built once and cached.
    /// </summary>
    public static class PowerGearArt
    {
        static Sprite wing, pack, flame;
        static readonly Color Navy = new Color(0.07f, 0.12f, 0.30f, 1f);

        /// <summary>Left wing, attached at its right edge (mirror it for the right side). 2 world units wide at scale 1.</summary>
        public static Sprite Wing { get { return wing != null ? wing : (wing = BuildWing()); } }
        /// <summary>A small rocket standing upright, worn on the back.</summary>
        public static Sprite Pack { get { return pack != null ? pack : (pack = BuildPack()); } }
        /// <summary>A flame pointing down, hangs under the pack.</summary>
        public static Sprite Flame { get { return flame != null ? flame : (flame = BuildFlame()); } }

        static float Ellipse(float x, float y, float cx, float cy, float a, float b, float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float dx = x - cx, dy = y - cy;
            float u = (dx * c + dy * s) / a, v = (-dx * s + dy * c) / b;
            return (Mathf.Sqrt(u * u + v * v) - 1f) * Mathf.Min(a, b);
        }

        static Sprite Bake(int w, int h, System.Func<float, float, Color> shade, Vector2 pivot, float pixelsPerUnit, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = shade(x + 0.5f, y + 0.5f);
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, pixelsPerUnit);
            sp.name = name;
            return sp;
        }

        /// <summary>Colour of a point whose signed distance to the shape is sd: fill inside, a navy outline of 4 px, clear outside.</summary>
        static Color Paint(float sd, Color fill)
        {
            const float outline = 4f;
            if (sd <= -0.5f) return fill;
            if (sd < 0.5f) return Color.Lerp(fill, Navy, Mathf.Clamp01(sd + 0.5f));
            if (sd < outline) return Navy;
            if (sd < outline + 1f) return new Color(Navy.r, Navy.g, Navy.b, 1f - (sd - outline));
            return Color.clear;
        }

        static Sprite BuildWing()
        {
            const int n = 128;
            return Bake(n, n, (x, y) =>
            {
                float d = Ellipse(x, y, 98f, 66f, 20f, 18f, 0f);                 // the root
                d = Mathf.Min(d, Ellipse(x, y, 50f, 98f, 46f, 12f, 24f));        // upper feather
                d = Mathf.Min(d, Ellipse(x, y, 44f, 74f, 50f, 13f, 6f));
                d = Mathf.Min(d, Ellipse(x, y, 50f, 50f, 44f, 12f, -12f));
                d = Mathf.Min(d, Ellipse(x, y, 60f, 30f, 34f, 10f, -26f));       // lowest, shortest feather
                float t = Mathf.Clamp01(y / n);
                Color fill = Color.Lerp(new Color(0.72f, 0.86f, 1f, 1f), Color.white, t);
                return Paint(d, fill);
            }, new Vector2(0.78f, 0.52f), 64f, "power_wing");
        }

        static float Box(float x, float y, float cx, float cy, float hx, float hy, float round)
        {
            float qx = Mathf.Abs(x - cx) - (hx - round), qy = Mathf.Abs(y - cy) - (hy - round);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
        }

        static Sprite BuildPack()
        {
            const int w = 96, h = 128;
            return Bake(w, h, (x, y) =>
            {
                float d = Box(x, y, 48f, 56f, 24f, 40f, 14f);                      // body
                d = Mathf.Min(d, Ellipse(x, y, 48f, 100f, 22f, 26f, 0f));          // nose
                d = Mathf.Min(d, Box(x, y, 20f, 34f, 10f, 20f, 6f));               // left fin
                d = Mathf.Min(d, Box(x, y, 76f, 34f, 10f, 20f, 6f));               // right fin
                Color fill = new Color(0.90f, 0.28f, 0.24f, 1f);
                if (y > 80f && Ellipse(x, y, 48f, 100f, 22f, 26f, 0f) < 0f) fill = new Color(1f, 0.93f, 0.85f, 1f);    // pale nose
                if (Mathf.Abs(y - 58f) < 6f) fill = new Color(1f, 0.93f, 0.85f, 1f);                                  // a band
                if (x < 40f && y > 20f && y < 90f && d < -6f) fill = Color.Lerp(fill, Color.white, 0.25f);             // a soft highlight
                return Paint(d, fill);
            }, new Vector2(0.5f, 0.5f), 96f, "power_pack");
        }

        static Sprite BuildFlame()
        {
            const int w = 64, h = 96;
            return Bake(w, h, (x, y) =>
            {
                float d = Ellipse(x, y, 32f, 62f, 18f, 30f, 0f);
                d = Mathf.Min(d, Ellipse(x, y, 32f, 40f, 11f, 34f, 0f));
                Color fill = Color.Lerp(new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.92f, 0.35f, 1f), Mathf.Clamp01((y - 20f) / 60f));
                return Paint(d, fill);
            }, new Vector2(0.5f, 1f), 96f, "power_flame");
        }
    }
}
