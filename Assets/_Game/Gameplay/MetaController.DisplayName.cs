using FluffyClimbers.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Settings: the player's own leaderboard display name (owner 2026-11-13, Main Bug List 1 item 2e). The row only exists when a Google account is linked and the weekly leaderboard is on
    /// (or the Admin test switch). The popup edits the name; the rules are <see cref="LeaderboardNameRules"/>; an empty name goes back to the Google Play Games name.
    /// </summary>
    public sealed partial class MetaController
    {
        InputField nameField;
        Text nameError;

        /// <summary>The Settings row is shown: a Google account is linked and the leaderboard is switched on in the config or by the Admin test switch.</summary>
        bool ShowDisplayNameRow(AccountPopup.State st)
        {
            return st == AccountPopup.State.SignedIn && (LeaderboardHub.Enabled || GameSettings.LeaderboardTest);
        }

        /// <summary>The row: one full-width button (icon, "Name: ..."); tapping it opens <see cref="ShowDisplayNameBox"/>.</summary>
        Button SettingsDisplayNameRow(RectTransform c, float y, float w, float h)
        {
            string shown = LeaderboardHub.ShownName;
            string label = "Name: " + (string.IsNullOrEmpty(shown) ? "Player" : shown);
            Button b = SettingsIconButton(c, "DisplayName", label, "icon_heroes", 0, "Display name, " + label + ". Edit", x: 0f, y: y, w: w, h: h, onClick: ShowDisplayNameBox);
            return b;
        }

        /// <summary>The small edit popup: a text field, the rule hint, an inline error and OK / Cancel (icon buttons, at least 48 dp).</summary>
        public void ShowDisplayNameBox()
        {
            OpenPopup("Display name", "", 1000f);
            Transform panel = popup.Find("Panel");
            Transform emptyBody = panel.Find("Body");
            if (emptyBody != null) Destroy(emptyBody.gameObject);

            float fieldH = Mathf.Max(132f, UiKit.MinTouchRef);
            RectTransform fieldRt = UiKit.Rect(panel, "NameField");
            UiKit.Place(fieldRt, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(700f, fieldH));
            var bg = fieldRt.gameObject.AddComponent<Image>();
            bg.color = Theme.SurfaceDim;
            Text input = UiKit.AddText(fieldRt, "Text", "", 52, Theme.Ink, TextAnchor.MiddleCenter);
            DropOutline(input);
            input.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch((RectTransform)input.transform);
            Text hint = UiKit.AddText(fieldRt, "Placeholder", "your name", 44, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(hint);
            UiKit.Stretch((RectTransform)hint.transform);
            nameField = fieldRt.gameObject.AddComponent<InputField>();
            nameField.textComponent = input;
            nameField.placeholder = hint;
            nameField.characterLimit = LeaderboardNameRules.MaxLength + 8;   // a little over, so the "too long" message can be shown
            nameField.contentType = InputField.ContentType.Standard;
            nameField.text = progress != null ? progress.LeaderboardDisplayName : "";
            fieldRt.gameObject.AddComponent<A11yLabel>().Text = "Display name";

            float textH = Mathf.Ceil(Mathf.Max(40, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
            Text rule = UiKit.AddText(panel, "Rule", "3 to 16 letters, numbers, spaces or _.\nEmpty = your Google Play Games name.", 40, Color.white);
            UiKit.OnPanel(rule);
            rule.alignment = TextAnchor.MiddleCenter;
            UiKit.Place((RectTransform)rule.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(230f + fieldH + 24f)), new Vector2(760f, 2f * textH));

            nameError = UiKit.AddText(panel, "Error", "", 42, Color.white);
            UiKit.OnPanel(nameError);
            nameError.color = Theme.AccentDeep;
            nameError.alignment = TextAnchor.MiddleCenter;
            UiKit.Place((RectTransform)nameError.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(230f + fieldH + 24f + 2f * textH + 12f)), new Vector2(760f, textH));

            nameField.onValueChanged.AddListener(OnDisplayNameEdited);
            ToyKit.PopupAction(panel, "OK", Btn(1), Color.white, 60f, () => SubmitDisplayName(), x: 110f);
            ToyKit.PopupAction(panel, "Cancel", Btn(0), Color.white, 60f, ClosePopup, x: -110f);
        }

        void OnDisplayNameEdited(string text)
        {
            if (nameError == null) return;
            string n = LeaderboardNameRules.Normalize(text);
            LeaderboardNameCheck v = n.Length == 0 ? LeaderboardNameCheck.Ok : LeaderboardNameRules.Validate(n);
            if (v == LeaderboardNameCheck.TooShort) v = LeaderboardNameCheck.Ok;   // "too short" is only said on OK, not while typing
            nameError.text = LeaderboardNameRules.Message(v);
        }

        /// <summary>OK: stores the name (empty clears it). Returns true when it was accepted (public for tests). A refused name stays in the box with its message.</summary>
        public bool SubmitDisplayName()
        {
            string typed = nameField != null ? nameField.text : "";
            LeaderboardNameCheck r = LeaderboardHub.SetDisplayName(typed);
            if (r != LeaderboardNameCheck.Ok)
            {
                if (nameError != null) nameError.text = LeaderboardNameRules.Message(r);
                return false;
            }
            ClosePopup();
            ShowSettings();   // rebuild so the row shows the new name
            return true;
        }
    }
}
