using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// A hidden, persistent helper object: runs the web coroutines and calls the hub's slow tick (every 2 s: one float compare per frame, no allocation) so the offline queue and the weekly
    /// countdown never need a per-frame loop of their own. Created on first use.
    /// </summary>
    public sealed class LeaderboardRunner : MonoBehaviour
    {
        static LeaderboardRunner instance;
        float nextTick;

        /// <summary>Called about every 2 seconds of unscaled time while the app runs.</summary>
        public Action OnTick;

        public static LeaderboardRunner Get()
        {
            if (instance == null)
            {
                var go = new GameObject("LeaderboardRunner");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                instance = go.AddComponent<LeaderboardRunner>();
            }
            return instance;
        }

        void Update()
        {
            if (OnTick == null) return;
            float t = Time.unscaledTime;
            if (t < nextTick) return;
            nextTick = t + 2f;
            OnTick();
        }
    }

    /// <summary>UnityWebRequest transport: JSON bodies, bearer header, 15 s timeout, no redirects to other schemes. Used only in play mode.</summary>
    public sealed class UnityWebTransport : ILeaderboardTransport
    {
        const int TimeoutSeconds = 15;

        public void Send(string method, string url, string bearer, string jsonBody, Action<HttpReply> done)
        {
            LeaderboardRunner.Get().StartCoroutine(Run(method, url, bearer, jsonBody, done));
        }

        static IEnumerator Run(string method, string url, string bearer, string jsonBody, Action<HttpReply> done)
        {
            HttpReply reply = new HttpReply { NetworkError = true };
            UnityWebRequest req = null;
            bool failed = false;
            try
            {
                req = new UnityWebRequest(url, method);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (jsonBody != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
                req.timeout = TimeoutSeconds;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Leaderboard] request setup failed: " + e.Message);
                if (req != null) req.Dispose();
                failed = true;
            }
            if (failed) { done(reply); yield break; }
            yield return req.SendWebRequest();
            try
            {
                bool noAnswer = req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError || req.responseCode == 0;
                reply = new HttpReply { NetworkError = noAnswer, Status = (int)req.responseCode, Body = noAnswer ? "" : (req.downloadHandler != null ? req.downloadHandler.text : "") };
            }
            finally { req.Dispose(); }
            done(reply);
        }

        public void GetTexture(string url, Action<Texture2D> done)
        {
            LeaderboardRunner.Get().StartCoroutine(RunTexture(url, done));
        }

        static IEnumerator RunTexture(string url, Action<Texture2D> done)
        {
            UnityWebRequest req = null;
            bool failed = false;
            try
            {
                req = UnityWebRequestTexture.GetTexture(url, true);
                req.timeout = TimeoutSeconds;
            }
            catch (Exception)
            {
                if (req != null) req.Dispose();
                failed = true;
            }
            if (failed) { done(null); yield break; }
            yield return req.SendWebRequest();
            Texture2D tex = null;
            try
            {
                if (req.result == UnityWebRequest.Result.Success) tex = DownloadHandlerTexture.GetContent(req);
            }
            catch (Exception) { tex = null; }
            finally { req.Dispose(); }
            done(tex);
        }
    }
}
