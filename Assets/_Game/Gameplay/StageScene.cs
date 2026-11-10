using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
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
        /// <summary>Size factor of this layout against the 760 px reference stage.</summary>
        public float Scale = 1f;
        /// <summary>Distance from the screen top to the centre of the island grass of the stage picture (equals FeetY when the picture is used, -1 otherwise).</summary>
        public float IslandGrassY = -1f;

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
        /// <summary>Character screen: the stage is the top 760 px; the list of characters sits on a dark panel below it.</summary>
        /// <summary>
        /// The stage takes 44 percent of the canvas height (700 to 1000 px; 760 px was the fixed value on 9:16), so a tall phone gets a bigger hero
        /// instead of an empty lower half; every measure scales with it (Scale 1 = the original 760 px layout).
        /// </summary>
        public static Stage CharacterLayout()
        {
            float h = UiKit.CanvasSizeRef().y;
            float gb = Mathf.Clamp(Mathf.Round(h * 0.44f), 700f, 1000f);
            float k = gb / 760f;
            // The hero is 20 percent smaller (owner 2026-11-02) and its name stands on the grass above it, so the hills end higher (210) and the oval shrinks;
            // the roster starts 18 reference px higher, right under the oval.
            return new Stage { Scale = k, BackdropH = Mathf.Round(210f * k), GroundBottom = Mathf.Round(742f * k), OvalY = Mathf.Round(660f * k), OvalW = Mathf.Round(480f * k), OvalH = Mathf.Round(128f * k) };
        }

        static Sprite grass, edge;

        /// <summary>Builds the stage described by <paramref name="layout"/> as the first child of <paramref name="screenRoot"/> (behind every other element).</summary>
        public static Stage Build(RectTransform screenRoot, ArtCatalog art, ProgressService progress, Stage layout)
        {
            RectTransform root = UiKit.Rect(screenRoot, "Stage");
            UiKit.Stretch(root);
            root.SetAsFirstSibling();
            layout.Root = root;

            // the painted stage picture (Resources/Art/Ui/hero_stage_bg), when present, replaces the generated sky, hills and grass; the present look is the fallback
            if (!BuildPictureBackground(root, layout)) BuildGeneratedBackground(root, art, progress, layout);
            return Finish(layout);
        }

        /// <summary>Resources name (under Art/Ui) of the painted stage picture for the Heroes screen.</summary>
        public const string StageBgName = "hero_stage_bg";

        /// <summary>Where the centre of the island's grassy top lies in hero_stage_bg, as a fraction of the picture height from its top (measured from the owner's screenshot, 2026-11-10: about 0.65). Change this one number when the picture is replaced.</summary>
        public const float IslandGrassFrac = 0.62f;

        /// <summary>
        /// Cover-fits the picture into the top GroundBottom px of the screen: one uniform scale (never stretched), centred, the overflow cropped by a RectMask2D.
        /// Returns false (nothing built) when the file is missing.
        /// </summary>
        static bool BuildPictureBackground(RectTransform root, Stage layout)
        {
            Sprite pic = ToyKit.Get(StageBgName);
            if (pic == null) return false;
            RectTransform box = UiKit.Rect(root, "StageBg");
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(1f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.offsetMin = new Vector2(0f, -layout.GroundBottom);
            box.offsetMax = Vector2.zero;
            box.gameObject.AddComponent<RectMask2D>();
            Image img = UiKit.AddImage(box, "Picture", pic, Color.white);
            img.preserveAspect = false;
            img.raycastTarget = false;
            // Uniform scale only (never stretched). The picture is placed so the centre of the island's grass top (IslandGrassFrac of the picture height from its top) lies exactly on the
            // hero's feet line (layout.FeetY), then scaled up just enough that it still covers the whole box: width, height, the part below the feet and the part above them.
            float aspect = pic.rect.width / Mathf.Max(1f, pic.rect.height);
            float w = 1080f, h = layout.GroundBottom, feet = layout.FeetY;
            float dh = Mathf.Max(Mathf.Max(h, w / aspect), Mathf.Max(feet / IslandGrassFrac, (h - feet) / (1f - IslandGrassFrac)));
            RectTransform ir = img.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.sizeDelta = new Vector2(dh * aspect, dh);
            ir.anchoredPosition = new Vector2(0f, -(feet - IslandGrassFrac * dh));   // top edge of the picture relative to the box top (above it when positive)
            layout.IslandGrassY = feet;
            return true;
        }

        static void BuildGeneratedBackground(RectTransform root, ArtCatalog art, ProgressService progress, Stage layout)
        {
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
        }

        /// <summary>The brown dirt oval is gone (owner 2026-11-10): the hero stands on the painted island, or on the plain grass of the fallback.</summary>
        static Stage Finish(Stage layout)
        {
            layout.Oval = null;
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

    }
}
