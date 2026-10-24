using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The land stage behind the character (owner reference screens, docs/18 section A.4): the current region's painted backdrop cropped to its top part,
    /// a grass ground below it and a sandy dirt oval with the character's feet ON it. Used by the character screen. Menu screens only: built once per screen, no per-frame code. All numbers are pixels of the 1080 wide reference canvas, measured from the top.
    /// </summary>
    public sealed class Stage
    {
        /// <summary>Full screen root of the stage; the doll is a child of it so it shares the oval's coordinates (the safe area never shifts one against the other).</summary>
        public RectTransform Root;
        /// <summary>The dirt oval.</summary>
        public RectTransform Oval;
        public float BackdropH, GroundBottom, OvalY, OvalW, OvalH;

        /// <summary>Puts the doll (any rect) on the oval: bottom centre of the doll a little below the oval's centre, so the feet are on the dirt.</summary>
        public void Stand(RectTransform doll)
        {
            doll.SetParent(Root, false);
            doll.anchorMin = doll.anchorMax = new Vector2(0.5f, 1f);
            doll.pivot = new Vector2(0.5f, 0f);
            doll.anchoredPosition = new Vector2(0f, -FeetY);
        }

        /// <summary>Distance from the top of the screen to the feet line.</summary>
        public float FeetY { get { return OvalY + OvalH * 0.12f; } }
    }

    public static class StageScene
    {
        /// <summary>Character screen: the stage fills the screen down to the tab bar; the oval sits at about 55 percent of the height.</summary>
        public static Stage CharacterLayout() { return new Stage { BackdropH = 820f, GroundBottom = -1f, OvalY = 1030f, OvalW = 720f, OvalH = 210f }; }

        static Sprite grass, oval, edge;

        /// <summary>Builds the stage described by <paramref name="layout"/> as the first child of <paramref name="screenRoot"/> (behind every other element).</summary>
        public static Stage Build(RectTransform screenRoot, ArtCatalog art, ProgressService progress, Stage layout)
        {
            RectTransform root = UiKit.Rect(screenRoot, "Stage");
            UiKit.Stretch(root);
            root.SetAsFirstSibling();
            layout.Root = root;

            // the current region's backdrop (region 1 when the region has none), cropped to the top part
            RegionDef cur = progress != null ? progress.CurrentRegion() : null;
            RegionArt ra = art != null ? (art.ForRegion(cur != null ? cur.Index : 1) ?? art.ForRegion(1)) : null;
            Sprite bg = ra != null ? ra.BgFar : null;
            RectTransform sky = UiKit.Rect(root, "Backdrop");
            sky.anchorMin = new Vector2(0f, 1f);
            sky.anchorMax = new Vector2(1f, 1f);
            sky.pivot = new Vector2(0.5f, 1f);
            sky.offsetMin = new Vector2(0f, -layout.BackdropH);
            sky.offsetMax = Vector2.zero;
            if (bg != null)
            {
                var raw = sky.gameObject.AddComponent<RawImage>();
                raw.texture = bg.texture;
                raw.uvRect = CropUv(bg, layout.BackdropH);
                raw.raycastTarget = false;
            }
            else
            {
                var img = sky.gameObject.AddComponent<Image>();
                img.color = new Color(0.45f, 0.72f, 0.95f, 1f);
                img.raycastTarget = false;
            }

            // grass ground: starts a little above the backdrop's bottom edge so the two blend
            RectTransform ground = UiKit.Rect(root, "Ground");
            ground.anchorMin = new Vector2(0f, 0f);
            ground.anchorMax = new Vector2(1f, 1f);
            ground.offsetMin = Vector2.zero;
            ground.offsetMax = new Vector2(0f, -layout.BackdropH);
            if (layout.GroundBottom > 0f)
            {
                ground.anchorMin = new Vector2(0f, 1f);
                ground.anchorMax = new Vector2(1f, 1f);
                ground.pivot = new Vector2(0.5f, 1f);
                ground.offsetMin = new Vector2(0f, -layout.GroundBottom);
                ground.offsetMax = new Vector2(0f, -layout.BackdropH);
            }
            Image gi = ground.gameObject.AddComponent<Image>();
            gi.sprite = GrassSprite();
            gi.raycastTarget = false;

            // a soft fade strip above the ground hides the seam with the backdrop's bottom edge
            RectTransform edge = UiKit.Rect(root, "GroundEdge");
            edge.anchorMin = new Vector2(0f, 1f);
            edge.anchorMax = new Vector2(1f, 1f);
            edge.pivot = new Vector2(0.5f, 1f);
            edge.offsetMin = new Vector2(0f, -(layout.BackdropH + 2f));
            edge.offsetMax = new Vector2(0f, -(layout.BackdropH - 130f));
            Image ei = edge.gameObject.AddComponent<Image>();
            ei.sprite = EdgeSprite();
            ei.raycastTarget = false;

            // the dirt oval
            Image ov = UiKit.AddImage(root, "DirtOval", OvalSprite(), Color.white);
            ov.preserveAspect = false;
            ov.raycastTarget = false;
            RectTransform ort = ov.rectTransform;
            ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 1f);
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.sizeDelta = new Vector2(layout.OvalW, layout.OvalH);
            ort.anchoredPosition = new Vector2(0f, -layout.OvalY);
            layout.Oval = ort;
            return layout;
        }

        /// <summary>UV rectangle that shows the part of the picture whose bottom lies at 74 percent of its height (the hills) at a width of 1080 reference px.</summary>
        static Rect CropUv(Sprite s, float heightPx)
        {
            Rect tr = s.textureRect;
            float tw = s.texture.width, th = s.texture.height;
            float shownH = 1080f * tr.height / tr.width;         // the whole picture at the screen width
            float frac = Mathf.Clamp01(heightPx / shownH);
            float bottomFromTop = Mathf.Max(0.74f, frac);
            return new Rect(tr.x / tw, (tr.y + (1f - bottomFromTop) * tr.height) / th, tr.width / tw, frac * tr.height / th);
        }

        static Sprite GrassSprite()
        {
            if (grass != null) return grass;
            const int h = 128;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[4 * h];
            Color top = new Color(0.52f, 0.82f, 0.28f), bot = new Color(0.20f, 0.52f, 0.20f);
            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (float)(h - 1);                  // 0 at the top of the picture
                Color c = Color.Lerp(top, bot, t);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            grass = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
            return grass;
        }

        static Sprite EdgeSprite()
        {
            if (edge != null) return edge;
            const int h = 32;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[4 * h];
            Color top = new Color(0.52f, 0.82f, 0.28f);
            for (int y = 0; y < h; y++)
            {
                Color c = top;
                c.a = Mathf.SmoothStep(0f, 1f, 1f - y / (float)(h - 1));   // transparent at the top of the picture, solid at the bottom
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            edge = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
            return edge;
        }

        static Sprite OvalSprite()
        {
            if (oval != null) return oval;
            const int w = 256, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            Color mid = new Color(0.96f, 0.72f, 0.40f), rim = new Color(0.78f, 0.50f, 0.24f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = Color.Lerp(mid, rim, Mathf.SmoothStep(0.55f, 1f, r));
                    c.a = Mathf.Clamp01((1f - r) / 0.10f);          // feathered edge
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            oval = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return oval;
        }
    }
}
