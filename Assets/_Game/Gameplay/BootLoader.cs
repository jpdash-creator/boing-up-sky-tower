using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Boot scene: the loading screen (sky background, logo, Momo, a green progress bar tied to the real warm-up steps and a rotating tip), then
    /// opens Meta. The warm-up (kit sprites, glyphs, audio) runs here so nothing hitches during a run (CUX-1).
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        public float MinSeconds = 1.2f;
        float timer;
        RectTransform fill;
        float trackW;
        Text tipText;
        bool started;
        readonly List<Action> steps = new List<Action>();
        int stepIndex;
        float shown;
        RectTransform momoRt, logoRt;
        Vector2 momoBase, logoBase;
        int lastTip = -1;

        static readonly string[] Tips =
        {
            "Bounce off the walls to climb faster.",
            "Chain landings for a combo.",
            "Collect the 3 stars of every level.",
            "Power-ups can save a run.",
        };

        void Start()
        {
            GameSession.EnsureInit();
            ArtCatalog art = GameSession.Art;
            Canvas canvas = UiKit.CreateCanvas("BootUi", 0);
            canvas.transform.SetParent(transform, false);

            RectTransform sky = Theme.SkyBackground(canvas.transform);
            RectTransform safe = UiKit.SafeArea(canvas.transform);

            Sprite logo = ToyKit.Get("logo");
            if (logo != null)
            {
                Image lg = UiKit.AddImage(safe, "Logo", logo, Color.white);
                UiKit.Place(lg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(860f, 352f));
                lg.rectTransform.pivot = new Vector2(0.5f, 1f);
                lg.raycastTarget = false;
                logoRt = lg.rectTransform;
                logoBase = logoRt.anchoredPosition;
            }
            else
            {
                Text title = UiKit.AddText(safe, "Title", "BOING UP: SKY TOWER", 120, Color.white);
                UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 160f));
            }
            if (art != null && art.Player != null)
            {
                Image m = UiKit.AddImage(safe, "Momo", art.Player, Color.white);
                UiKit.Place(m.rectTransform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(460f, 560f));
                m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                momoRt = m.rectTransform;
                momoBase = momoRt.anchoredPosition;
            }

            Text label = UiKit.AddText(safe, "Loading", "Loading..", 64, Theme.Ink, TextAnchor.MiddleCenter, true);
            var lo = label.GetComponent<Outline>();
            if (lo != null) lo.enabled = false;
            UiKit.Place((RectTransform)label.transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(700f, 90f));

            // The bar: a dark plate with a green fill that grows with the warm-up steps.
            trackW = 780f;
            Image track = Theme.GlassPanel(safe, "BarTrack", 22f);
            UiKit.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(trackW, 88f));
            Image f = ToyKit.Sliced(track.transform, "Fill", ToyKit.Button(1), 30f);
            fill = f.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(8f, 8f);
            fill.offsetMax = new Vector2(8f, -8f);
            fill.sizeDelta = new Vector2(60f, -16f);

            tipText = UiKit.AddText(safe, "Tip", Tips[0], 42, Theme.Ink, TextAnchor.MiddleCenter);
            var to = tipText.GetComponent<Outline>();
            if (to != null) to.enabled = false;
            UiKit.Place((RectTransform)tipText.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(940f, 70f));

            steps.Add(Theme.Warmup);
            steps.Add(() => { foreach (string n in new[] { "btn_green", "btn_yellow", "btn_blue", "btn_red", "panel_cream", "plate_pill", "plate_sky" }) ToyKit.Get(n); });
            steps.Add(() => { foreach (string n in new[] { "icon_shop", "icon_gear", "icon_heroes", "icon_daily", "icon_play", "icon_home", "icon_coin", "icon_gem", "icon_star", "icon_flame", "icon_chest", "icon_bolt", "icon_crown", "icon_tower", "icon_flag", "icon_speed", "shop_scene" }) ToyKit.Get(n); });
            steps.Add(() => ToyKit.ApplyTo(GameSession.Art));
            steps.Add(AudioManager.Warm);   // loads the clips during the loading screen
        }

        void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (!GameSettings.ReducedMotion)
            {
                // Momo bounces and the logo breathes while the bar fills (reduced motion keeps them still).
                if (momoRt != null) momoRt.anchoredPosition = momoBase + new Vector2(0f, Mathf.Abs(Mathf.Sin(timer * 3.2f)) * 70f);
                if (logoRt != null) logoRt.localScale = Vector3.one * (1f + Mathf.Sin(timer * 2.4f) * 0.02f);
            }
            // One warm-up step per frame so the bar really moves.
            if (stepIndex < steps.Count)
            {
                try { steps[stepIndex].Invoke(); } catch (Exception e) { Debug.LogWarning("Boot step " + stepIndex + " failed: " + e.Message); }
                stepIndex++;
            }
            float real = steps.Count == 0 ? 1f : stepIndex / (float)steps.Count;
            float t = Mathf.Min(real, Mathf.Clamp01(timer / MinSeconds));
            shown = Mathf.MoveTowards(shown, t, Time.unscaledDeltaTime * 2f);
            if (fill != null) fill.sizeDelta = new Vector2(Mathf.Lerp(60f, trackW - 16f, shown), -16f);
            int tip = Mathf.FloorToInt(timer / 1.1f) % Tips.Length;
            if (tip != lastTip && tipText != null) { lastTip = tip; tipText.text = Tips[tip]; }
            if (shown >= 0.999f && real >= 1f && timer >= MinSeconds && !started)
            {
                started = true;
                SceneManager.LoadScene("Meta");
            }
        }
    }
}
