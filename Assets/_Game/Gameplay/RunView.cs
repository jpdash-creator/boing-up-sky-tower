using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;

namespace FluffyClimbers.Gameplay
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
        int[] platformPose;   // Spore Pop painted pose in use (-1 calm, 1 swollen, 2 flat; 0 = not a painted spore pop)
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
        // Endless re-base (EndlessWorld): the Sim heights and the platform / gong ids of the window are shifted about every 500 floors. The view follows in the same frame
        // (SyncRebase): its index-based caches move down, every stored world position moves by the same amount, and the parallax / wall patterns use the ABSOLUTE height
        // (originY + camera) so they stay seamless. Between re-bases nothing here costs anything.
        int seenRebase, seenDropP, seenDropH;
        double originY;   // absolute height (u) of the window's zero
        int actLo, actHi;             // active platform views are [actLo, actHi)
        int hazLo, hazHi;             // active gong views are [hazLo, hazHi)
        readonly Stack<SpriteRenderer> platPool = new Stack<SpriteRenderer>();
        readonly Stack<SpriteRenderer> gongPool = new Stack<SpriteRenderer>();
        Sprite gongSprite;
        static Sprite hazardSpikes;
        static Sprite HazardSpikes() { if (hazardSpikes == null) hazardSpikes = MakeHazardSpikes(); return hazardSpikes; }
        /// <summary>Views kept below / above the screen edges in endless mode, world units.</summary>
        const float WindowBelow = 3f, WindowAbove = 8f;

        float camHalfH, camHalfW, camY, designHalfH;
        /// <summary>World height of the HUD strips (RunLayout): bottomWorld is always 0 (the danger edge is the screen bottom); topWorld is the room reserved for the HUD rows.</summary>
        float bottomWorld, topWorld;
        float prevX, prevY, curX, curY, prevKill, curKill;
        bool wasGrounded = true;
        float prevVy;           // audio cues are derived from state changes here; the simulation knows nothing about sound
        bool wasDead;
        int prevHits;
        float squash;   // 0..1 landing squash timer
        bool reduced;   // reduced-motion setting, read once per run
        SpriteRenderer dangerBand;
        SpecialPlatformFx specialFx;   // motion of the region 1 special platforms (squash, tilt, mirror; no drawn cues, doc 24)

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
            seenRebase = s.RebaseCount;
            seenDropP = s.DroppedPlatforms;
            seenDropH = s.DroppedHazards;
            originY = s.OriginYRaw / 65536.0;
            reduced = GameSettings.ReducedMotion;
            AudioManager.SetMusic(MusicTrack.Run, s.Region.Index);
            art = catalog;
            cam = camera;
            region = art != null ? art.ForRegion(s.Region.Index) : null;
            white = MakeWhite();
            specialFx = new SpecialPlatformFx((n, sp, o) => NewSprite(n, sp, o), reduced);

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
            camY = curKill + camHalfH - bottomWorld;
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

        /// <summary>Index into region.KindPieces of the painted piece of a region 1 special platform (kind, thorny side Platform.Dir, nearest baked width), or -1 (no art yet: tinted normal platform, no drawn cues).</summary>
        int KindPieceIndexFor(Platform p, float width)
        {
            if (region == null || !SpecialPlatformFx.Handles(p.Kind)) return -1;
            return region.KindPieceIndex(p.Kind, PosedKind(p.Kind) ? -1 : (p.Dir < 0 ? -1 : 1), width);   // posed kinds start on pose -1; the pose is swapped per frame (see PoseOf)
        }

        /// <summary>
        /// Kinds whose painted pieces are poses chosen by the Sim state (piece Dir = pose): Spore Pop (calm -1, swollen 1, flat 2), Clam Shell Pad (open -1, shut 1), Pufferfish Pad (calm -1, swelling 1, puffed 2),
        /// region 4: Licorice Belt (tread frame A -1, B 1), Taffy Stretch (4 widths: narrow -1, 2nd 1, 3rd 2, wide 3), Jelly Wobble (calm -1, squashed 1), Pinwheel Gust (spin frame A -1, B 1).
        /// </summary>
        static bool PosedKind(PlatformKind k)
        {
            return k == PlatformKind.SporePop || k == PlatformKind.ClamShellPad || k == PlatformKind.PufferfishPad
                || k == PlatformKind.LicoriceBelt || k == PlatformKind.TaffyStretch || k == PlatformKind.JellyWobble || k == PlatformKind.PinwheelGust
                || k == PlatformKind.BambooFlick || k == PlatformKind.MalletSmash;
        }

        /// <summary>Walkable width the painted pose of a Taffy Stretch shows (four steps from the narrow 2.4 u to the wide 5.2 u: 2.4, 3.3, 4.3, 5.2); every other kind keeps its Platform.Width.</summary>
        static float PoseWidth(Platform p, int pose)
        {
            if (p.Kind != PlatformKind.TaffyStretch) return p.Width.F();
            float narrow = p.Width.F(), span = 2f * p.Amplitude.F();
            return narrow + span * (pose < 0 ? 0 : pose) / 3f;
        }

        /// <summary>Pose number of a posed platform at a tick (the Sim clocks: Spore Pop landing clock, Clam (tick + Phase) % 144 &lt; 72 = open, Pufferfish (tick + Phase) % 180: 0..95 calm, 96..131 swelling, 132..179 puffed).</summary>
        int PoseOf(Platform p, int tick, in SimState s)
        {
            switch (p.Kind)
            {
                case PlatformKind.SporePop: { int ph = session.Sim.SporePopPhase(p.Id, tick, s.CrumbleTick); return ph == 0 ? -1 : (ph == 1 ? 1 : 2); }
                case PlatformKind.ClamShellPad: return session.Sim.ClamOpen(p.Id, tick) ? -1 : 1;
                case PlatformKind.PufferfishPad: { int ph = session.Sim.PufferPhase(p.Id, tick); return ph == 0 ? -1 : (ph == 1 ? 1 : 2); }
                // Region 4: the belt and the pinwheel alternate two loop frames (reduced motion: still); the taffy picks the painted width nearest its Sim width; the jelly is squashed for the first
                // 24 ticks of a wobble (the wobble itself is the Sim movement of the cube)
                case PlatformKind.LicoriceBelt: return reduced ? -1 : ((tick / 6) % 2 == 0 ? -1 : 1);
                case PlatformKind.PinwheelGust: return reduced ? -1 : ((tick / 8) % 2 == 0 ? -1 : 1);
                case PlatformKind.TaffyStretch:
                    {
                        float span = 2f * p.Amplitude.F();
                        int step = span < 0.01f ? 0 : Mathf.RoundToInt((session.Sim.WidthAt(p.Id, tick).F() - p.Width.F()) / span * 3f);   // nearest of the four painted widths
                        return step <= 0 ? -1 : step;
                    }
                case PlatformKind.JellyWobble:
                    {
                        int landed = s.CrumbleTick[p.Id];
                        return landed >= 0 && tick - landed < 24 ? 1 : -1;
                    }
                // Region 5: the Bamboo Flick is sprung for the half second after it flicked the player (rest -1, sprung 1); the Mallet Smash is down from the smash until it lifts again (raised -1, smashed 1); both read the Sim clocks
                case PlatformKind.BambooFlick: return s.GroundId == p.Id && tick - s.LandTick < 30 ? 1 : -1;
                case PlatformKind.MalletSmash: return session.Sim.MalletDown(p.Id, tick) ? 1 : -1;
                default: return 0;
            }
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
            // Art hook (doc 24): a painted sprite for a special platform kind (RegionArt.KindSprite, from the OpenArt width sheets) wins; until then the normal platform is tinted.
            if (SpecialPlatformFx.Handles(p.Kind)) { Sprite ks = region.KindSprite(p.Kind); if (ks != null) return ks; }
            switch (p.Kind)
            {
                case PlatformKind.Moving: return region.Moving ?? region.Normal;
                case PlatformKind.Crumble: return region.Crumble ?? region.Normal;
                case PlatformKind.Bounce:
                case PlatformKind.BounceCap: return region.Bounce ?? region.Normal;
                default: return (p.Width.F() < 3.2f && region.Narrow != null) ? region.Narrow : region.Normal;
            }
        }

        static Color PlatformTint(PlatformKind kind)
        {
            switch (kind)
            {
                case PlatformKind.Sticky: return new Color(1f, 0.7f, 0.9f, 1f);
                case PlatformKind.Slippery: return new Color(0.7f, 0.9f, 1f, 1f);
                case PlatformKind.Vanishing: return new Color(0.8f, 0.7f, 1f, 1f);
                default: return SpecialPlatformFx.Handles(kind) ? SpecialPlatformFx.Tint(kind) : Color.white;
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
            if (specialFx != null) specialFx.Resize(n);
            if (platformSr == null)
            {
                platformSr = new SpriteRenderer[n];
                platformTf = new Transform[n];
                platformBaseScaleX = new float[n];
                platformPose = new int[n];
                platformOffX = new float[n];
                platformOffY = new float[n];
                return;
            }
            System.Array.Resize(ref platformSr, n);
            System.Array.Resize(ref platformTf, n);
            System.Array.Resize(ref platformBaseScaleX, n);
            System.Array.Resize(ref platformPose, n);
            System.Array.Resize(ref platformOffX, n);
            System.Array.Resize(ref platformOffY, n);
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
            int kp = KindPieceIndexFor(p, width);
            if (kp >= 0)
            {
                // Painted special platform (doc 24) for this kind and width (Thorn Bush: the baked piece of its thorny side, Platform.Dir): ONE uniform scale, never stretched.
                KindPiece kpc = region.KindPieces[kp];
                Sprite sp = kpc.Sprite;
                PlatformMetric m = kpc.Metric;
                float ppu = sp.pixelsPerUnit;
                float s = width / ((m.BodyRight - m.BodyLeft) / ppu);
                sr.sprite = sp;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.transform.localScale = new Vector3(s, s, 1f);
                platformBaseScaleX[i] = s;
                platformOffY[i] = -(m.H * 0.5f - m.SurfaceY) / ppu * s;
            }
            else if (wp >= 0)
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
            sr.color = kp >= 0 ? Color.white : PlatformTint(p.Kind);   // painted art keeps its own colours; the tint is only the placeholder
            sr.flipX = false;                                          // a pooled ladybug view may still be mirrored
            sr.transform.rotation = Quaternion.identity;   // a pooled seesaw view may still be tilted
            platformSr[i] = sr;
            platformTf[i] = sr.transform;
            platformPose[i] = kp >= 0 && PosedKind(p.Kind) ? -1 : 0;
            if (SpecialPlatformFx.Handles(p.Kind))
            {
                if (region != null) { specialFx.SetKnob(region.SeesawKnob, region.SeesawKnobTopDropPx); specialFx.SetStem(region.SproutStem, region.SproutStemTopDropPx); specialFx.SetLog(region.RollingLog, white); }
                specialFx.Attach(i, p, kp >= 0);
            }
            else specialFx.Release(i);

        }

        /// <summary>Swaps the painted piece of a posed platform to another pose (same width, same uniform scale rule as SetupPlatform); keeps the current one when the pose has no piece.</summary>
        void SwapPosePiece(int i, Platform p, SpriteRenderer sr, int pose)
        {
            platformPose[i] = pose;
            if (region == null) return;
            float width = PoseWidth(p, pose);
            int kp = region.KindPieceIndex(p.Kind, pose, width);
            if (kp < 0) return;
            KindPiece kpc = region.KindPieces[kp];
            float ppu = kpc.Sprite.pixelsPerUnit;
            float sc = width / ((kpc.Metric.BodyRight - kpc.Metric.BodyLeft) / ppu);
            sr.sprite = kpc.Sprite;
            sr.transform.localScale = new Vector3(sc, sc, 1f);
            platformBaseScaleX[i] = sc;
            platformOffY[i] = -(kpc.Metric.H * 0.5f - kpc.Metric.SurfaceY) / ppu * sc;
        }

        void ReleasePlatform(int i)
        {
            if (platformSr[i] == null) return;
            platformSr[i].gameObject.SetActive(false);
            platPool.Push(platformSr[i]);
            platformSr[i] = null;
            platformTf[i] = null;
            specialFx.Release(i);
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

        /// <summary>Moves the contents of a view array down by k slots (the first k are dropped) and clears the tail. In place, no allocation.</summary>
        static void ShiftDown<T>(T[] a, int k)
        {
            if (a == null || k <= 0) return;
            if (k >= a.Length) { System.Array.Clear(a, 0, a.Length); return; }
            System.Array.Copy(a, k, a, 0, a.Length - k);
            System.Array.Clear(a, a.Length - k, k);
        }

        /// <summary>
        /// Endless re-base, called at the start of OnTick and Render (a single integer compare when nothing happened). When the world was re-based the Sim lowered all heights
        /// by one amount and renumbered platforms and gongs: this lowers every height the view stored by the same amount in the same frame (previous / current player and kill
        /// line, the camera, the effect pops), moves the index-based caches (platform and gong views, special platform parts, the active windows) down by the dropped counts, and
        /// takes the new absolute origin. Platform and gong views are positioned from the Sim every frame, so they need nothing else. No allocation, no visual pop.
        /// </summary>
        void SyncRebase()
        {
            if (!endless || session.RebaseCount == seenRebase) return;
            int dropP = session.DroppedPlatforms - seenDropP;
            int dropH = session.DroppedHazards - seenDropH;
            double newOrigin = session.OriginYRaw / 65536.0;
            float d = (float)(newOrigin - originY);

            if (dropP > 0 && platformSr != null)
            {
                for (int i = 0; i < dropP && i < platformSr.Length; i++) ReleasePlatform(i);   // (already below the window in practice)
                ShiftDown(platformSr, dropP);
                ShiftDown(platformTf, dropP);
                ShiftDown(platformBaseScaleX, dropP);
                ShiftDown(platformPose, dropP);
                ShiftDown(platformOffX, dropP);
                ShiftDown(platformOffY, dropP);
                specialFx.ShiftDown(dropP);
                actLo = actLo > dropP ? actLo - dropP : 0;
                actHi = actHi > dropP ? actHi - dropP : 0;
            }
            if (dropH > 0 && hazardSr != null)
            {
                for (int i = 0; i < dropH && i < hazardSr.Length; i++) ReleaseHazard(i);
                ShiftDown(hazardSr, dropH);
                ShiftDown(hazardTf, dropH);
                hazLo = hazLo > dropH ? hazLo - dropH : 0;
                hazHi = hazHi > dropH ? hazHi - dropH : 0;
            }

            prevY -= d;
            curY -= d;
            prevKill -= d;
            curKill -= d;
            camY -= d;
            for (int i = 0; i < pops.Length; i++)
                if (pops[i] != null && pops[i].Tf != null) { Vector3 pp = pops[i].Tf.position; pp.y -= d; pops[i].Tf.position = pp; }
            if (cam != null) ApplyCamera();

            originY = newOrigin;
            seenRebase = session.RebaseCount;
            seenDropP = session.DroppedPlatforms;
            seenDropH = session.DroppedHazards;
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
                case PickupKind.Gem: return pk.Tag == SimConfig.DoubleGemTag ? 1.3f : 0.75f + 0.1f * Mathf.Clamp(pk.Tag, 0, 3);   // a double gem (worth 2) is drawn clearly bigger: size, not only colour (CUX-6)
                case PickupKind.HiddenGem: return pk.Tag == 1 ? 1.35f : 1.1f;
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
                    else sprite = ArtCatalog.Pick(art.GemSizes, pk.Kind == PickupKind.HiddenGem ? 2 : Mathf.Min(pk.Tag, 2));   // Tag 4 (double) uses the biggest gem
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
                else if (pk.Kind == PickupKind.Gem) sr.color = pk.Tag == SimConfig.DoubleGemTag ? new Color(1f, 0.95f, 0.55f, 1f) : RarityTint[Mathf.Clamp(pk.Tag, 0, 3)];
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

        SpriteRenderer[] partSr = new SpriteRenderer[0];
        CharacterDef who;
        /// <summary>Size in world units of the character canvas (every part picture is one full canvas); the player transform is scaled so it is playerHeight tall.</summary>
        Vector2 bodySize = new Vector2(0.9f, 1f);

        /// <summary>The first part picture of the player; its bounds are the canvas (tests).</summary>
        public SpriteRenderer PlayerRenderer { get { return playerSr; } }
        public SpriteRenderer[] PartRenderers { get { return partSr; } }
        /// <summary>The character this run draws (read by the AltTester level sweep, LevelSweepApi).</summary>
        public CharacterDef Character { get { return who; } }

        /// <summary>
        /// The player is the selected character built from its parts (docs/18): the same <see cref="PartsAssembler"/> list the character screens draw, every part a
        /// child of the player transform placed on the canvas rectangle the rig gives it, so the run looks exactly like the character screen. The parts share one sorting group.
        /// </summary>
        void BuildPlayer()
        {
            who = CharacterCatalog.Selected;
            var go = new GameObject("Momo");
            go.transform.SetParent(worldRoot, false);
            player = go.transform;
            var group = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = 10;
            List<PartDraw> draws = PartsAssembler.Build(who, art);
            var all = new List<SpriteRenderer>(draws.Count);
            for (int i = 0; i < draws.Count; i++)
            {
                PartDraw d = draws[i];
                SpriteRenderer sr = NewSprite("Part" + d.Name, d.Sprite, i, player);
                Vector2 size = d.Sprite.bounds.size;
                if (i == 0) bodySize = size;
                Vector3 pos, scale;
                d.LocalTransform(bodySize, out pos, out scale);
                sr.transform.localScale = scale;
                sr.transform.localPosition = pos;
                all.Add(sr);
            }
            partSr = all.ToArray();
            playerSr = partSr.Length > 0 ? partSr[0] : NewSprite("Momo", null, 10, player);
            float s = playerHeight / bodySize.y;
            player.localScale = partSr.Length > 0 ? new Vector3(s, s, 1f) : new Vector3(0.9f, playerHeight, 1f);
        }

        void SetPlayerColor(Color c)
        {
            for (int i = 0; i < partSr.Length; i++) partSr[i].color = c;
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
        SpriteRenderer wingPair, wingL, wingR, rocketPack, rocketFlame;   // Flight puts wings on Momo, Rocket a rocket pack (owner 2026-10-24)
        /// <summary>Painted wing pair height relative to the player height (the picture is about 2:1, so its width is twice this).</summary>
        const float WingsHeightFactor = 0.8f;
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
            Sprite wingsPic = PowerGearArt.WingsPicture;
            if (wingsPic != null)
            {
                wingPair = NewSprite("Wings", wingsPic, 8);   // one centred, symmetric picture behind the character
                wingPair.enabled = false;
            }
            else
            {
                wingL = NewSprite("WingL", PowerGearArt.Wing, 8);   // fallback: the code-drawn wing, mirrored (CUX-10)
                wingR = NewSprite("WingR", PowerGearArt.Wing, 8);
                wingR.flipX = true;
                wingL.enabled = wingR.enabled = false;
            }
            rocketPack = NewSprite("RocketPack", PowerGearArt.Pack, 8);
            rocketFlame = NewSprite("RocketFlame", PowerGearArt.Flame, 7);
            rocketPack.enabled = rocketFlame.enabled = false;
        }

        static int PowerMask(in SimState st)
        {
            int m = 0;
            if (st.FlightTicks > 0) m |= 1 << (int)PowerUp.Flight;
            if (st.RocketTicks > 0) m |= 1 << (int)PowerUp.Rocket;
            if (st.DoubleJumpTicks > 0) m |= 1 << (int)PowerUp.DoubleJump;
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
            if (st.PwUsed == 0 && prevPwMask == 0 && st.ShieldSaves == 0) return;   // no power-up in use: nothing to do
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
            if (rocketPack == null) return;
            SimState st = session.State;
            bool wings = st.FlightTicks > 0, rocket = st.RocketTicks > 0;
            if (wingPair != null) { if (wingPair.enabled != wings) wingPair.enabled = wings; }
            else if (wingL.enabled != wings) wingL.enabled = wingR.enabled = wings;
            if (rocketPack.enabled != rocket) rocketPack.enabled = rocketFlame.enabled = rocket;
            float t = st.Tick / 60f;
            if (wings)
            {
                float flap = reduced ? 0f : Mathf.Sin(t * 14f);
                if (wingPair != null)
                {
                    // one symmetric picture, as tall as the character, centred on the shoulders; the flap is a gentle scale pulse only
                    float ph = wingPair.sprite.bounds.size.y;
                    float sp = ph > 0.0001f ? playerHeight * WingsHeightFactor / ph : 1f;
                    wingPair.transform.position = new Vector3(cx, cy - playerHeight * 0.04f, 0f);   // the shoulders sit a little below the body centre
                    wingPair.transform.localScale = new Vector3(sp * (1f + 0.04f * flap), sp * (1f + 0.08f * flap), 1f);
                    wingPair.transform.rotation = Quaternion.identity;
                }
                else
                {
                    float s = playerHeight * 0.95f / 2f;
                    float lift = 0.12f * playerHeight * flap;
                    wingL.transform.position = new Vector3(cx - playerHeight * 0.22f, cy + playerHeight * 0.05f + lift, 0f);
                    wingR.transform.position = new Vector3(cx + playerHeight * 0.22f, cy + playerHeight * 0.05f + lift, 0f);
                    wingL.transform.localScale = new Vector3(s, s * (1f + 0.12f * flap), 1f);
                    wingR.transform.localScale = new Vector3(s, s * (1f + 0.12f * flap), 1f);
                    wingL.transform.rotation = Quaternion.Euler(0f, 0f, 14f * flap);
                    wingR.transform.rotation = Quaternion.Euler(0f, 0f, -14f * flap);
                }
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
            SyncRebase();   // endless: the session may have re-based its heights in this tick
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
                    bounce = PlatformKinds.IsLaunch(k);   // flower, cap and dandelion launch
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
            SyncRebase();
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
            if (partSr.Length > 0)
            {
                Vector2 size = bodySize;
                float sy = h / size.y;
                float sx = (playerHeight / size.y) * widthFactor;
                float facing = s.Vx.Raw < -1000 ? -1f : (s.Vx.Raw > 1000 ? 1f : Mathf.Sign(player.localScale.x));
                player.localScale = new Vector3(sx * (facing < 0 ? -1f : 1f), sy, 1f);
            }
            player.position = new Vector3(px, py + h * 0.5f, 0f);
            player.rotation = reduced ? Quaternion.identity : Quaternion.Euler(0f, 0f, Mathf.Clamp(-s.Vx.F() * 40f, -12f, 12f));
            SetPlayerColor(s.Invuln > 0 && (reduced || (tick / 10) % 2 == 0) ? new Color(1f, 1f, 1f, 0.55f) : Color.white);
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
                if (SpecialPlatformFx.Handles(p.Kind))
                {
                    // Ladybug path (with its boost) and Bee Hive bob come from the Sim; the motion layer animates the rest (Snap Daisy squash, Seesaw tilt, Ladybug mirror).
                    if (p.Kind == PlatformKind.Ladybug) x = session.Sim.PlatformXAt(in s, p.Id).F();
                    else if (p.Kind == PlatformKind.HiveLift) y = session.Sim.PlatformY(p.Id, tick).F();
                    else if (p.Kind == PlatformKind.OrbitCap) { x = session.Sim.PlatformX(p.Id, tick).F(); y = session.Sim.PlatformY(p.Id, tick).F(); }   // region 2: the lap comes from the Sim
                    else if (p.Kind == PlatformKind.SproutLift) y = session.Sim.PlatformY(p.Id, tick, s.CrumbleTick).F();                                  // rises while stood on, sinks when empty
                    else if (p.Kind == PlatformKind.SquishCap) y = p.Y.F() - session.Sim.SquishSink(in s, p.Id).F();                                       // sinks under the player
                    else if (p.Kind == PlatformKind.TurtleShuttle) x = session.Sim.PlatformX(p.Id, tick).F();                                              // region 3: stop-and-go glide from the Sim
                    else if (p.Kind == PlatformKind.JellyWobble) x = session.Sim.PlatformX(p.Id, tick, s.CrumbleTick).F();                                 // region 4: the wobble after a landing comes from the Sim
                    else if (p.Kind == PlatformKind.SinkingRaft) y = session.Sim.PlatformY(p.Id, tick, s.CrumbleTick).F();                                 // sinks while stood on, springs back when empty
                    else if (p.Kind == PlatformKind.LanternLift) { x = session.Sim.PlatformX(p.Id, tick).F(); y = session.Sim.PlatformY(p.Id, tick, s.CrumbleTick).F(); }   // region 5: the lantern sways and rises while stood on (Sim)
                    else if (p.Kind == PlatformKind.SwingPlank) { x = session.Sim.PlatformX(p.Id, tick).F(); y = session.Sim.PlatformY(p.Id, tick).F(); }                       // region 5: the pendulum swing from the Sim
                }
                var sr = platformSr[i];
                if (platformPose[i] != 0)
                {
                    // Painted poses chosen by the Sim state: Spore Pop calm / swollen / flat, Clam Shell Pad open / shut, Pufferfish Pad calm / swelling / puffed
                    int pose = PoseOf(p, tick, in s);
                    if (pose != platformPose[i]) SwapPosePiece(i, p, sr, pose);
                }
                if (p.Kind == PlatformKind.TaffyStretch && platformPose[i] == 0)
                {
                    // No painted pieces yet: the tinted normal platform breathes with the Sim width (a placeholder animation; the painted poses replace it)
                    Vector3 ts = platformTf[i].localScale;
                    float baseW = Mathf.Max(0.1f, p.Width.F());
                    platformTf[i].localScale = new Vector3(platformBaseScaleX[i] * session.Sim.WidthAt(p.Id, tick).F() / baseW, ts.y, ts.z);
                }
                platformTf[i].position = new Vector3(x + platformOffX[i], y + platformOffY[i], 0f);
                if (SpecialPlatformFx.Handles(p.Kind)) specialFx.Update(i, p, session.Sim, in s, tick, x, y, sr);
                if (p.Kind == PlatformKind.Vanishing || p.Kind == PlatformKind.Crumble)
                {
                    bool solid = session.Sim.IsSolid(p.Id, tick, s.CrumbleTick);
                    Color c = PlatformTint(p.Kind);
                    // A crumbled platform is gone for good (no ghost to walk onto); a vanishing one comes back, so it stays a faint ghost.
                    c.a = solid ? 1f : (p.Kind == PlatformKind.Crumble ? 0f : 0.2f);
                    if (p.Kind == PlatformKind.Crumble && s.CrumbleTick[i] >= 0 && solid) c.a = reduced ? 0.7f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(tick * 0.3f));
                    sr.color = c;
                }
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
                RunLayout.StripsWorld(aspect, camHalfW * 2f, out topWorld, out bottomWorld);   // only when the shape changes: no per-frame work
            }
        }

        /// <summary>
        /// The bottom edge of the screen IS the danger line of the simulation (owner rules, 2026-10-19): the screen scrolls up after the first jump,
        /// platforms slide down, and anything below the edge is gone. The line is interpolated between ticks for smooth motion at any frame rate.
        /// </summary>
        void UpdateCamera(float killY)
        {
            FitCamera();
            camY = killY + camHalfH - bottomWorld;   // bottomWorld is 0 (owner 2026-11-02): the kill line is the physical screen bottom, the pads float over the field
            ApplyCamera();
        }

        void ApplyCamera()
        {
            cam.transform.position = new Vector3(camHalfW, camY, -10f);
        }

        void UpdateBackground()
        {
            double absCam = (double)camY + originY;   // endless: the pattern follows the ABSOLUTE height, so a re-base of the window does not move the background
            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                double scrolled = reduced ? 0.0 : absCam * l.Factor;
                double cell = System.Math.Floor(scrolled / l.Height);
                float offset = (float)(scrolled - cell * l.Height);
                long baseIdx = (long)cell;
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
            double bottom = (double)(camY - camHalfH - wallSegHeight) + originY;   // absolute height: the wall tiles keep their places across an endless re-base
            long first = (long)System.Math.Floor(bottom / wallSegHeight);
            for (int i = 0; i < wallL.Length; i++)
            {
                float y = (float)((first + i) * (double)wallSegHeight + wallSegHeight * 0.5 - originY);
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
