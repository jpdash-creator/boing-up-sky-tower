using System;
using System.Collections.Generic;
using FluffyClimbers.Leaderboard;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Player pictures of the leaderboard: downloaded once per URL through the hub's transport (UnityWebRequestTexture), kept in memory as sprites and reused by every row and every screen visit.
    /// A failed download is remembered for a minute so a row never retries in a loop; the row shows a neutral round letter badge instead. No per-frame work: rows ask when they are bound.
    /// </summary>
    public static class AvatarCache
    {
        /// <summary>More cached pictures than this are dropped (all at once, by <see cref="Trim"/>) when a leaderboard screen opens.</summary>
        public const int MaxEntries = 160;
        const float RetryAfterSeconds = 60f;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, float> failedUntil = new Dictionary<string, float>();
        static readonly Dictionary<string, List<Action<Sprite>>> waiting = new Dictionary<string, List<Action<Sprite>>>();

        /// <summary>Tests: replaces the download (url, done). Null = the hub's transport.</summary>
        public static Action<string, Action<Texture2D>> Loader;

        public static int Count { get { return sprites.Count; } }

        /// <summary>The sprite of a URL when it is already cached, else null.</summary>
        public static Sprite Peek(string url)
        {
            Sprite s;
            return !string.IsNullOrEmpty(url) && sprites.TryGetValue(url, out s) && s != null ? s : null;
        }

        /// <summary>Gives the cached sprite at once, or downloads it and calls back later (main thread). The callback never runs for a failed or empty URL.</summary>
        public static void Request(string url, Action<Sprite> done)
        {
            if (string.IsNullOrEmpty(url) || done == null) return;
            Sprite have = Peek(url);
            if (have != null) { done(have); return; }
            float until;
            if (failedUntil.TryGetValue(url, out until) && Time.unscaledTime < until) return;
            List<Action<Sprite>> list;
            if (waiting.TryGetValue(url, out list)) { list.Add(done); return; }
            list = new List<Action<Sprite>> { done };
            waiting[url] = list;
            Action<Texture2D> got = tex => OnLoaded(url, tex);
            if (Loader != null) Loader(url, got); else LeaderboardHub.GetTexture(url, got);
        }

        static void OnLoaded(string url, Texture2D tex)
        {
            List<Action<Sprite>> list;
            waiting.TryGetValue(url, out list);
            waiting.Remove(url);
            if (tex == null || tex.width < 2 || tex.height < 2)
            {
                Kill(tex);
                failedUntil[url] = Time.unscaledTime + RetryAfterSeconds;
                return;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            // a centred square crop: the circle mask shows a round picture and nothing is ever stretched
            int side = Mathf.Min(tex.width, tex.height);
            var rect = new Rect((tex.width - side) * 0.5f, (tex.height - side) * 0.5f, side, side);
            Sprite sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            sp.name = "avatar";
            sprites[url] = sp;
            if (list != null) for (int i = 0; i < list.Count; i++) list[i](sp);
        }

        /// <summary>Drops every cached picture when the cache grew past <see cref="MaxEntries"/> (call before a screen binds its rows).</summary>
        public static void Trim()
        {
            if (sprites.Count > MaxEntries) Clear();
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public static void Clear()
        {
            foreach (KeyValuePair<string, Sprite> kv in sprites)
            {
                if (kv.Value == null) continue;
                Texture2D t = kv.Value.texture;
                Kill(kv.Value);
                Kill(t);
            }
            sprites.Clear();
            failedUntil.Clear();
        }
    }
}
