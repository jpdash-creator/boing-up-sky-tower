using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Draws a RunSession with SpriteRenderers (no gameplay logic here: it only reads the Sim state).
    /// World units equal Simulation units; the camera shows exactly SimConfig.ScreenWidth units across.
    /// </summary>
    public sealed class RunView : MonoBehaviour
    {
        sealed class Layer
        {
            public SpriteRenderer[] Tiles;
            public float Factor;
            public float Height;
            public float Scale;
        }

        sealed class Pop
        {
            public Transform Tf;
            public SpriteRenderer Sr;
            public float Age = 99f;
            public float Life = 0.35f;
            public float Size = 1f;
            public float Rise = 1f;
        }

        RunSession session;
        ArtCatalog art;
        RegionArt region;
        Camera cam;

        Transform worldRoot;
        Transform player;
        SpriteRenderer playerSr;
        float playerHeight = 1.5f;
        SpriteRenderer[] platformSr;
        Transform[] platformTf;
        float[] platformBaseScaleX;
        float[] platformOffX, platformOffY;   // sprite offset that puts the island's walking surface on the platform line
        SpriteRenderer[] pickupSr;
        bool[] pickupSeen;
        Transform[] hazardTf;
        SpriteRenderer[] hazardSr;
        readonly List<Layer> layers = new List<Layer>();
        SpriteRenderer[] wallL, wallR;
        float wallSegHeight = 3.2f;
        const float wallWidth = 1.0f;
        SpriteRenderer killZone;
        SpriteRenderer killLine;
        readonly Pop[] pops = new Pop[24];
        int nextPop;
        Sprite white;

        // Endless mode: the tower grows without end, so platform and gong views are created on demand for the part of the tower around the
        // camera and recycled through pools (no allocation in steady state: a pooled renderer is only re-configured). Campaign levels build
        // everything once; their window is the whole level.
        bool endless;
        int builtVersion;
        int actLo, actHi;             // active platform views are [actLo, actHi)
        int hazLo, hazHi;             // active gong views are [hazLo, hazHi)
        readonly Stack<SpriteRenderer> platPool = new Stack<SpriteRenderer>();
        readonly Stack<SpriteRenderer> cuePool = new Stack<SpriteRenderer>();
        readonly Stack<SpriteRenderer> gongPool = new Stack<SpriteRenderer>();
        Sprite gongSprite;
        static Sprite hazardSpikes;
        static Sprite HazardSpikes() { if (hazardSpikes == null) hazardSpikes = MakeHazardSpikes(); return hazardSpikes; }
        /// <summary>Views kept below / above the screen edges in endless mode, world units.</summary>
        const float WindowBelow = 3f, WindowAbove = 8f;

        float camHalfH, camHalfW, camY, designHalfH;
        float prevX, prevY, curX, curY, prevKill, curKill;
        bool wasGrounded = true;
        float prevVy;           // audio cues are derived from state changes here; the simulation knows nothing about sound
        bool wasDead;
        int prevHits;
        float squash;   // 0..1 landing squash timer
        bool reduced;   // reduced-motion setting, read once per run
        Sprite[] patterns;
        SpriteRenderer[] cueSr;
        SpriteRenderer dangerBand;

        /// <summary>Tallest supported aspect (9:21 and a margin). Backgrounds and walls are sized for it so any phone is covered.</summary>
        public const float TallestAspect = 0.4f;

        /// <summary>Test hook: forces the view aspect (width / height). 0 = use the camera.</summary>
        public static float AspectOverride;

        public float CameraY { get { return camY; } }
        public Camera Camera { get { return cam; } }

        // ---------------- galaxy portal (the goal) ----------------
        // A glowing galaxy swirl hangs above the last platform of the level. Reaching the goal row sends Momo spinning into it
        // (PlayWarp), then the results appear. Purely visual: the simulation still ends the run on the goal row.
        SpriteRenderer portal, portalGlow;
        Transform portalTf;
        Vector3 portalPos;
        float warpStart = -1f;

        static Sprite MakeGalaxy()
        {
            const int n = 192;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    float ang = Mathf.Atan2(dy, dx);
                    // two logarithmic spiral arms
                    float arm = Mathf.Cos(2f * (ang - 3.2f * Mathf.Log(r + 0.08f)));
                    float arms = Mathf.Clamp01(arm * 0.5f + 0.5f);
                    float core = Mathf.Clamp01(1f - r * 1.6f);
                    float edge = Mathf.Clamp01((1f - r) * 4f);
                    Color c = Color.Lerp(new Color(0.18f, 0.10f, 0.45f), new Color(0.85f, 0.45f, 0.95f), arms * (1f - r * 0.5f));
                    c = Color.Lerp(c, new Color(0.45f, 0.85f, 1f), Mathf.Pow(arms, 4f) * 0.6f);
                    c = Color.Lerp(c, Color.white, core * core);
                    float a = Mathf.Clamp01((0.35f + arms * 0.65f + core) * edge);
                    px[y * n + x] = new Color(c.r, c.g, c.b, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        }

        void BuildPortal()
        {
            int target = session.Spec.TargetRow();
            Platform[] plats = session.Data.Platforms;
            float topY = 0f;
            for (int i = 0; i < plats.Length; i++) if (plats[i].Row == target) topY = Mathf.Max(topY, plats[i].Y.F());
            if (topY <= 0f) for (int i = 0; i < plats.Length; i++) topY = Mathf.Max(topY, plats[i].Y.F());
            portalPos = new Vector3(camHalfW, topY + 4.2f, 0f);
            Sprite g = MakeGalaxy();
            portalGlow = NewSprite("PortalGlow", g, -50);
            portalGlow.color = new Color(0.6f, 0.4f, 1f, 0.35f);
            portalGlow.transform.position = portalPos;
            portalGlow.transform.localScale = Vector3.one * 5.2f;
            portal = NewSprite("Portal", g, -49);
            portal.transform.position = portalPos;
            portal.transform.localScale = Vector3.one * 3.6f;
            portalTf = portal.transform;
        }

        void UpdatePortal(float dt)
        {
            if (portalTf == null) return;
            if (!reduced) portalTf.Rotate(0f, 0f, -22f * dt);
            if (warpStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - warpStart) / WarpSeconds);
                float e = t * t * (3f - 2f * t);
                // Momo spirals into the galaxy: moves to its centre, shrinks and turns.
                player.position = Vector3.Lerp(warpFrom, portalPos, e);
                player.localScale = warpScale * (1f - 0.9f * e);
                player.rotation = Quaternion.Euler(0f, 0f, reduced ? 0f : e * 540f);
                if (!reduced) portalTf.localScale = Vector3.one * (3.6f + 1.6f * e);
            }
        }

        public const float WarpSeconds = 1.0f;
        Vector3 warpFrom, warpScale;

        /// <summary>Starts the goal animation: Momo flies into the galaxy portal. Call once; WarpDone tells when to show the results.</summary>
        public void PlayWarp()
        {
            if (warpStart >= 0f || player == null) return;
            warpStart = Time.time;
            warpFrom = player.position;
            warpScale = player.localScale;
        }

        public bool WarpActive { get { return warpStart >= 0f; } }
        public bool WarpDone { get { return warpStart >= 0f && Time.time - warpStart >= WarpSeconds; } }


        /// <summary>Debug/test hook: hides the parallax layers so the gameplay objects can be inspected on their own.</summary>
        public void SetBackgroundVisible(bool visible)
        {
            for (int i = 0; i < layers.Count; i++)
                for (int k = 0; k < layers[i].Tiles.Length; k++) layers[i].Tiles[k].enabled = visible;
        }

        /// <summary>Debug/test hook: shows only the first n parallax layers (0 = none).</summary>
        public void SetBackgroundLayerCount(int n)
        {
            for (int i = 0; i < layers.Count; i++)
                for (int k = 0; k < layers[i].Tiles.Length; k++) layers[i].Tiles[k].enabled = i < n;
        }

        public string DebugPlayer()
        {
            return playerSr == null ? "no player" : "player enabled=" + playerSr.enabled + " order=" + playerSr.sortingOrder + " sprite=" + (playerSr.sprite != null ? playerSr.sprite.name : "null") + " pos=" + playerSr.transform.position + " scale=" + playerSr.transform.localScale + " color=" + playerSr.color + " bounds=" + playerSr.bounds.size;
        }

        // ---------------- setup ----------------

        public void Build(RunSession s, ArtCatalog catalog, Camera camera)
        {
            session = s;
            endless = s.IsEndless;
            builtVersion = s.WorldVersion;
            reduced = GameSettings.ReducedMotion;
            AudioManager.SetMusic(MusicTrack.Run, s.Region.Index);
            art = catalog;
            cam = camera;
            region = art != null ? art.ForRegion(s.Region.Index) : null;
            white = MakeWhite();

            worldRoot = new GameObject("RunWorld").transform;
            worldRoot.SetParent(transform, false);

            float screenW = s.Cfg.ScreenWidth.F();
            cam.orthographic = true;
            camHalfW = screenW * 0.5f;
            FitCamera();
            designHalfH = screenW / (2f * TallestAspect);
            cam.transform.position = new Vector3(screenW * 0.5f, 0f, -10f);
            cam.backgroundColor = new Color32(0x14, 0x16, 0x24, 0xff);
            cam.clearFlags = CameraClearFlags.SolidColor;

            BuildBackground(screenW);
            BuildWalls();
            BuildPlatforms();
            BuildPickups();
            BuildHazards();
            BuildPlayer();
            BuildPowerFx();
            if (!endless) BuildPortal();   // endless towers have no goal
            for (int i = 0; i < pops.Length; i++) pops[i] = MakePop();

            curX = prevX = s.State.X.F();
            curY = prevY = s.State.Y.F();
            prevKill = curKill = s.State.KillY.F();
            camY = curKill + camHalfH;
            ApplyCamera();
            if (endless) UpdateEndlessWindow();
        }

        /// <summary>Red spiky ball with a dark outline (CUX-6: a hazard differs from every platform by shape, not by colour alone). 1 world unit wide, scaled by SetupHazard.</summary>
        static Sprite MakeHazardSpikes()
        {
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            const float spikes = 10f, baseR = 28f, tipR = 46f, outline = 4f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - (n - 1) * 0.5f, dy = y - (n - 1) * 0.5f;
                    float rho = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * spikes;
                    float tri = 1f - Mathf.Abs(2f * (a - Mathf.Floor(a)) - 1f);   // 0 between spikes, 1 at a tip
                    float edge = baseR + (tipR - baseR) * tri;
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (rho <= edge) c = rho > edge - outline ? new Color32(40, 10, 20, 255) : (rho < 10f ? new Color32(255, 235, 150, 255) : new Color32(235, 55, 45, 255));
                    px[y * n + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        static Sprite MakeWhite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);   // 1x1 world unit
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : worldRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : white;
            sr.sortingOrder = order;
            return sr;
        }

        void BuildBackground(float screenW)
        {
            Sprite[] sprites = region != null ? new[] { region.BgFar, region.BgMid, region.BgNear } : new Sprite[3];
            float[] factor = { 0.12f, 0.25f, 0.45f };
            float[] alpha = { 1f, 0f, 0f };   // the mid and near layers are full pictures: faint, so they never ghost into a double image behind the platforms (owner, T15, 2026-10-20)
            for (int i = 0; i < 3; i++)
            {
                if (sprites[i] == null || alpha[i] <= 0f) continue;   // owner issue 14: calm background, no ghost layers behind the platforms
                Vector2 size = sprites[i].bounds.size;
                float scale = Mathf.Max(screenW * 1.1f / size.x, designHalfH * 2f * 1.05f / size.y);
                var layer = new Layer { Factor = factor[i], Height = size.y * scale, Scale = scale, Tiles = new SpriteRenderer[3] };
                for (int k = 0; k < 3; k++)
                {
                    var sr = NewSprite("bg" + i + "_" + k, sprites[i], -100 + i * 10);
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    sr.color = i == 0 ? new Color(0.84f, 0.9f, 0.98f, 1f) : new Color(1f, 1f, 1f, alpha[i]);
                    layer.Tiles[k] = sr;
                }
                layers.Add(layer);
            }

            // A light veil between the painted layers and the platforms calms the picture so platforms and pickups read clearly in every region.
            if (white != null && sprites[0] != null)
            {
                float vs = designHalfH * 2f * 1.7f / Mathf.Max(0.01f, white.bounds.size.y);
                var veil = new Layer { Factor = 0f, Height = white.bounds.size.y * vs, Scale = vs, Tiles = new SpriteRenderer[3] };
                for (int k = 0; k < 3; k++)
                {
                    var sr = NewSprite("veil" + k, white, -75);
                    sr.transform.localScale = new Vector3(vs, vs, 1f);
                    sr.color = new Color(0.08f, 0.2f, 0.42f, 0.42f);
                    veil.Tiles[k] = sr;
                }
                layers.Add(veil);
            }
        }

        void BuildWalls()
        {
            if (region == null || (region.WallL == null && region.WallR == null && region.WallTileL == null)) return;
            Sprite tl = region.WallTileL ?? region.WallL, tr = region.WallTileR ?? region.WallR;
            if (region.WallTileL != null)
            {
                // Seamless tower wall: equal tiles stacked edge to edge (slightly overlapping), mirrored art so the joins match.
                Vector2 sz = region.WallTileL.bounds.size;
                wallSegHeight = wallWidth * sz.y / sz.x;
            }
            int count = Mathf.CeilToInt(designHalfH * 2f / wallSegHeight) + 2;
            wallL = new SpriteRenderer[count];
            wallR = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                wallL[i] = MakeWall(tl, true);
                wallR[i] = MakeWall(tr, false);
            }
        }

        SpriteRenderer MakeWall(Sprite sprite, bool left)
        {
            if (sprite == null) return null;
            var sr = NewSprite(left ? "wallL" : "wallR", sprite, -20);
            Vector2 size = sprite.bounds.size;
            if (region.WallTileL != null)
            {
                sr.transform.localScale = new Vector3(wallWidth / size.x, (wallSegHeight * 1.01f) / size.y, 1f);   // 1 percent overlap hides hairline seams
                return sr;
            }
            float h = wallSegHeight;
            float w = Mathf.Min(1.1f, size.x / size.y * h);
            sr.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            return sr;
        }

        static readonly System.Collections.Generic.Dictionary<int, Sprite> slabs = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>A clean rounded slab: dark outline, light top band, darker body. 9-sliced, so it is never stretched out of shape.</summary>
        static Sprite MakeSlab(Color top, Color side)
        {
            int key = ((Color32)top).GetHashCode() * 31 + ((Color32)side).GetHashCode();
            Sprite cached;
            if (slabs.TryGetValue(key, out cached) && cached != null) return cached;
            const int w = 64, h = 32, r = 10;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[w * h];
            Color outline = new Color(0.08f, 0.07f, 0.14f, 1f);
            Color hi = Color.Lerp(top, Color.white, 0.45f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // distance to the rounded rectangle edge (0 at the border, growing inwards)
                    float dx = Mathf.Min(x + 0.5f, w - x - 0.5f), dy = Mathf.Min(y + 0.5f, h - y - 0.5f);
                    float inset;
                    if (dx < r && dy < r) inset = r - Mathf.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy));
                    else inset = Mathf.Min(dx, dy);
                    if (inset < 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    Color c;
                    float fromTop = h - 1 - y;     // y grows upwards in texture space
                    if (inset < 2.2f) c = outline;
                    else if (fromTop < 3.5f) c = outline;
                    else if (fromTop < 6f) c = hi;
                    else if (fromTop < 13f) c = top;
                    else c = Color.Lerp(side, side * 0.78f, Mathf.Clamp01((fromTop - 13f) / 16f));
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(14, 10, 14, 10));
            slabs[key] = sp;
            return sp;
        }

        /// <summary>Index into region.Pieces (long, medium, short, crumble, moving, bounce), or -1 when the region has no piece art.</summary>
        int PieceIndex(Platform p, float width)
        {
            if (region == null || region.Pieces == null || region.Pieces.Length < 6 || region.Pieces[0] == null) return -1;
            switch (p.Kind)
            {
                case PlatformKind.Crumble: return 3;
                case PlatformKind.Moving: return 4;
                case PlatformKind.Bounce:
                case PlatformKind.BounceCap: return 5;
                default: return width < 2.4f ? 2 : (width < 4f ? 1 : 0);
            }
        }

        /// <summary>Index of the painted piece for this kind and width, or -1 when the region has none (the older pieces or code-drawn slab are used then).</summary>
        int WidthPieceIndex(Platform p, float width)
        {
            if (region == null || region.WidthPieces == null || region.WidthPieces.Length == 0) return -1;
            int kind = p.Kind == PlatformKind.Crumble ? 1 : p.Kind == PlatformKind.Moving ? 2 : (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap) ? 3 : 0;
            int best = -1;
            float bestDist = 0.26f;
            for (int k = 0; k < region.WidthUnits.Length; k++)
            {
                float d = Mathf.Abs(region.WidthUnits[k] - width);
                if (d < bestDist && region.WidthPieces[k] != null && region.WidthKinds != null && k < region.WidthKinds.Length && region.WidthKinds[k] == kind) { bestDist = d; best = k; }
            }
            return best;
        }

        PlatformMetric PlatformMetricFor(Platform p)
        {
            if (region == null || region.Metrics == null) return default(PlatformMetric);
            int k;
            switch (p.Kind)
            {
                case PlatformKind.Moving: k = region.Moving != null ? 2 : 0; break;
                case PlatformKind.Crumble: k = region.Crumble != null ? 3 : 0; break;
                case PlatformKind.Bounce:
                case PlatformKind.BounceCap: k = region.Bounce != null ? 4 : 0; break;
                default: k = (p.Width.F() < 3.2f && region.Narrow != null) ? 1 : 0; break;
            }
            return k < region.Metrics.Length ? region.Metrics[k] : default(PlatformMetric);
        }

        Sprite PlatformSprite(Platform p)
        {
            if (region == null) return null;
            switch (p.Kind)
            {
                case PlatformKind.Moving: return region.Moving ?? region.Normal;
                case PlatformKind.Crumble: return region.Crumble ?? region.Normal;
                case PlatformKind.Bounce:
                case PlatformKind.BounceCap: return region.Bounce ?? region.Normal;
                default: return (p.Width.F() < 3.2f && region.Narrow != null) ? region.Narrow : region.Normal;
            }
        }

        /// <summary>Procedural 32 x 8 tile used as a non-colour cue: dots (sticky), stripes (slippery), dashes (vanishing), hazard stripes (danger).</summary>
        static Sprite MakePattern(int kind)
        {
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool on;
                    switch (kind)
                    {
                        case 0: on = ((x % 16) - 8) * ((x % 16) - 8) + (y - 4) * (y - 4) <= 6; break;           // dots
                        case 1: on = ((x + y) % 8) < 3; break;                                                    // diagonal stripes
                        case 2: on = (x % 16) < 9 && y >= 2 && y <= 5; break;                                     // dashes
                        case 4: on = Mathf.Abs((x % 16) - 8) == 7 - y || Mathf.Abs((x % 16) - 8) == 6 - y; break;   // up chevrons (bounce)
                        default: on = ((x + y) % 16) < 8; break;                                                  // hazard stripes
                    }
                    px[y * w + x] = on ? new Color32(20, 16, 40, 230) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        }

        static Color PlatformTint(PlatformKind kind)
        {
            switch (kind)
            {
                case PlatformKind.Sticky: return new Color(1f, 0.7f, 0.9f, 1f);
                case PlatformKind.Slippery: return new Color(0.7f, 0.9f, 1f, 1f);
                case PlatformKind.Vanishing: return new Color(0.8f, 0.7f, 1f, 1f);
                default: return Color.white;
            }
        }

        void BuildPlatforms()
        {
            Platform[] plats = session.Data.Platforms;
            ResizePlatformArrays(plats.Length);
            if (endless) return;   // endless views come from UpdateEndlessWindow
            for (int i = 0; i < plats.Length; i++) SetupPlatform(i, plats[i], null);
            actLo = 0;
            actHi = plats.Length;
        }

        void ResizePlatformArrays(int n)
        {
            if (platformSr == null)
            {
                platformSr = new SpriteRenderer[n];
                platformTf = new Transform[n];
                platformBaseScaleX = new float[n];
                platformOffX = new float[n];
                platformOffY = new float[n];
                cueSr = new SpriteRenderer[n];
                return;
            }
            System.Array.Resize(ref platformSr, n);
            System.Array.Resize(ref platformTf, n);
            System.Array.Resize(ref platformBaseScaleX, n);
            System.Array.Resize(ref platformOffX, n);
            System.Array.Resize(ref platformOffY, n);
            System.Array.Resize(ref cueSr, n);
        }

        /// <summary>Creates (or, with a pooled renderer, re-configures) the view of platform i.</summary>
        void SetupPlatform(int i, Platform p, SpriteRenderer reuse)
        {
            Sprite sprite = PlatformSprite(p);
            SpriteRenderer sr = reuse;
            if (sr == null) sr = NewSprite("plat" + p.Id + "_" + p.Kind, sprite, 0);
            else
            {
                sr.gameObject.SetActive(true);
                sr.drawMode = SpriteDrawMode.Simple;
                sr.sprite = sprite != null ? sprite : white;
            }
            float width = p.Width.F();
            platformOffX[i] = 0f;
            int piece = PieceIndex(p, width);
            int wp = WidthPieceIndex(p, width);
            if (wp >= 0)
            {
                // A real painted piece for exactly this width: ONE uniform scale, never stretched (owner rule, CUX-9 addendum).
                Sprite sp = region.WidthPieces[wp];
                PlatformMetric m = region.WidthMetrics[wp];
                float ppu = sp.pixelsPerUnit;
                float s = width / ((m.BodyRight - m.BodyLeft) / ppu);
                sr.sprite = sp;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.transform.localScale = new Vector3(s, s, 1f);
                platformBaseScaleX[i] = s;
                platformOffY[i] = -(m.H * 0.5f - m.SurfaceY) / ppu * s;
            }
            else if (piece >= 0)
            {
                // On-style art piece, never stretched out of shape: caps keep their size (9-slice), the middle is stretched sideways only,
                // and a platform narrower than its piece is scaled down uniformly (thinner, same proportions).
                Sprite sp = region.Pieces[piece];
                PlatformMetric m = region.PieceMetrics[piece];
                float ppu = sp.pixelsPerUnit;
                float bodyW = (m.BodyRight - m.BodyLeft) / ppu;
                float s = Mathf.Min(1f, width / bodyW);
                sr.sprite = sp;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2((width / s) * (m.W / (m.BodyRight - m.BodyLeft)), m.H / ppu);
                sr.transform.localScale = new Vector3(s, s, 1f);
                platformBaseScaleX[i] = s;
                platformOffY[i] = -(m.H * 0.5f - m.SurfaceY) / ppu * s;   // the sprite centre sits (H/2 - surface) below the walking line
            }
            else if (region != null && region.PlatSide.a > 0f)
            {
                // Clean code-drawn slab in the region's colours, 0.5 units thick; the top edge is exactly the walking surface.
                Color top = region.PlatTop, side = region.PlatSide;
                if (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap) { top = new Color(1f, 0.55f, 0.75f); side = new Color(0.8f, 0.2f, 0.45f); }
                else if (p.Kind == PlatformKind.Crumble) { side = Color.Lerp(side, Color.gray, 0.45f); top = Color.Lerp(top, Color.gray, 0.35f); }
                sr.sprite = MakeSlab(top, side);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(width + 0.3f, 0.6f);
                sr.transform.localScale = Vector3.one;
                platformBaseScaleX[i] = 1f;
                platformOffY[i] = -0.3f;
            }
            else
            {
                sr.transform.localScale = new Vector3(width, 0.4f, 1f);   // fallback bar
                platformBaseScaleX[i] = width;
                platformOffY[i] = -0.2f;
            }
            sr.color = PlatformTint(p.Kind);
            platformSr[i] = sr;
            platformTf[i] = sr.transform;

            // Non-colour cue (CUX-6): a pattern strip on the walking surface.
            int cue = p.Kind == PlatformKind.Sticky ? 0 : p.Kind == PlatformKind.Slippery ? 1 : p.Kind == PlatformKind.Vanishing ? 2 : ((p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap) && piece < 0 && wp < 0) ? 4 : -1;
            if (cue >= 0)
            {
                if (patterns == null) patterns = new Sprite[5];
                if (patterns[cue] == null) patterns[cue] = MakePattern(cue);
                SpriteRenderer c = cuePool.Count > 0 ? cuePool.Pop() : NewSprite("cue" + p.Id, patterns[cue], 1);
                c.gameObject.SetActive(true);
                c.sprite = patterns[cue];
                c.drawMode = SpriteDrawMode.Tiled;
                c.size = new Vector2(width * 0.8f, 0.25f);
                c.color = Color.white;
                cueSr[i] = c;
            }
            else cueSr[i] = null;
        }

        void ReleasePlatform(int i)
        {
            if (platformSr[i] == null) return;
            platformSr[i].gameObject.SetActive(false);
            platPool.Push(platformSr[i]);
            platformSr[i] = null;
            platformTf[i] = null;
            if (cueSr[i] != null)
            {
                cueSr[i].gameObject.SetActive(false);
                cuePool.Push(cueSr[i]);
                cueSr[i] = null;
            }
        }

        /// <summary>First index in a Y-sorted platform array whose Y is at least y.</summary>
        static int FirstPlatformAtOrAbove(Platform[] plats, float y)
        {
            int lo = 0, hi = plats.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (plats[mid].Y.F() < y) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        static int FirstHazardAtOrAbove(Hazard[] hz, float y)
        {
            int lo = 0, hi = hz.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (hz[mid].Y.F() < y) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// Endless mode, once per frame: picks up a grown tower (arrays are extended, a rare step), then makes sure exactly the platforms and gongs
        /// between a little below the screen and a few rows above it have views. Views leaving the window go back to the pools.
        /// </summary>
        void UpdateEndlessWindow()
        {
            if (session.WorldVersion != builtVersion)
            {
                ResizePlatformArrays(session.Data.Platforms.Length);
                int gongs = session.Data.Hazards.Length;
                if (hazardTf.Length != gongs)
                {
                    System.Array.Resize(ref hazardTf, gongs);
                    System.Array.Resize(ref hazardSr, gongs);
                }
                builtVersion = session.WorldVersion;
            }

            float bottom = camY - camHalfH - WindowBelow;
            float top = camY + camHalfH + WindowAbove;

            Platform[] plats = session.Data.Platforms;
            int lo = FirstPlatformAtOrAbove(plats, bottom);
            int hi = FirstPlatformAtOrAbove(plats, top);
            for (int i = actLo; i < actHi && i < plats.Length; i++) if (i < lo || i >= hi) ReleasePlatform(i);
            for (int i = lo; i < hi; i++)
                if (platformSr[i] == null) SetupPlatform(i, plats[i], platPool.Count > 0 ? platPool.Pop() : null);
            actLo = lo;
            actHi = hi;

            Hazard[] hz = session.Data.Hazards;
            int hlo = FirstHazardAtOrAbove(hz, bottom - 1f);
            int hhi = FirstHazardAtOrAbove(hz, top + 1f);
            for (int i = hazLo; i < hazHi && i < hz.Length; i++) if (i < hlo || i >= hhi) ReleaseHazard(i);
            for (int i = hlo; i < hhi; i++)
                if (hazardSr[i] == null) SetupHazard(i, hz[i], gongPool.Count > 0 ? gongPool.Pop() : null);
            hazLo = hlo;
            hazHi = hhi;
        }

        /// <summary>Drawn size in world units (the Simulation radius is only the collection zone). Rarer gems are drawn bigger.</summary>
        static float VisualDiameter(Pickup pk)
        {
            switch (pk.Kind)
            {
                case PickupKind.Star: return 1.0f;
                case PickupKind.Gem: return 0.75f + 0.1f * Mathf.Clamp(pk.Tag, 0, 3);
                case PickupKind.HiddenGem: return 1.1f;
                default: return 0.7f;
            }
        }

        /// <summary>Draw height of a pickup: always its Sim position (the builder keeps pickups in clear air), so what you touch is what you collect.</summary>
        float PickupVisualY(Pickup pk)
        {
            return pk.Y.F();
        }

        static readonly Color[] RarityTint =
        {
            Color.white, new Color(0.65f, 0.9f, 1f), new Color(0.9f, 0.6f, 1f), new Color(1f, 0.85f, 0.3f)
        };

        void BuildPickups()
        {
            Pickup[] picks = session.Data.Pickups;
            pickupSr = new SpriteRenderer[picks.Length];
            pickupSeen = new bool[picks.Length];
            for (int i = 0; i < picks.Length; i++)
            {
                Pickup pk = picks[i];
                Sprite sprite = null;
                if (art != null)
                {
                    if (pk.Kind == PickupKind.Star) sprite = art.StarFilled;
                    else sprite = ArtCatalog.Pick(art.GemSizes, pk.Kind == PickupKind.HiddenGem ? 2 : Mathf.Min(pk.Tag, 2));
                }
                var sr = NewSprite("pick" + pk.Id, sprite, 5);
                float diameter = VisualDiameter(pk);
                if (sprite != null)
                {
                    Vector2 size = sprite.bounds.size;
                    float s = diameter / Mathf.Max(size.x, size.y);
                    sr.transform.localScale = new Vector3(s, s, 1f);
                }
                else sr.transform.localScale = new Vector3(diameter, diameter, 1f);
                if (pk.Kind == PickupKind.HiddenGem) sr.color = new Color(1f, 0.6f, 1f, 1f);
                else if (pk.Kind == PickupKind.Gem) sr.color = RarityTint[Mathf.Clamp(pk.Tag, 0, 3)];
                sr.transform.position = new Vector3(pk.X.F(), PickupVisualY(pk), 0f);
                pickupSr[i] = sr;
            }
        }

        void BuildHazards()
        {
            Hazard[] hz = session.Data.Hazards;
            hazardTf = new Transform[hz.Length];
            hazardSr = new SpriteRenderer[hz.Length];
            // Only the gong region has hazard art; elsewhere the signature art is a platform, so hazards (surviveWave) get a spiky code-drawn sprite.
            bool signatureIsHazard = SignatureKinds.HazardsUseSignatureArt(SignatureKinds.FromModuleId(session.Region.Rules.SignatureModule));
            gongSprite = signatureIsHazard && region != null && region.Signature.Length > 0 ? region.Signature[0] : HazardSpikes();
            if (endless) return;   // endless gongs come from UpdateEndlessWindow
            for (int i = 0; i < hz.Length; i++) SetupHazard(i, hz[i], null);
            hazLo = 0;
            hazHi = hz.Length;
        }

        void SetupHazard(int i, Hazard h, SpriteRenderer reuse)
        {
            Sprite gong = gongSprite;
            SpriteRenderer sr = reuse != null ? reuse : NewSprite("gong" + h.Id, gong, 6);
            if (reuse != null) sr.gameObject.SetActive(true);
            float diameter = h.Radius.F() * 2.4f;
            if (gong != null)
            {
                Vector2 size = gong.bounds.size;
                float s = diameter / Mathf.Max(size.x, size.y);
                sr.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                sr.transform.localScale = new Vector3(diameter, diameter, 1f);
                sr.color = new Color(1f, 0.75f, 0.2f, 1f);
            }
            hazardTf[i] = sr.transform;
            hazardSr[i] = sr;
        }

        void ReleaseHazard(int i)
        {
            if (hazardSr[i] == null) return;
            hazardSr[i].gameObject.SetActive(false);
            gongPool.Push(hazardSr[i]);
            hazardSr[i] = null;
            hazardTf[i] = null;
        }

        SpriteRenderer[] wornSr = new SpriteRenderer[0];

        /// <summary>The worn gear: one layer per slot, a child of Momo (the layers are painted at the size of the Momo picture), so it moves, squashes and flips with him.</summary>
        void BuildWornGear()
        {
            var list = new List<SpriteRenderer>(4);
            ProgressService pr = GameSession.Progress;
            if (pr != null)
            {
                int order = 11;
                foreach (GearSlot slot in MomoDoll.DrawOrder)
                {
                    Sprite sp = GearLayers.Find(pr.EquippedDef(slot));
                    if (sp == null) continue;
                    SpriteRenderer sr = NewSprite("Worn" + slot, sp, order++, player);
                    sr.transform.localPosition = Vector3.zero;
                    // the layer is painted at the size of the Momo picture; imported at another pixels-per-unit it would be drawn several times too big,
                    // so it is scaled to the player sprite's own bounds (a child inherits the player's scale)
                    Vector2 mb = art != null && art.Player != null ? (Vector2)art.Player.bounds.size : (Vector2)sp.bounds.size;
                    sr.transform.localScale = new Vector3(mb.x / sp.bounds.size.x, mb.y / sp.bounds.size.y, 1f);
                    list.Add(sr);
                }
            }
            wornSr = list.ToArray();
        }

        void BuildPlayer()
        {
            playerSr = NewSprite("Momo", art != null ? art.Player : null, 10);
            player = playerSr.transform;
            if (art != null && art.Player != null)
            {
                Vector2 size = art.Player.bounds.size;
                float s = playerHeight / size.y;
                player.localScale = new Vector3(s, s, 1f);
            }
            else player.localScale = new Vector3(0.9f, playerHeight, 1f);
            BuildWornGear();
        }

        Pop MakePop()
        {
            var sr = NewSprite("pop", null, 20);
            sr.enabled = false;
            return new Pop { Tf = sr.transform, Sr = sr };
        }


        // ---------------- power-up effects (docs/15 FR-PWR-7) ----------------
        // Rings around Momo for the Shield (cyan), Magnet (gold, as wide as its pull) and Flight/Rocket (white / orange), a burst when one starts
        // and trail sparkles while flying. Everything reads the simulation state; nothing here feeds back. Reduced motion: static rings only,
        // no trail and no pops. Allocation free: three pooled renderers and the existing pop pool.

        SpriteRenderer fxShield, fxMagnet, fxBoost;
        SpriteRenderer wingL, wingR, rocketPack, rocketFlame;   // Flight puts wings on Momo, Rocket a rocket pack (owner 2026-10-24)
        int prevPwMask, prevPwUsed, prevShieldSaves;

        static Sprite MakeRing()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) / 0.06f);   // a 0.06 wide band near the rim
                    float fill = r < 0.92f ? 0.10f : 0f;                             // a faint tint inside
                    float a = Mathf.Max(ring, fill);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);   // 2 world units across
        }

        void BuildPowerFx()
        {
            Sprite ring = MakeRing();
            fxShield = NewSprite("FxShield", ring, 11);
            fxMagnet = NewSprite("FxMagnet", ring, 9);
            fxBoost = NewSprite("FxBoost", ring, 9);
            fxShield.enabled = fxMagnet.enabled = fxBoost.enabled = false;
            wingL = NewSprite("WingL", PowerGearArt.Wing, 8);
            wingR = NewSprite("WingR", PowerGearArt.Wing, 8);
            wingR.flipX = true;
            rocketPack = NewSprite("RocketPack", PowerGearArt.Pack, 8);
            rocketFlame = NewSprite("RocketFlame", PowerGearArt.Flame, 7);
            wingL.enabled = wingR.enabled = rocketPack.enabled = rocketFlame.enabled = false;
        }

        static int PowerMask(in SimState st)
        {
            int m = 0;
            if (st.FlightTicks > 0) m |= 1 << (int)PowerUp.Flight;
            if (st.RocketTicks > 0) m |= 1 << (int)PowerUp.Rocket;
            if (st.DoubleJumps > 0) m |= 1 << (int)PowerUp.DoubleJump;
            if (st.WallStickTicks > 0) m |= 1 << (int)PowerUp.WallStick;
            if (st.ShieldTicks > 0) m |= 1 << (int)PowerUp.Shield;
            if (st.MagnetTicks > 0) m |= 1 << (int)PowerUp.Magnet;
            if (st.SlowTicks > 0) m |= 1 << (int)PowerUp.SlowTime;
            return m;
        }

        /// <summary>Sounds and bursts from the change of the power-up state between two ticks.</summary>
        void DerivePowerFx()
        {
            SimState st = session.State;
            if (st.PwUsed == 0 && prevPwMask == 0 && st.ShieldSaves == 0) return;   // no loadout in use: nothing to do
            int mask = PowerMask(in st);
            float cx = st.X.F(), cy = st.Y.F() + 0.5f;
            if (st.PwUsed > prevPwUsed)
            {
                AudioManager.Play(Sfx.PowerUp);
                Spawn(cx, cy, 2.4f, new Color(1f, 0.95f, 0.5f, 0.9f), 0.45f, 0.4f, ArtCatalog.Pick(art != null ? art.Sparkles : null, 1));
            }
            else if (st.ShieldSaves != prevShieldSaves)
            {
                AudioManager.Play(Sfx.PowerUp);   // the shield took a hit or caught a fall
                Spawn(cx, cy, 2.6f, new Color(0.6f, 0.95f, 0.95f, 0.9f), 0.5f, 0.3f, ArtCatalog.Pick(art != null ? art.Sparkles : null, 0));
            }
            else if ((prevPwMask & ~mask) != 0) AudioManager.Play(Sfx.PowerEnd);
            prevPwMask = mask;
            prevPwUsed = st.PwUsed;
            prevShieldSaves = st.ShieldSaves;
        }

        void UpdatePowerFx(float cx, float cy)
        {
            if (fxShield == null) return;
            SimState st = session.State;
            bool shield = st.ShieldTicks > 0, magnet = st.MagnetTicks > 0, boost = false;   // the old ring is gone: Flight shows wings and Rocket a rocket pack
            if (fxShield.enabled != shield) fxShield.enabled = shield;
            if (fxMagnet.enabled != magnet) fxMagnet.enabled = magnet;
            if (fxBoost.enabled != boost) fxBoost.enabled = boost;
            if (shield)
            {
                float k = 2.1f * (reduced ? 1f : 1f + 0.05f * Mathf.Sin(st.Tick * 0.1f));
                fxShield.transform.position = new Vector3(cx, cy, 0f);
                fxShield.transform.localScale = new Vector3(k * 0.5f, k * 0.5f, 1f);
                fxShield.color = new Color(0.5f, 0.95f, 0.95f, 0.85f);
            }
            if (magnet)
            {
                float d = (session.Cfg.StarRadius + session.Cfg.MagnetExtraRadius).F() * 2f;   // as wide as the pull
                fxMagnet.transform.position = new Vector3(cx, cy, 0f);
                fxMagnet.transform.localScale = new Vector3(d * 0.5f, d * 0.5f, 1f);
                fxMagnet.color = new Color(1f, 0.85f, 0.3f, 0.45f);
            }
            if (boost)
            {
                float k = st.RocketTicks > 0 ? 2.8f : 3.2f;
                fxBoost.transform.position = new Vector3(cx, cy - 0.2f, 0f);
                fxBoost.transform.localScale = new Vector3(k * 0.5f, k * 0.5f, 1f);
                fxBoost.color = st.RocketTicks > 0 ? new Color(1f, 0.6f, 0.25f, 0.7f) : new Color(0.75f, 0.92f, 1f, 0.7f);
            }
        }

        /// <summary>Wings (Flight) or a rocket pack with a flame (Rocket) on the player while the power-up runs. Reduced motion: no flapping, no flicker.</summary>
        void UpdateGearFx(float cx, float cy, float h)
        {
            if (wingL == null) return;
            SimState st = session.State;
            bool wings = st.FlightTicks > 0, rocket = st.RocketTicks > 0;
            if (wingL.enabled != wings) wingL.enabled = wingR.enabled = wings;
            if (rocketPack.enabled != rocket) rocketPack.enabled = rocketFlame.enabled = rocket;
            float t = st.Tick / 60f;
            if (wings)
            {
                float flap = reduced ? 0f : Mathf.Sin(t * 14f);
                float s = playerHeight * 0.95f / 2f;
                float lift = 0.12f * playerHeight * flap;
                wingL.transform.position = new Vector3(cx - playerHeight * 0.22f, cy + playerHeight * 0.05f + lift, 0f);
                wingR.transform.position = new Vector3(cx + playerHeight * 0.22f, cy + playerHeight * 0.05f + lift, 0f);
                wingL.transform.localScale = new Vector3(s, s * (1f + 0.12f * flap), 1f);
                wingR.transform.localScale = new Vector3(s, s * (1f + 0.12f * flap), 1f);
                wingL.transform.rotation = Quaternion.Euler(0f, 0f, 14f * flap);
                wingR.transform.rotation = Quaternion.Euler(0f, 0f, -14f * flap);
            }
            if (rocket)
            {
                float s = playerHeight * 0.62f / 1.33f;
                rocketPack.transform.position = new Vector3(cx, cy + playerHeight * 0.05f, 0f);
                rocketPack.transform.localScale = new Vector3(s, s, 1f);
                float flick = reduced ? 1f : 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(t * 40f));
                rocketFlame.transform.position = new Vector3(cx, cy - playerHeight * 0.30f, 0f);
                rocketFlame.transform.localScale = new Vector3(s * 0.9f, s * flick, 1f);
            }
        }

        // ---------------- per tick / per frame ----------------

        /// <summary>Call once after every Sim tick: remembers the previous and current player position for interpolation and spawns effects.</summary>
        public void OnTick(bool coinTaken, float coinX, float coinY)
        {
            prevX = curX;
            prevKill = curKill;
            curKill = session.State.KillY.F();
            prevY = curY;
            curX = session.State.X.F();
            curY = session.State.Y.F();
            bool grounded = session.State.Grounded;
            DeriveAudio(grounded);
            DerivePowerFx();
            if (grounded && !wasGrounded) { squash = 1f; Spawn(curX, curY + 0.1f, 0.9f, new Color(1f, 1f, 1f, 0.6f), 0.25f, 0.2f, ArtCatalog.Pick(art != null ? art.DustPuff : null, 0)); }
            wasGrounded = grounded;
            if (coinTaken) SpawnPickupEffects();
        }

        /// <summary>Jump, bounce, land, hit and fall sounds, from the change between the previous and the current simulation state (no allocation).</summary>
        void DeriveAudio(bool grounded)
        {
            SimState st = session.State;
            float vy = st.Vy.F();
            if (prevVy <= 0f && vy > 0f && !st.Dead)
            {
                Platform[] plats = session.Data.Platforms;
                bool bounce = false;
                if (prevVy < 0f && st.GroundId >= 0 && st.GroundId < plats.Length)
                {
                    PlatformKind k = plats[st.GroundId].Kind;
                    bounce = k == PlatformKind.Bounce || k == PlatformKind.BounceCap;
                }
                AudioManager.Play(bounce ? Sfx.Bounce : Sfx.Jump);
            }
            else if (grounded && !wasGrounded) AudioManager.Play(Sfx.Land);
            if (st.Dead && !wasDead) AudioManager.Play(Sfx.Fall);
            else if (st.Hits > prevHits) AudioManager.Play(Sfx.Fall);
            wasDead = st.Dead;
            prevHits = st.Hits;
            prevVy = vy;
        }

        /// <summary>Pickup feedback drawn where the pickup was (not at the character): a gem shows its own gem, a star its star, a coin a gold sparkle.</summary>
        void SpawnPickupEffects()
        {
            Pickup[] picks = session.Data.Pickups;
            for (int i = 0; i < picks.Length; i++)
            {
                if (pickupSeen[i] || session.State.PickupTaken == null || !session.State.PickupTaken[i]) continue;
                pickupSeen[i] = true;
                Pickup pk = picks[i];
                float x = pk.X.F(), y = pk.Y.F();
                AudioManager.Play(pk.Kind == PickupKind.Star ? Sfx.Star : (pk.Kind == PickupKind.Gem || pk.Kind == PickupKind.HiddenGem) ? Sfx.Gem : Sfx.Coin);
                if (pk.Kind == PickupKind.Gem || pk.Kind == PickupKind.HiddenGem)
                {
                    Sprite gem = art != null ? ArtCatalog.Pick(art.GemSizes, pk.Kind == PickupKind.HiddenGem ? 2 : Mathf.Min(pk.Tag, 2)) : null;
                    Color tint = pk.Kind == PickupKind.HiddenGem ? new Color(1f, 0.6f, 1f, 1f) : RarityTint[Mathf.Clamp(pk.Tag, 0, 3)];
                    Spawn(x, y, 0.9f, tint, 0.45f, 1.1f, gem);
                }
                else if (pk.Kind == PickupKind.Star)
                    Spawn(x, y, 1.3f, Color.white, 0.5f, 0.9f, art != null ? art.StarFilled : null);
                else
                    Spawn(x, y, 0.7f, new Color(1f, 0.9f, 0.3f, 1f), 0.3f, 0.8f, ArtCatalog.Pick(art != null ? art.Sparkles : null, 0));
            }
        }

        void Spawn(float x, float y, float size, Color color, float life, float rise, Sprite sprite)
        {
            if (reduced) return;
            Pop p = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Length;
            p.Age = 0f;
            p.Life = life;
            p.Size = size;
            p.Rise = rise;
            p.Sr.sprite = sprite != null ? sprite : white;
            p.Sr.color = color;
            p.Sr.enabled = true;
            p.Tf.position = new Vector3(x, y, 0f);
            if (sprite != null)
            {
                Vector2 sz = sprite.bounds.size;
                float s = size / Mathf.Max(sz.x, sz.y);
                p.Tf.localScale = new Vector3(s, s, 1f);
            }
            else p.Tf.localScale = new Vector3(size * 0.4f, size * 0.4f, 1f);
        }

        const float camSmoothing = 11f;
        float frameDt = 1f / 60f;

        public void Render(float alpha, float deltaTime)
        {
            if (session == null) return;
            frameDt = deltaTime;
            SimState s = session.State;
            int tick = s.Tick;

            // Player (interpolated, squash and stretch from vertical speed and landing).
            float px = Mathf.Lerp(prevX, curX, alpha);
            float py = Mathf.Lerp(prevY, curY, alpha);
            float vy = s.Vy.F();
            squash = Mathf.Max(0f, squash - deltaTime * 6f);
            float stretch = reduced ? 1f : 1f + Mathf.Clamp(Mathf.Abs(vy) * 1.6f, 0f, 0.25f) - squash * 0.22f;
            float h = playerHeight * stretch;
            float widthFactor = 1f / Mathf.Max(0.7f, stretch);
            if (art != null && art.Player != null)
            {
                Vector2 size = art.Player.bounds.size;
                float sy = h / size.y;
                float sx = (playerHeight / size.y) * widthFactor;
                float facing = s.Vx.Raw < -1000 ? -1f : (s.Vx.Raw > 1000 ? 1f : Mathf.Sign(player.localScale.x));
                player.localScale = new Vector3(sx * (facing < 0 ? -1f : 1f), sy, 1f);
            }
            player.position = new Vector3(px, py + h * 0.5f, 0f);
            player.rotation = reduced ? Quaternion.identity : Quaternion.Euler(0f, 0f, Mathf.Clamp(-s.Vx.F() * 40f, -12f, 12f));
            playerSr.color = s.Invuln > 0 && (reduced || (tick / 10) % 2 == 0) ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            UpdatePowerFx(px, py + h * 0.5f);
            UpdateGearFx(px, py + h * 0.5f, h);

            // Platforms (moving ones follow the sim, vanishing ones blink, crumbling ones fade).
            if (endless) UpdateEndlessWindow();
            Platform[] plats = session.Data.Platforms;
            for (int i = actLo; i < actHi; i++)
            {
                if (platformSr[i] == null) continue;
                Platform p = plats[i];
                float x = p.Kind == PlatformKind.Moving ? session.Sim.PlatformX(p.Id, tick).F() : p.X.F();
                float y = p.Y.F();
                var sr = platformSr[i];
                platformTf[i].position = new Vector3(x + platformOffX[i], y + platformOffY[i], 0f);
                if (p.Kind == PlatformKind.Vanishing || p.Kind == PlatformKind.Crumble)
                {
                    bool solid = session.Sim.IsSolid(p.Id, tick, s.CrumbleTick);
                    Color c = PlatformTint(p.Kind);
                    // A crumbled platform is gone for good (no ghost to walk onto); a vanishing one comes back, so it stays a faint ghost.
                    c.a = solid ? 1f : (p.Kind == PlatformKind.Crumble ? 0f : 0.2f);
                    if (p.Kind == PlatformKind.Crumble && s.CrumbleTick[i] >= 0 && solid) c.a = reduced ? 0.7f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(tick * 0.3f));
                    // Warning before a vanishing platform disappears under the player: a slow pulse, about 2 per second (CUX-6), steady dim with reduced motion.
                    else if (solid && p.Kind == PlatformKind.Vanishing && session.Sim.VanishWarning(p.Id, tick)) c.a = reduced ? 0.6f : 0.65f + 0.35f * Mathf.Sin(tick * 0.2f);
                    sr.color = c;
                    if (cueSr[i] != null) cueSr[i].color = new Color(1f, 1f, 1f, c.a);
                }
                if (cueSr[i] != null) cueSr[i].transform.position = new Vector3(x, y - 0.14f, 0f);
            }

            // Pickups (taken ones hide; stars pulse gently, unless reduced motion is on).
            Pickup[] picks = session.Data.Pickups;
            float pulse = reduced ? 1f : 1f + 0.08f * Mathf.Sin(tick * 0.12f);
            for (int i = 0; i < picks.Length; i++)
            {
                var sr = pickupSr[i];
                if (s.PickupTaken != null && s.PickupTaken[i]) { if (sr.enabled) sr.enabled = false; continue; }
                if (picks[i].Kind == PickupKind.Star)
                {
                    float d0 = VisualDiameter(picks[i]);
                    Vector2 sz = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
                    float k = d0 / Mathf.Max(sz.x, sz.y) * pulse;
                    sr.transform.localScale = new Vector3(k, k, 1f);
                }
            }

            // Hazards.
            for (int i = hazLo; i < hazHi; i++)
            {
                if (hazardTf[i] == null) continue;
                Fx hx, hy;
                session.Sim.HazardPosition(session.Data.Hazards[i].Id, tick, out hx, out hy);
                hazardTf[i].position = new Vector3(hx.F(), hy.F(), 0f);
                hazardTf[i].rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(tick * 0.05f + i) * 12f);
            }

            // Effects.
            for (int i = 0; i < pops.Length; i++)
            {
                Pop p = pops[i];
                if (p.Age > p.Life) { if (p.Sr.enabled) p.Sr.enabled = false; continue; }
                p.Age += deltaTime;
                float t = Mathf.Clamp01(p.Age / p.Life);
                Color c = p.Sr.color;
                c.a = 1f - t;
                p.Sr.color = c;
                p.Tf.position += new Vector3(0f, p.Rise * deltaTime, 0f);
                p.Tf.localScale *= 1f + deltaTime * 2.5f;
            }

            UpdatePortal(deltaTime);
            UpdateCamera(Mathf.Lerp(prevKill, curKill, alpha));
            UpdateBackground();
            UpdateWalls();
        }

        /// <summary>Fits the view width to the screen width for the current aspect (called every frame: rotation, split screen, tests).</summary>
        void FitCamera()
        {
            float aspect = AspectOverride > 0f ? AspectOverride : cam.aspect;
            float h = camHalfW / Mathf.Max(0.3f, aspect);
            if (Mathf.Abs(h - camHalfH) > 0.0001f)
            {
                camHalfH = h;
                cam.orthographicSize = h;
            }
        }

        /// <summary>
        /// The bottom edge of the screen IS the danger line of the simulation (owner rules, 2026-10-19): the screen scrolls up after the first jump,
        /// platforms slide down, and anything below the edge is gone. The line is interpolated between ticks for smooth motion at any frame rate.
        /// </summary>
        void UpdateCamera(float killY)
        {
            FitCamera();
            camY = killY + camHalfH;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            cam.transform.position = new Vector3(camHalfW, camY, -10f);
        }

        void UpdateBackground()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                float scrolled = reduced ? 0f : camY * l.Factor;
                float offset = Mathf.Repeat(scrolled, l.Height);
                int baseIdx = Mathf.FloorToInt(scrolled / l.Height);
                for (int k = 0; k < l.Tiles.Length; k++)
                {
                    // Every other tile is mirrored vertically so neighbouring tiles meet on identical pixels: no visible seam.
                    bool mirror = (((baseIdx + k - 1) % 2) + 2) % 2 == 1;
                    l.Tiles[k].transform.localScale = new Vector3(l.Scale, mirror ? -l.Scale : l.Scale, 1f);
                    float cy = camY - offset + (k - 1) * l.Height;
                    l.Tiles[k].transform.position = new Vector3(camHalfW, cy + l.Height * 0.5f - camHalfH * 0.0f, 0f);
                }
            }
        }

        void UpdateWalls()
        {
            if (wallL == null) return;
            float bottom = camY - camHalfH - wallSegHeight;
            int first = Mathf.FloorToInt(bottom / wallSegHeight);
            for (int i = 0; i < wallL.Length; i++)
            {
                float y = (first + i) * wallSegHeight + wallSegHeight * 0.5f;
                bool tiled = region != null && region.WallTileL != null;
                float inset = tiled ? wallWidth * 0.5f - 0.1f : 0.3f;   // the playfield edge is x = 0 / screen width; the wall overlaps it by 0.1
                if (wallL[i] != null) wallL[i].transform.position = new Vector3(inset, y, 0f);
                if (wallR[i] != null) wallR[i].transform.position = new Vector3(camHalfW * 2f - inset, y, 0f);
            }
        }

        void UpdateKillZone(float killY)
        {
            killZone.transform.position = new Vector3(camHalfW, killY - 4f, 0f);
            killZone.transform.localScale = new Vector3(camHalfW * 2f + 2f, 8f, 1f);
            killLine.transform.position = new Vector3(camHalfW, killY, 0f);
            killLine.transform.localScale = new Vector3(camHalfW * 2f + 2f, 0.08f, 1f);
            dangerBand.transform.position = new Vector3(camHalfW, killY + 0.19f, 0f);
        }
    }
}
