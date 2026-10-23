using System;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Measured walkable island of a platform sprite, in the sprite's pixels (Tools/art_pipeline/platform_metrics.py).</summary>
    [Serializable]
    public struct PlatformMetric
    {
        public float W, H, BodyLeft, BodyRight, SurfaceY, BodyBottom;
        public bool Valid { get { return W > 0f && BodyRight > BodyLeft; } }
    }

    /// <summary>Sprites of one region. Filled by the editor tool ArtCatalogBuilder from Assets/Art (see Assets/Art/NOTES.md).</summary>
    [Serializable]
    public sealed class RegionArt
    {
        public int Index;
        public string Id;
        public Sprite BgFar, BgMid, BgNear;
        public Sprite Normal, Narrow, Moving, Crumble, Bounce, WallL, WallR;
        /// <summary>Thin on-style platform pieces (optional): long, medium, short, crumble, moving, bounce. Each has the same world thickness.</summary>
        public Sprite[] Pieces = new Sprite[0];
        public PlatformMetric[] PieceMetrics = new PlatformMetric[0];
        /// <summary>One real, unscaled piece per platform width (owner, 2026-10-23: never stretch). WidthUnits[i] is the walkable length of WidthPieces[i] in world units.</summary>
        public Sprite[] WidthPieces = new Sprite[0];
        public PlatformMetric[] WidthMetrics = new PlatformMetric[0];
        public float[] WidthUnits = new float[0];
        /// <summary>Seamless vertical wall tiles (mirrored middle band of the wall art): stacked edge to edge they never leave a gap.</summary>
        public Sprite WallTileL, WallTileR;
        /// <summary>Order: normal, narrow, moving, crumble, bounce.</summary>
        public PlatformMetric[] Metrics = new PlatformMetric[5];
        /// <summary>Colours of the code-drawn platform slab (sampled from the tileset art).</summary>
        public Color PlatTop, PlatSide;
        public Sprite[] Props = new Sprite[0];
        public Sprite[] Signature = new Sprite[0];
    }

    /// <summary>
    /// Every sprite the game needs, referenced from one asset in Resources so player builds include them.
    /// Rebuild with Tools/Boing Up: Sky Tower/Build Art Catalog after the art changes.
    /// </summary>
    public sealed class ArtCatalog : ScriptableObject
    {
        public RegionArt[] Regions = new RegionArt[0];

        public Sprite Player;
        public Sprite[] CoinFrames = new Sprite[0];
        public Sprite[] GemSizes = new Sprite[0];
        public Sprite[] Orbs = new Sprite[0];
        public Sprite CoinIcon, GemIcon, StarFilled, StarEmpty;

        public Sprite[] Buttons = new Sprite[0];        // blue, pink, silver, navy squares
        public Sprite[] RoundButtons = new Sprite[0];   // 12 round icon buttons
        public Sprite[] Panels = new Sprite[0];         // 3 glass panels
        public Sprite[] Popup = new Sprite[0];          // popup frame
        public Sprite[] HudIcons = new Sprite[0];       // 10 hud icons
        public Sprite[] LevelNodes = new Sprite[0];     // normal, complex, boss, locked, completed, current
        public Sprite[] MapNodes = new Sprite[0];       // unlocked, current, completed, locked, teaser, ring
        public Sprite[] Ribbons = new Sprite[0];
        public Sprite[] RarityFrames = new Sprite[0];
        public Sprite[] Toggles = new Sprite[0];
        public Sprite[] ProgressBars = new Sprite[0];
        public Sprite[] SettingsIcons = new Sprite[0];
        public Sprite[] Confetti = new Sprite[0];
        public Sprite[] Sparkles = new Sprite[0];
        public Sprite[] DustPuff = new Sprite[0];
        public Sprite[] Trail = new Sprite[0];

        public Sprite MapBackground, LoadingArt, AppIcon;
        /// <summary>Clean 9-slice popup frame (OpenArt, T28). Null = the code-drawn panel.</summary>
        public Sprite PopupFrame;
        /// <summary>Round close (X) button icon (OpenArt, T32).</summary>
        public Sprite CloseIcon;

        public RegionArt ForRegion(int index)
        {
            for (int i = 0; i < Regions.Length; i++) if (Regions[i].Index == index) return Regions[i];
            return null;
        }

        public static Sprite Pick(Sprite[] list, int index)
        {
            if (list == null || list.Length == 0) return null;
            if (index < 0) index = 0;
            return list[index < list.Length ? index : list.Length - 1];
        }
    }
}
