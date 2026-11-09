using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Settings screen (Bug List 1 B2, docs/35 and docs/38): one compact popup on the sky background, sized from its content. Icon cells with a green
    /// switch (Sound, Music, Haptics, Reduced motion, Left-handed), the text size value button, then two big buttons (the Google button and
    /// Save Progress), a small row with Reset and Restore, and the footer with the Admin icon (left) and the version (right). Only Google Play is offered
    /// as an account (no mail, social or language links). Rows grow with the screen density so every target stays at least 48 dp with 8 dp between them.
    /// Doc 46 (timed gameplay): the Pace ghost switch sits in the fourth cell row at the left and the version label (7 quick taps open the admin code box) at its right; the
    /// footer row is gone, so the popup grows by one gap only. The Admin icon (development builds and after the hidden code) moved to the top left of the popup frame.
    /// Bug List 6 item 7: the screen never scrolls (no ScrollRect). The Gameplay (difficulty) setting moved to the level screen (item 6), which freed a row; nothing needed the "More" popup.
    /// </summary>
    public sealed partial class MetaController
    {
        const float SettingsW = 860f, SettingsPad = 36f, SettingsTop = 176f, SettingsMaxH = 1470f;

        Text saveLabel;
        Button saveButton;
        bool saving;
        int saveToken;

        static float SettingsTextH(int font, int lines)
        {
            return lines * Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
        }

        public void ShowSettings()
        {
            NewScreen("Settings");
            SkyBackground();
            saving = false;
            saveLabel = null;
            saveButton = null;

            bool admin = AdminAccess.Available || GameSettings.ShowDevRowsForTests;   // the Admin button (docs/38): development builds, or after the hidden code
            AccountPopup.State st = AccountPopup.Current;
            float contentW = SettingsW - 2f * SettingsPad;
            float rowH = Mathf.Max(126f, UiKit.MinTouchRef);   // every switch and button is at least 48 dp on this screen density
            float gap = Mathf.Ceil(UiKit.MinSpacingPx);

            RectTransform panel = UiKit.BuildPopupFrame(screen, "Settings", art, new Vector2(SettingsW, SettingsMaxH), ShowMenu);
            RectTransform c = PopupBodyBegin(panel, "SettingsViewport", SettingsTop, SettingsPad, false);   // no ScrollRect: everything fits (Bug List 6 item 7)

            // ---- the light inner panel: 4 rows of icon cells (the last one: Pace ghost switch and the version label) ----
            const float innerPad = 20f, colGap = 24f;
            float innerH = 2f * innerPad + 4f * rowH + 3f * gap;
            Image inner = SoftPanel(c, "Inner", new Color(0.88f, 0.92f, 1f, 1f), 36f);
            UiKit.Place(inner.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(contentW, innerH));
            float cellW = (contentW - 2f * innerPad - colGap) * 0.5f;
            float x0 = innerPad, x1 = innerPad + cellW + colGap, pitch = rowH + gap;
            float r0 = innerPad, r1 = r0 + pitch, r2 = r1 + pitch, r3 = r2 + pitch;
            SettingsSwitchAt(c, "Sound", "icon_sound", x0, r0, cellW, rowH, () => GameSettings.Sound, v => GameSettings.Sound = v);
            SettingsSwitchAt(c, "Music", "icon_music", x1, r0, cellW, rowH, () => GameSettings.Music, v => GameSettings.Music = v);
            SettingsSwitchAt(c, "Haptics", "icon_vibrate", x0, r1, cellW, rowH, () => GameSettings.Haptics, v => GameSettings.Haptics = v);
            SettingsSwitchAt(c, "Reduced motion", "icon_motion", x1, r1, cellW, rowH, () => GameSettings.ReducedMotion, v => GameSettings.ReducedMotion = v);
            SettingsSwitchAt(c, "Left-handed", "icon_hand", x0, r2, cellW, rowH, () => GameSettings.LeftHanded, v => GameSettings.LeftHanded = v);
            SettingsValueAt(c, "Text size", "icon_size", x1, r2, cellW, rowH, 220f, GameSettings.TextPercent + "%", () =>
            {
                GameSettings.NextTextPercent();
                ShowSettings();   // rebuild so the new size applies to every label
            });

            SettingsSwitchAt(c, "Pace ghost", "icon_speed", x0, r3, cellW, rowH, () => GameSettings.PaceGhost, v => GameSettings.PaceGhost = v);
            AddSettingsFooter(panel, c, x1, r3, cellW, rowH, admin);   // version label (right cell of the last row) and the Admin icon (frame, top left)

            float y = innerH + 28f;

            // ---- big button 1: the Google account (Sign in with Google / Unlink Google Account) ----
            if (st == AccountPopup.State.Unavailable)
            {
                float hintH = SettingsTextH(40, 2);
                Text hint = UiKit.AddText(c, "AccountHint", AccountPopup.UnavailableText, 40, Theme.Ink, TextAnchor.MiddleCenter);
                DropOutline(hint);
                UiKit.Place((RectTransform)hint.transform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(contentW, hintH));
                y += hintH + gap;
            }
            else
            {
                float googleH = Mathf.Max(150f, UiKit.MinTouchRef);
                Button g = LoginScreen.BuildGoogleButton(c, AccountPopup.SettingsButtonLabel(st), () =>
                {
                    AccountPopup.RunPrimary(st, (ok, text) =>
                    {
                        if (ok) ShowSettings();            // rebuild: the button follows the new state
                        else ShowMessage("Account", text);
                    });
                });
                UiKit.Place((RectTransform)g.transform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(contentW, googleH));
                y += googleH + gap;
            }

            // ---- big button 2: Save Progress (Google Saved Games when signed in, this phone when guest) ----
            float saveH = Mathf.Max(132f, UiKit.MinTouchRef);
            saveButton = UiKit.AddButton(c, "SaveProgress", AccountPopup.SaveLabel, ToyKit.Button(0), Color.white, new Vector2(contentW, saveH), OnSaveProgress, 46);
            saveLabel = saveButton.transform.Find("Label").GetComponent<Text>();
            UiKit.Place((RectTransform)saveButton.transform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(contentW, saveH));
            y += saveH + gap;

            // ---- one small row: Reset (restart icon, confirm step) and Restore purchases (shop bag) ----
            float halfW = (contentW - gap) * 0.5f;
            Button reset = SettingsIconButton(c, "Reset", "Reset", "icon_restart", 3, "Reset progress", x: 0f, y: y, w: halfW, h: rowH, onClick: ConfirmReset);
            Button restore = SettingsIconButton(c, "RestorePurchases", "Restore", "icon_shop", 0, "Restore purchases", x: halfW + gap, y: y, w: halfW, h: rowH, onClick: RestorePurchases);
            y += rowH + gap;

            y -= gap;   // no gap after the last row

            PopupBodyEnd(panel, c, y, SettingsTop, SettingsPad, SettingsMaxH);
        }

        // ---------------- shared popup body (Settings and Admin) ----------------

        /// <summary>
        /// Adds the clipped viewport under the title area and returns its content rect (anchored top, as wide as the viewport). Call
        /// <see cref="PopupBodyEnd"/> after the content is placed: it sets the real heights and turns scrolling on only when the content is taller than the cap.
        /// </summary>
        RectTransform PopupBodyBegin(RectTransform panel, string viewportName, float top, float pad, bool scrollable = true)
        {
            RectTransform vp = UiKit.Rect(panel, viewportName);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(pad, pad);
            vp.offsetMax = new Vector2(-pad, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);   // drag target between the rows
            RectTransform content = UiKit.Rect(vp, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            if (!scrollable) return content;   // Settings: a fixed popup, never a ScrollRect
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 60f;
            return content;
        }

        /// <summary>Sets the panel height from the content (content-measured, never above maxH) and enables vertical scrolling only when the content does not fit.</summary>
        static void PopupBodyEnd(RectTransform panel, RectTransform content, float contentH, float top, float pad, float maxH)
        {
            float panelH = Mathf.Min(top + contentH + pad, maxH);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panelH);
            content.sizeDelta = new Vector2(0f, contentH);
            bool fits = contentH <= panelH - top - pad + 0.5f;
            var sr = content.GetComponentInParent<ScrollRect>();
            if (sr != null && !fits)
            {
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Elastic;
            }
        }

        // ---------------- cells ----------------

        static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        /// <summary>A rounded filled panel drawn from the shared round sprite (a surface sprite: the layout audit treats it as a backing, not as content).</summary>
        static Image SoftPanel(Transform parent, string name, Color color, float radius)
        {
            Image img = UiKit.AddImage(parent, name, Theme.RoundSprite, color);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / radius);
            return img;
        }

        void SettingsIconAt(RectTransform c, string name, string icon, float x, float y, float rowH)
        {
            Image ic = UiKit.AddImage(c, name + "Icon", ToyKit.Get(icon), Color.white);
            ic.preserveAspect = true;
            UiKit.Place(ic.rectTransform, TopLeft, new Vector2(x, -(y + (rowH - 88f) * 0.5f)), new Vector2(88f, 88f));
        }

        /// <summary>An on/off cell: the icon at the left and the kit switch at the right (green with the knob right when on, blue with the knob left when off). The label is for screen readers only.</summary>
        void SettingsSwitchAt(RectTransform c, string label, string icon, float x, float y, float cellW, float rowH, Func<bool> get, Action<bool> set)
        {
            const float sw = 200f, inset = 14f;
            SettingsIconAt(c, label, icon, x + inset, y, rowH);
            Button b = UiKit.AddButton(c, label + "Toggle", "", ToyKit.Button(get() ? 1 : 2), Color.white, new Vector2(sw, rowH), null);
            UiKit.Place((RectTransform)b.transform, TopLeft, new Vector2(x + cellW - inset - sw, -y), new Vector2(sw, rowH));
            Image knob = UiKit.AddImage(b.transform, "Knob", Theme.RoundSprite, Color.white);
            knob.type = Image.Type.Sliced;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(get() ? 0.74f : 0.26f, 0.5f);
            knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
            knob.rectTransform.sizeDelta = new Vector2(86f, 86f);
            Image ring = UiKit.AddImage(knob.transform, "Ring", Theme.RingSprite, UiKit.ButtonInk);
            ring.type = Image.Type.Sliced;
            ring.preserveAspect = false;
            UiKit.Stretch(ring.rectTransform);
            b.GetComponent<A11yLabel>().Text = label + (get() ? ", on" : ", off");
            b.onClick.AddListener(() =>
            {
                set(!get());
                bool on = get();
                b.GetComponent<Image>().sprite = ToyKit.Button(on ? 1 : 2);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 0.74f : 0.26f, 0.5f);
                b.GetComponent<A11yLabel>().Text = label + (on ? ", on" : ", off");
            });
        }

        /// <summary>A value cell: the icon at the left and a yellow value button at the right (text size).</summary>
        void SettingsValueAt(RectTransform c, string label, string icon, float x, float y, float cellW, float rowH, float valueW, string value, Action onClick)
        {
            const float inset = 14f;
            SettingsIconAt(c, label, icon, x + inset, y, rowH);
            Button b = UiKit.AddButton(c, label.Replace(" ", "") + "Button", value, ToyKit.Button(0), Color.white, new Vector2(valueW, rowH), onClick, 44);
            UiKit.Place((RectTransform)b.transform, TopLeft, new Vector2(x + cellW - inset - valueW, -y), new Vector2(valueW, rowH));
            b.GetComponent<A11yLabel>().Text = label + ", " + value;
        }

        /// <summary>A kit button with a painted icon on the left and a short label (Reset, Restore): the icon explains it, the word removes any doubt.</summary>
        Button SettingsIconButton(RectTransform c, string name, string label, string icon, int role, string a11y, float x, float y, float w, float h, Action onClick)
        {
            Button b = UiKit.AddButton(c, name, "", ToyKit.Button(role), Color.white, new Vector2(w, h), onClick);
            UiKit.Place((RectTransform)b.transform, TopLeft, new Vector2(x, -y), new Vector2(w, h));
            b.GetComponent<A11yLabel>().Text = a11y;
            float icon0 = Mathf.Min(h * 0.62f, 84f);
            Image ic = UiKit.AddImage(b.transform, "Icon", ToyKit.Get(icon), Color.white);
            ic.preserveAspect = true;
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(20f, 0f);
            irt.sizeDelta = new Vector2(icon0, icon0);
            Text t = UiKit.AddText(b.transform, "Label", label, 40, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            DropOutline(t);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f + icon0 + 6f, 0f);
            trt.offsetMax = new Vector2(-12f, 0f);
            return b;
        }

        // ---------------- Save Progress ----------------

        /// <summary>Save Progress: a guest saves on this phone, a Google player also pushes to Google Saved Games. The result is shown on the button for a moment.</summary>
        void OnSaveProgress()
        {
            if (saving) return;
            saving = true;
            AccountPopup.SaveProgress(progress, (ok, text) =>
            {
                saving = false;
                ShowSaveResult(text);
            });
        }

        void ShowSaveResult(string text)
        {
            if (saveLabel == null || saveButton == null) return;   // the screen was rebuilt meanwhile
            UiKit.SetText(saveLabel, text);
            saveButton.GetComponent<A11yLabel>().Text = text;
            int token = ++saveToken;
            if (isActiveAndEnabled) StartCoroutine(RevertSaveLabel(token));
        }

        IEnumerator RevertSaveLabel(int token)
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (token != saveToken || saveLabel == null || saveButton == null) yield break;
            UiKit.SetText(saveLabel, AccountPopup.SaveLabel);
            saveButton.GetComponent<A11yLabel>().Text = AccountPopup.SaveLabel;
        }

        /// <summary>Tests: the text the Save Progress button shows now (the result toast or the normal label).</summary>
        public string SaveButtonTextForTests { get { return saveLabel != null ? saveLabel.text : null; } }
    }
}
