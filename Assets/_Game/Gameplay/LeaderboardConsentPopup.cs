using System;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The one-time consent popup of the weekly leaderboards (Planning/LEADERBOARD-DESIGN.md section 5.7). The exact words are fixed by the owner decision and by the privacy policy (POLICY.md section 4a):
    /// they say what is shown and what is sent. "Join leaderboards" stores the consent flag (<c>leaderboardConsent {v, at}</c>); "Not now" stores nothing and the popup comes back at most once a day.
    /// Layout: title, body, a Privacy Policy button, then the two actions stacked; the body height is measured, so it survives the 130 percent text size on every aspect.
    /// </summary>
    public static class LeaderboardConsentPopup
    {
        public const string RootName = "Popup";
        public const string Title = "Join the weekly leaderboards?";
        public const string Body = "Your leaderboard name (the name you chose in Settings, or your Google Play Games name) and profile picture will be shown to other players on the leaderboards. Your runs are sent to our server so it can check your scores. You can stop any time by playing as a guest.";
        public const string JoinLabel = "Join leaderboards", NotNowLabel = "Not now", PolicyLabel = "Privacy Policy";
        public const string JoinName = "JoinLeaderboards", NotNowName = "NotNow", PolicyName = "PrivacyPolicy";

        /// <summary>Builds the popup on its own dim layer under <paramref name="canvas"/>. The caller keeps the returned root (and destroys it when an action ran).</summary>
        public static RectTransform Build(Transform canvas, ArtCatalog art, Action onJoin, Action onNotNow, Action onPolicy)
        {
            RectTransform root = UiKit.Rect(canvas, RootName);
            UiKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);

            const float W = 860f, pad = 40f;
            float btnH = Mathf.Max(130f, UiKit.MinTouchRef);
            float linkH = Mathf.Max(UiKit.MinTouchRef, 110f);
            float innerW = W - 2f * pad;

            // a throw-away measuring text gives the body height at the current text size (the real one is created in the panel below)
            RectTransform panel = UiKit.BuildPopupFrame(root, Title, art, new Vector2(W, 800f), null);
            Text body = UiKit.AddText(panel, "Body", Body, 44, Color.white, TextAnchor.UpperCenter);
            UiKit.OnPanel(body);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            float top = LeaderboardScreen.FitPopupTitle(panel);
            UiKit.Place((RectTransform)body.transform, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(innerW, 100f));
            float bodyH = Mathf.Ceil(body.preferredHeight) + RewardsScreen.LineH(44);
            ((RectTransform)body.transform).sizeDelta = new Vector2(innerW, bodyH);

            float y = top + bodyH + 24f;
            Button link = UiKit.AddButton(panel, PolicyName, PolicyLabel, ToyKit.Button(2), Color.white, new Vector2(420f, linkH), onPolicy, 44);
            UiKit.Place((RectTransform)link.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(420f, linkH));
            y += linkH + 24f;
            Button join = UiKit.AddButton(panel, JoinName, JoinLabel, ToyKit.Button(1), Color.white, new Vector2(700f, btnH), onJoin, 46);
            UiKit.Place((RectTransform)join.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(700f, btnH));
            y += btnH + 16f;
            Button later = UiKit.AddButton(panel, NotNowName, NotNowLabel, ToyKit.Button(0), Color.white, new Vector2(700f, btnH), onNotNow, 46);
            UiKit.Place((RectTransform)later.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(700f, btnH));
            y += btnH + pad;

            panel.sizeDelta = new Vector2(W, y);
            return root;
        }
    }
}
