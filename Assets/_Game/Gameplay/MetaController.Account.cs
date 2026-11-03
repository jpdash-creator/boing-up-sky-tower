using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Account entry points of the Meta scene (owner issue 35.3, docs/35 section 8): the round account button on Home, the pop-up and the Settings account block.</summary>
    public sealed partial class MetaController
    {
        Button accountButton;
        Text accountLetter;
        GameObject accountDot;

        /// <summary>Opens the Account pop-up (Home account button). Sign in / Sync now / Sign out live there.</summary>
        public void ShowAccount()
        {
            ClosePopup();
            AccountPopup.View view = AccountPopup.Build(canvas.transform, art, ClosePopup, RefreshAccountButton);
            popup = view.Root;
        }

        /// <summary>
        /// The round account button at the left end of the Home top row (before the wallet pills). It is the back/close plate size (ToyKit.ChromeButtonScale) inside a hit
        /// area of at least 48 dp, shows the Google "G" (guest) or the player's initial (signed in) and a small green dot when signed in.
        /// </summary>
        void AddAccountButton(RectTransform row)
        {
            Button b = ToyKit.GlyphButton(row, "Account", "Account", 0, null, new Vector2(130f, 128f), ShowAccount);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 130f;
            le.preferredWidth = 130f;
            le.flexibleWidth = 0f;
            Transform plate = b.transform.Find("Plate");
            Transform glyph = plate != null ? plate.Find("Glyph") : null;
            if (glyph != null) { glyph.gameObject.SetActive(false); Destroy(glyph.gameObject); }   // the letter replaces the painted glyph
            Text letter = UiKit.AddText(plate != null ? plate : b.transform, "Letter", "G", 52, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            DropOutline(letter);
            letter.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch((RectTransform)letter.transform);
            Image dot = UiKit.AddImage(b.transform, "SignedInDot", Theme.RoundSprite, new Color(0.18f, 0.82f, 0.32f, 1f));
            dot.type = Image.Type.Sliced;
            dot.preserveAspect = false;
            var drt = dot.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.86f, 0.86f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(36f, 36f);
            Image ring = UiKit.AddImage(dot.transform, "Ring", Theme.RingSprite, UiKit.ButtonInk);
            ring.type = Image.Type.Sliced;
            ring.preserveAspect = false;
            UiKit.Stretch(ring.rectTransform);
            accountButton = b;
            accountLetter = letter;
            accountDot = dot.gameObject;
            RefreshAccountButton();
        }

        /// <summary>Redraws the letter, the dot and the accessibility label after a sign-in or sign-out.</summary>
        void RefreshAccountButton()
        {
            if (accountButton == null || accountLetter == null) return;
            AccountPopup.State st = AccountPopup.Current;
            UiKit.SetText(accountLetter, AccountPopup.ButtonLetter(st, AccountSync.Account.DisplayName));
            if (accountDot != null) accountDot.SetActive(st == AccountPopup.State.SignedIn);
            accountButton.GetComponent<A11yLabel>().Text = AccountPopup.ButtonA11y(st);
        }

        static float AccountTextH(int font, int lines)
        {
            return lines * Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
        }

        /// <summary>
        /// The Settings account block at the top of the card: the status line and one button (Sign in with Google Play / Sign out), or the notice when this build cannot sign in.
        /// Returns the height it uses including the gap below it; with <paramref name="card"/> null it only measures (the card height needs it first).
        /// </summary>
        float SettingsAccountBlock(RectTransform card, float y, float btnH)
        {
            AccountPopup.State st = AccountPopup.Current;
            const float gap = 16f, below = 28f;
            float lineH = AccountTextH(46, 3);
            float h = lineH + gap;
            float hintH = AccountTextH(40, 2);
            float googleH = Mathf.Max(170f, UiKit.MinTouchRef);
            if (st == AccountPopup.State.Unavailable) h += hintH;
            else h += st == AccountPopup.State.Guest ? googleH : btnH;
            if (card == null) return h + below;

            Text line = UiKit.AddText(card, "AccountLine", AccountSync.AccountLine, 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(line);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.offsetMin = new Vector2(28f, -(y + lineH));
            lrt.offsetMax = new Vector2(-28f, -y);
            float by = y + lineH + gap;
            if (st == AccountPopup.State.Unavailable)
            {
                Text hint = UiKit.AddText(card, "AccountHint", AccountPopup.UnavailableText, 40, Theme.Ink, TextAnchor.MiddleCenter);
                DropOutline(hint);
                var hrt = (RectTransform)hint.transform;
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.offsetMin = new Vector2(28f, -(by + hintH));
                hrt.offsetMax = new Vector2(-28f, -by);
                return h + below;
            }
            System.Action act = () =>
            {
                AccountPopup.RunPrimary(st, (ok, text) =>
                {
                    if (ok) ShowSettings();            // rebuild: the line and the button follow the new state
                    else ShowMessage("Account", text);
                });
            };
            Button b;
            if (st == AccountPopup.State.Guest)
            {
                b = LoginScreen.BuildGoogleButton(card, AccountPopup.SignInLabel, act);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -by), new Vector2(700f, googleH));
            }
            else
            {
                b = UiKit.AddButton(card, "AccountAction", AccountPopup.SettingsButtonLabel(st), ToyKit.Button(0), Color.white, new Vector2(560f, btnH), act, 46);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -by), new Vector2(560f, btnH));
            }
            return h + below;
        }
    }
}
