using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoingTower.Gameplay
{
    /// <summary>
    /// Development-build only hook for the Android test harness (Tools/android_test). It writes game state (BTSTATE) and the visible UI tree
    /// with screen rectangles (BTUI) to logcat, so an external script can find buttons by name and tap them through adb, like a browser driver.
    /// It does nothing in release builds.
    /// </summary>
    public sealed class TestBridge : MonoBehaviour
    {
        const string Tag = "BT";
        float nextState, nextUi;
        string lastUiKey = "";
        readonly StringBuilder sb = new StringBuilder(4096);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("TestBridge");
            DontDestroyOnLoad(go);
            go.AddComponent<TestBridge>();
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (t >= nextState) { nextState = t + 0.5f; LogState(); }
            if (t >= nextUi) { nextUi = t + 1f; LogUi(); }
        }

        void LogState()
        {
            sb.Length = 0;
            sb.Append("BTSTATE {\"version\":\"").Append(Application.version).Append("\",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append("\",\"fps\":").Append((1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("F0"));
            var meta = FindFirstObjectByType<MetaController>();
            if (meta != null) sb.Append(",\"screen\":\"").Append(meta.CurrentScreen).Append('"');
            var rc = FindFirstObjectByType<RunController>();
            if (rc != null && rc.Session != null)
            {
                var s = rc.Session.State;
                sb.Append(",\"level\":\"").Append(rc.Session.Level != null ? rc.Session.Level.Id : "endless").Append("\",\"tick\":").Append(s.Tick).Append(",\"row\":").Append(s.Row)
                  .Append(",\"coins\":").Append(s.Coins).Append(",\"dead\":").Append(s.Dead ? "true" : "false")
                  .Append(",\"over\":").Append(rc.Session.Over ? "true" : "false").Append(",\"paused\":").Append(rc.Paused ? "true" : "false")
                  .Append(",\"overlay\":").Append(rc.Hud != null && rc.Hud.OverlayOpen ? "true" : "false")
                  .Append(",\"intro\":").Append(rc.Hud != null && rc.Hud.IntroRemaining > 0f ? "true" : "false")
                  .Append(",\"x\":").Append(s.X.F().ToString("F2")).Append(",\"y\":").Append(s.Y.F().ToString("F2"))
                  .Append(",\"camY\":").Append(rc.View.CameraY.ToString("F2"))
                  .Append(",\"avgMs\":").Append(rc.Stats.AverageMs.ToString("F3")).Append(",\"maxMs\":").Append(rc.Stats.MaxMs.ToString("F2"));
            }
            sb.Append('}');
            Debug.Log(sb.ToString());
        }

        void LogUi()
        {
            var parts = new System.Collections.Generic.List<string>();
            var items = new StringBuilder(2048);
            int count = 0, total = 0;
            // A modal popup / overlay hides everything behind it: only its elements are reported (they are the only ones a player can touch).
            Transform modal = null;
            foreach (Canvas c0 in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c0.isActiveAndEnabled) continue;
                foreach (Transform ch in c0.transform)
                    if (ch.gameObject.activeInHierarchy && (ch.name == "Popup" || ch.name == "Overlay")) modal = ch;
            }
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isActiveAndEnabled) continue;
                foreach (Button b in c.GetComponentsInChildren<Button>(false))
                {
                    if (modal != null && !b.transform.IsChildOf(modal)) continue;
                    var rt = (RectTransform)b.transform;
                    var lab = b.GetComponent<A11yLabel>();
                    Append(items, "button", rt, lab != null ? lab.Text : b.name, b.interactable, ref count);
                    Flush(items, parts, ref count);
                }
                foreach (Text tx in c.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(tx.text)) continue;
                    if (modal != null && !tx.transform.IsChildOf(modal)) continue;
                    Append(items, "text", (RectTransform)tx.transform, tx.text, true, ref count);
                    Flush(items, parts, ref count);
                }
            }
            if (items.Length > 0) { parts.Add(items.ToString()); items.Length = 0; }
            foreach (string part in parts) total += part.Length;
            string key = parts.Count + ":" + SceneManager.GetActiveScene().name + ":" + total;
            var rc = FindFirstObjectByType<RunController>();
            bool inRun = rc != null && rc.Session != null && !rc.Paused && !rc.Session.Over && rc.Hud != null && !rc.Hud.OverlayOpen && rc.Hud.IntroRemaining <= 0f;
            if (inRun && key == lastUiKey) return;    // keep the run loop quiet: only log when the UI changed
            lastUiKey = key;
            seq++;
            if (parts.Count == 0) parts.Add("");
            // logcat truncates long lines, so the tree is sent in parts of at most about 700 bytes (this device cuts long log lines at about 1000) and re-assembled by the harness.
            for (int i = 0; i < parts.Count; i++)
                Debug.Log("BTUI {\"seq\":" + seq + ",\"part\":" + i + ",\"parts\":" + parts.Count + ",\"w\":" + Screen.width + ",\"h\":" + Screen.height + ",\"items\":[" + parts[i] + "]}");
        }

        int seq;

        static void Flush(StringBuilder items, System.Collections.Generic.List<string> parts, ref int count)
        {
            if (items.Length < 600) return;
            parts.Add(items.ToString());
            items.Length = 0;
            count = 0;
        }

        static void Append(StringBuilder items, string kind, RectTransform rt, string label, bool enabled, ref int count)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // overlay canvas: world corners are screen pixels (origin bottom-left)
            float x = c[0].x, y = Screen.height - c[2].y, w = c[2].x - c[0].x, h = c[2].y - c[0].y;
            if (w < 1f || h < 1f) return;
            // Content of a scroll view (the world map) is reported only while it is fully inside the viewport: the rest is clipped away.
            ScrollRect sr = rt.GetComponentInParent<ScrollRect>();
            if (sr != null && sr.viewport != null)
            {
                var v = new Vector3[4];
                sr.viewport.GetWorldCorners(v);
                if (c[0].x < v[0].x - 1f || c[0].y < v[0].y - 1f || c[2].x > v[2].x + 1f || c[2].y > v[2].y + 1f) return;
            }
            if (count++ > 0) items.Append(',');
            items.Append("{\"k\":\"").Append(kind).Append("\",\"t\":\"").Append(Escape(label)).Append("\",\"n\":\"").Append(Escape(rt.name)).Append("\",\"on\":").Append(enabled ? "true" : "false")
                 .Append(",\"x\":").Append(Mathf.RoundToInt(x)).Append(",\"y\":").Append(Mathf.RoundToInt(y)).Append(",\"w\":").Append(Mathf.RoundToInt(w)).Append(",\"h\":").Append(Mathf.RoundToInt(h)).Append('}');
        }

        static string Escape(string s) { return s.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " "); }
    }
}
