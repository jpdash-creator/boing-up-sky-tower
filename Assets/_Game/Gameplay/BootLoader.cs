using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyHopper.Gameplay
{
    /// <summary>Boot scene: loads content, art and the save, shows the loading art for a moment, then opens Meta.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        public float MinSeconds = 1.2f;
        float timer;
        Image bar;
        bool started;

        void Start()
        {
            GameSession.EnsureInit();
            AudioManager.Warm();   // loads the clips during the loading screen
            ArtCatalog art = GameSession.Art;
            Canvas canvas = UiKit.CreateCanvas("BootUi", 0);
            canvas.transform.SetParent(transform, false);

            Image bg = UiKit.AddImage(canvas.transform, "Bg", art != null ? art.LoadingArt : null, new Color(0.08f, 0.09f, 0.16f, 1f));
            bg.preserveAspect = false;
            UiKit.Stretch(bg.rectTransform);

            Text title = UiKit.AddText(canvas.transform, "Title", "SKY HOPPER", 120, Color.white);
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 160f));

            Image track = UiKit.AddImage(canvas.transform, "BarTrack", null, new Color(0.1f, 0.2f, 0.35f, 0.9f));
            UiKit.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(520f, 70f));
            bar = UiKit.AddImage(track.transform, "Bar", null, new Color(1f, 0.85f, 0.3f, 1f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 0f);
        }

        void Update()
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / MinSeconds);
            if (bar != null) bar.rectTransform.sizeDelta = new Vector2(520f * t, 0f);
            if (t >= 1f && !started)
            {
                started = true;
                SceneManager.LoadScene("Meta");
            }
        }
    }
}
