using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Gameplay setting on the level screen (Bug List 6 item 6): a centred row of five buttons (Relaxed, Easy, Normal, Hard, Insane), each an icon plus its
    /// label, directly below the region name. It replaces the Difficulty row of Settings. The choice is stored in the same PlayerPrefs key as before and decides
    /// the size of the scroll speed-up steps (20, 22, 25, 28, 30 percent every 5 s) and the gem chance. The selected button is NOT shown by colour alone: it is
    /// green, has a thick ink outline (no tick mark, Bug List 8 item 2a), and its accessibility label says "selected".
    /// </summary>
    public sealed partial class MetaController
    {
        /// <summary>Name of the row container on the level screen (tests look it up).</summary>
        public const string GameplayRowName = "GameplayRow";

        /// <summary>Row height in canvas units for the current text scale: icon, a gap and one line of label, never below 48 dp.</summary>
        static float GameplayRowHeight()
        {
            float labelH = GameplayLabelH();
            return Mathf.Max(UiKit.MinTouchRef, 8f + GameplayIconPx + 2f + labelH + 6f);
        }

        static float GameplayLabelPx { get { return UiKit.MinBodyTextPx; } }   // 16 sp minimum, smaller than before so the bigger icon fits (Bug List 8 item 2a)
        static float GameplayLabelH() { return Mathf.Ceil(GameplayLabelPx * UiKit.TextScale * 1.25f); }

        const float GameplayIconPx = 88f, GameplaySideMargin = 16f, GameplayBelowGap = 12f;

        /// <summary>
        /// Builds the Gameplay row under <paramref name="parent"/> with its top edge <paramref name="top"/> units below the parent's top and returns the vertical space
        /// it takes (height plus the gap below it), so the caller can start the content after it.
        /// </summary>
        float BuildGameplayRow(RectTransform parent, float top)
        {
            float rowH = GameplayRowHeight();
            float gap = Mathf.Ceil(UiKit.MinSpacingPx);
            RectTransform row = UiKit.Rect(parent, GameplayRowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -top);
            row.sizeDelta = new Vector2(-2f * GameplaySideMargin, rowH);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = gap;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            int count = GameplayIcons.Keys.Length;
            var plates = new Image[count];
            var rings = new Outline[count];
            var buttons = new Button[count];
            int selected = GameSettings.Difficulty;
            for (int i = 0; i < count; i++)
            {
                int mode = i;
                string name = GameSettings.DifficultyNames[mode];
                Button b = UiKit.AddButton(row, "Gameplay" + name, "", Btn(mode == selected ? 1 : 0), Color.white, new Vector2(UiKit.MinTouchPx, rowH), () => SelectGameplay(mode, plates, rings, buttons));
                buttons[mode] = b;
                plates[mode] = b.GetComponent<Image>();
                var ring = b.gameObject.AddComponent<Outline>();   // thick ink outline for the selected one (not colour-only)
                ring.effectColor = UiKit.ButtonInk;
                ring.effectDistance = new Vector2(5f, -5f);
                ring.useGraphicAlpha = false;
                rings[mode] = ring;

                Image icon = UiKit.AddImage(b.transform, "Icon", GameplayIcons.For(mode), Color.white);
                icon.preserveAspect = true;
                var irt = icon.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0f, -8f);
                irt.sizeDelta = new Vector2(GameplayIconPx, GameplayIconPx);

                float labelH = GameplayLabelH();
                Text label = UiKit.AddText(b.transform, "Label", name, Mathf.RoundToInt(GameplayLabelPx), UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
                DropOutline(label);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;   // a word is never broken: the button is sized to the widest label
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.offsetMin = new Vector2(4f, 6f);
                lrt.offsetMax = new Vector2(-4f, 6f + labelH);


                var le = b.gameObject.AddComponent<LayoutElement>();
                le.minWidth = UiKit.MinTouchPx;
                le.preferredWidth = Mathf.Ceil(label.preferredWidth) + 16f;   // widest label plus a little air; spare width is shared out evenly
                le.flexibleWidth = 1f;
                le.minHeight = rowH;
                le.preferredHeight = rowH;
            }
            RefreshGameplayRow(plates, rings, buttons);
            return rowH + GameplayBelowGap;
        }

        void SelectGameplay(int mode, Image[] plates, Outline[] rings, Button[] buttons)
        {
            GameSettings.Difficulty = mode;
            GameSession.ApplyDifficulty();
            RefreshGameplayRow(plates, rings, buttons);
            RefreshMedalBadges();   // the node medals follow the chosen mode
        }

        /// <summary>Sets plate colour, outline and accessibility text of the five buttons from the stored choice.</summary>
        static void RefreshGameplayRow(Image[] plates, Outline[] rings, Button[] buttons)
        {
            int selected = GameSettings.Difficulty;
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] == null) continue;
                bool on = i == selected;
                Sprite plate = ToyKit.Button(on ? 1 : 0);
                if (plate != null) plates[i].sprite = plate;
                rings[i].enabled = on;
                var a11y = buttons[i].GetComponent<A11yLabel>();
                if (a11y != null) a11y.Text = "Gameplay " + GameSettings.DifficultyNames[i] + ", speed-up steps of " + GameSettings.DifficultyStepPercent[i] + " percent, up to plus " + GameSettings.MaxSpeedUpPercent(i) + " percent" + (on ? ", selected" : "");
            }
        }
    }
}
