using System;
using FluffyClimbers.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The "Privacy choices" row of the Settings screen (Google UMP: players in the EEA, UK and Switzerland must be able to reopen the consent form). The row exists only while
    /// Services.PrivacyOptionsRequired is true, is full width (at least 48 dp tall, same height as the other rows) and opens Services.ShowPrivacyOptions. The Settings popup
    /// grows by exactly one row for it (see ShowSettings), so nothing overlaps at 9:16, 9:19.5, 9:21 or 3:4.
    /// </summary>
    public sealed partial class MetaController
    {
        public const string PrivacyChoicesLabel = "Privacy choices";
        public const string PrivacyChoicesA11y = "Privacy choices, opens your ad and data consent settings";

        Button SettingsPrivacyChoicesButton(RectTransform c, float y, float w, float h)
        {
            Button b = UiKit.AddButton(c, "PrivacyChoices", PrivacyChoicesLabel, ToyKit.Button(2), Color.white, new Vector2(w, h), OpenPrivacyChoices, 40);
            UiKit.Place((RectTransform)b.transform, TopLeft, new Vector2(0f, -y), new Vector2(w, h));
            b.GetComponent<A11yLabel>().Text = PrivacyChoicesA11y;
            return b;
        }

        void OpenPrivacyChoices()
        {
            try { Services.ShowPrivacyOptions(() => { }); }
            catch (Exception e) { Debug.LogWarning("[Settings] privacy options failed: " + e.Message); ShowMessage("Privacy choices", "The privacy choices could not be opened right now. Try again later."); }
        }
    }
}
