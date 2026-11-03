using System;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The timer bundle offer (docs/26 section 3): a round-ish button at the bottom-left above the tab bar with the bag icon, "30%" and the
    /// time left as mm:ss, shown on the home and world-map screens only while an offer runs (never during a run: this component only exists on those
    /// menu screens). A tap opens a popup with the bundle, its chips, the discounted price and a Buy button; the popup's X closes it and a second
    /// button hides the offer for good. The component also feeds the play-time counter (ProgressService.TickPlayTime, once per second).
    ///
    /// Performance: Update adds one float per frame; everything else runs once per second. The countdown strings come from a cache of 301 strings, so
    /// a steady tick allocates nothing (the button text changes only when the whole second changes).
    /// Layout: a round-cornered plate of 220 px (about 80 dp) at the bottom LEFT, just above the tab bar (above the Shop tab), on the home and map screens:
    /// "30%" on top, the bag in the middle, mm:ss at the bottom, in three non-overlapping rows (they hold at 130 percent text).
    /// </summary>
    public sealed class BundleTimerButton : MonoBehaviour
    {
        const float W = 220f, H = 220f;

        /// <summary>Extra height of the timer button on the World Map screen only (canvas px): keeps it clear of the Meadow Springs pin and tag at the bottom of the map. PlanTags uses the same value.</summary>
        public const float MapLift = 300f;

        static readonly string[] Mmss = new string[ProgressService.BundleTimerWindowSec + 1];

        ProgressService progress;
        ArtCatalog art;
        ShopScreen.Host host;
        Action refreshWallet;
        GameObject buttonGo;
        Text timeText;
        Text popupTime;
        int shown = -1;
        bool visible = true;
        float acc;

        /// <summary>Adds the button (hidden until an offer runs) to a menu screen. Safe to call on every screen build; the old screen's copy is destroyed with it.</summary>
        public static BundleTimerButton Attach(RectTransform screen, ProgressService progress, ArtCatalog art, ShopScreen.Host host, Action refreshWallet, bool visible = true, float bottomLift = 0f)
        {
            if (screen == null || progress == null) return null;
            RectTransform root = UiKit.Rect(screen, "BundleTimer");
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(16f, ToyKit.TabBarReserve + 8f + bottomLift);   // bottomLift: the world map raises it (MapLift) so it clears the Meadow Springs pin and tag
            root.sizeDelta = new Vector2(W, H);
            var t = root.gameObject.AddComponent<BundleTimerButton>();
            t.progress = progress;
            t.art = art;
            t.host = host;
            t.refreshWallet = refreshWallet;
            t.visible = visible;
            t.Build(root);
            t.Refresh();
            return t;
        }

        /// <summary>"04:59" for a number of seconds (cached, 0 to 300).</summary>
        public static string FormatTime(int seconds)
        {
            int s = Mathf.Clamp(seconds, 0, ProgressService.BundleTimerWindowSec);
            string text = Mmss[s];
            if (text == null)
            {
                text = (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
                Mmss[s] = text;
            }
            return text;
        }

        void Build(RectTransform root)
        {
            Button b = UiKit.AddButton(root, "BundleOffer", "", ToyKit.Button(0), Color.white, new Vector2(W, H), OpenOffer);
            b.GetComponent<Image>().raycastTarget = true;
            b.GetComponent<A11yLabel>().Text = "Special bundle offer, 30 percent off. Opens the offer.";
            UiKit.Stretch((RectTransform)b.transform);
            buttonGo = b.gameObject;

            Image bag = UiKit.AddImage(b.transform, "Bag", BundlesScreen.BagIcon(art), Color.white);
            bag.preserveAspect = true;
            UiKit.Place(bag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(72f, 72f));

            Text off = UiKit.AddText(b.transform, "Off", ProgressService.BundleTimerPercentOff + "%", 42, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(off);
            off.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ort = (RectTransform)off.transform;
            ort.anchorMin = new Vector2(0f, 0f);
            ort.anchorMax = new Vector2(1f, 0f);
            ort.pivot = new Vector2(0.5f, 0f);
            ort.offsetMin = new Vector2(4f, 152f);
            ort.offsetMax = new Vector2(-4f, 218f);

            timeText = UiKit.AddText(b.transform, "Time", "00:00", 42, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(timeText);
            timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)timeText.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.offsetMin = new Vector2(4f, 2f);
            trt.offsetMax = new Vector2(-4f, 68f);
        }

        void Update()
        {
            acc += Time.unscaledDeltaTime;
            if (acc < 1f) return;
            int whole = (int)acc;
            acc -= whole;
            progress.TickPlayTime(whole);
            Refresh();
        }

        /// <summary>Shows or hides the button and updates the countdown, only when the whole second changed.</summary>
        void Refresh()
        {
            DateTime now = progress.BundleClock();
            int left = progress.TimerOfferSecondsLeft(now);
            if (left == shown) return;
            shown = left;
            bool on = visible && left > 0 && progress.CurrentTimerOffer(now) != null;
            if (buttonGo.activeSelf != on) buttonGo.SetActive(on);
            if (!on) return;
            string t = FormatTime(left);
            UiKit.SetText(timeText, t);
            if (popupTime != null) UiKit.SetText(popupTime, t);
        }

        // ---------------- the popup ----------------

        void Info(string title, string body)
        {
            host.OpenPopup(title, body, 860f);
            host.PopupButton("OK", false, 60f, host.ClosePopup);
        }

        /// <summary>Opens the offer popup (the button's tap; also called by the layout test).</summary>
        public void OpenOffer()
        {
            DateTime now = progress.BundleClock();
            BundleDef def = progress.CurrentTimerOffer(now);
            if (def == null)
            {
                shown = -1;
                Refresh();
                return;
            }
            int price = progress.TimerOfferPrice(def);
            if (host.NewOverlay == null)
            {
                host.OpenPopup("30% OFF!", def.Name + "\n" + BundlesScreen.Describe(def, ", ") + "\nNow " + BundlesScreen.Num(price) + " gems, was " + BundlesScreen.Num(def.PriceGems) + ".", 1180f);
                host.PopupButton("Buy for " + BundlesScreen.Num(price) + " gems", true, 230f, Buy);
                host.PopupButton("Close", false, 60f, host.ClosePopup);
                return;
            }

            RectTransform overlay = host.NewOverlay();
            // every row is placed from its real (text-scale aware) height, top to bottom, with 12 px gaps; the panel grows when the rows need it
            const float W = 780f, gap = 12f, bagTop = 190f;
            float bagSize = 150f;
            float canvasH = Mathf.Max(1f, Screen.height) / Mathf.Sqrt(Mathf.Max(1f, Screen.width) / UiKit.Reference.x * Mathf.Max(1f, Screen.height) / UiKit.Reference.y) * Mathf.Clamp01(UiKit.SafeAreaNormalized().height);
            string wasText = "Was " + BundlesScreen.Num(def.PriceGems) + " gems. Ends in:";
            float nameY = bagTop + bagSize + gap, nameH = BundlesScreen.TextHeight(def.Name, 50f, W);
            float hintY = nameY + nameH + gap, hintH = def.HiddenCard ? BundlesScreen.TextHeight(BundlesScreen.HiddenCardHint, 36f, W) : 0f;
            float chipY = hintY + hintH + (def.HiddenCard ? gap : 0f), chipH = BundlesScreen.ChipBlockHeight(BundlesScreen.ChipRows(def));
            float wasY = chipY + chipH + gap, wasH = BundlesScreen.TextHeight(wasText, 44f, W);
            float leftY = wasY + wasH + 4f, leftH = BundlesScreen.TextHeight("00:00", 64f, W);
            float panelH = Mathf.Max(1400f, leftY + leftH + 412f);
            if (panelH > canvasH * 0.96f)
            {
                // a short canvas (3:4) at large text: the decorative bag goes, everything below moves up so the panel still fits
                float up = bagSize + gap;
                bagSize = 0f; nameY -= up; hintY -= up; chipY -= up; wasY -= up; leftY -= up; panelH -= up;
            }
            // 412 = the two buttons below (Buy up from 230, Hide offer up from 60) and a gap
            RectTransform panel = UiKit.BuildPopupFrame(overlay, ProgressService.BundleTimerPercentOff + "% OFF!", art, new Vector2(860f, panelH), host.CloseOverlay);

            if (bagSize > 0f)
            {
                Image bag = UiKit.AddImage(panel, "Bag", BundlesScreen.BagIcon(art), Color.white);
                bag.preserveAspect = true;
                BundlesScreen.PlaceTop(bag.rectTransform, bagTop, bagSize, bagSize);
            }

            Text name = UiKit.AddText(panel, "Name", def.Name, 50, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(name);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            name.resizeTextMaxSize = name.fontSize;
            BundlesScreen.PlaceTop((RectTransform)name.transform, nameY, W, nameH);

            if (def.HiddenCard)
            {
                Text hint = UiKit.AddText(panel, "CardHint", BundlesScreen.HiddenCardHint, 36, UiKit.PanelInk, TextAnchor.MiddleCenter);
                UiKit.OnPanel(hint);
                hint.horizontalOverflow = HorizontalWrapMode.Wrap;
                hint.resizeTextForBestFit = true;
                hint.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
                hint.resizeTextMaxSize = hint.fontSize;
                BundlesScreen.PlaceTop((RectTransform)hint.transform, hintY, W, hintH);
            }

            BundlesScreen.AddChips(panel, def, art, 40f, chipY);

            Text was = UiKit.AddText(panel, "Was", wasText, 44, UiKit.PanelInk, TextAnchor.MiddleCenter);
            UiKit.OnPanel(was);
            was.horizontalOverflow = HorizontalWrapMode.Wrap;
            BundlesScreen.PlaceTop((RectTransform)was.transform, wasY, W, wasH);

            popupTime = UiKit.AddText(panel, "Left", FormatTime(progress.TimerOfferSecondsLeft(now)), 64, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(popupTime);
            BundlesScreen.PlaceTop((RectTransform)popupTime.transform, leftY, W, leftH);

            ToyKit.PopupAction(panel, "Buy for " + BundlesScreen.Num(price) + " gems", ToyKit.Button(1), Color.white, 230f, Buy);
            ToyKit.PopupAction(panel, "Hide offer", ToyKit.Button(0), Color.white, 60f, HideOffer);
        }

        void Buy()
        {
            DateTime now = progress.BundleClock();
            BundleDef def = progress.CurrentTimerOffer(now);
            if (def == null)
            {
                host.CloseOverlay();
                Info("Offer ended", "This offer has ended.");
                shown = -1;
                Refresh();
                return;
            }
            int price = progress.TimerOfferPrice(def);
            if (progress.Gems < price)
            {
                host.CloseOverlay();
                Info("Not enough gems", "You need " + BundlesScreen.Num(price) + " gems and have " + BundlesScreen.Num(progress.Gems) + ".\nOpen the Gems tab in the shop to get more.");
                return;
            }
            CharacterSpec card;
            BundleResult r = progress.TryBuyTimerOffer(now, out card);
            host.CloseOverlay();
            BundlesScreen.ShowResult(host, refreshWallet, def, r, card);
            shown = -1;
            Refresh();
        }

        void HideOffer()
        {
            progress.DismissTimerOffer();
            host.CloseOverlay();
            shown = -1;
            Refresh();
        }
    }
}
