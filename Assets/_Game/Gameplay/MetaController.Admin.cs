using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Settings extras (docs/38-settings-admin.md): the version label and small Admin button, the hidden code box, the Admin popup (System Test master switch
    /// with the PROD READY / TEST MODE pill, then Game Test, Shop Test, Character Test and Cloud Test), Reset Progress with its confirm popup and Restore Purchases.
    /// </summary>
    public sealed partial class MetaController
    {
        readonly AdminGate.TapCounter versionTaps = new AdminGate.TapCounter();
        Text adminStatus;
        InputField codeField;

        // ---------------- settings footer: Admin icon + version label ----------------

        /// <summary>Last line of the settings popup: the small Admin icon button at the left (when allowed) and the version label at the right (7 quick taps open the code box).</summary>
        void AddSettingsFooter(RectTransform c, float y, float h, bool showAdmin, float width)
        {
            Button v = UiKit.AddHitArea(c, "VersionTap", "Version " + Application.version, new Vector2(1f, 1f), new Vector2(0f, -y), new Vector2(380f, h), OnVersionTapped);
            Text vt = UiKit.AddText(v.transform, "Label", "v" + Application.version, 34, Theme.InkSoft, TextAnchor.MiddleRight);
            DropOutline(vt);
            UiKit.Stretch((RectTransform)vt.transform);
            if (!showAdmin) return;
            Button a = ToyKit.IconButton(c, "Admin", "Admin", 2, ToyKit.Get("icon_lock"), new Vector2(h, h), () => ShowAdmin(), 0.6f);
            UiKit.Place((RectTransform)a.transform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(h, h));
        }

        void OnVersionTapped()
        {
            if (AdminAccess.Available) return;   // already open to this install
            if (versionTaps.Tap(Time.unscaledTimeAsDouble)) ShowAdminCodeBox();
        }

        /// <summary>The small hidden code box. A right code turns admin mode on and opens the Admin popup; a wrong code does nothing (the box stays).</summary>
        public void ShowAdminCodeBox()
        {
            OpenPopup("Enter code", "", 800f);
            Transform panel = popup.Find("Panel");
            Transform emptyBody = panel.Find("Body");
            if (emptyBody != null) Destroy(emptyBody.gameObject);   // this popup has no text, only the field
            RectTransform fieldRt = UiKit.Rect(panel, "CodeField");
            UiKit.Place(fieldRt, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(660f, Mathf.Max(132f, UiKit.MinTouchRef)));
            var bg = fieldRt.gameObject.AddComponent<Image>();
            bg.color = Theme.SurfaceDim;
            Text input = UiKit.AddText(fieldRt, "Text", "", 52, Theme.Ink, TextAnchor.MiddleCenter);
            DropOutline(input);
            input.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch((RectTransform)input.transform);
            Text hint = UiKit.AddText(fieldRt, "Placeholder", "code", 44, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(hint);
            UiKit.Stretch((RectTransform)hint.transform);
            codeField = fieldRt.gameObject.AddComponent<InputField>();
            codeField.textComponent = input;
            codeField.placeholder = hint;
            codeField.characterLimit = 32;
            codeField.contentType = InputField.ContentType.Standard;
            fieldRt.gameObject.AddComponent<A11yLabel>().Text = "Admin code";
            PopupButton("OK", Btn(1), Color.white, 60f, () => SubmitAdminCode(codeField != null ? codeField.text : ""));
        }

        /// <summary>Checks the typed code (public for tests). Returns true and opens the Admin popup when it is right.</summary>
        public bool SubmitAdminCode(string typed)
        {
            if (!AdminAccess.TryUnlock(typed)) return false;   // wrong code: nothing happens, the box stays open
            ShowSettings();   // rebuild so the Admin button appears
            ShowAdmin();
            return true;
        }

        // ---------------- reset progress ----------------

        bool CloudSignedIn { get { return progress != null && progress.Data != null && progress.Data.AccountMode == SaveSync.ModeGoogle; } }

        /// <summary>Reset Progress: the confirm popup. Reset wipes this device's save (levels, heroes, gems, coins); purchases stay recorded.</summary>
        public void ConfirmReset()
        {
            bool cloud = CloudSignedIn;
            OpenPopup("Reset progress?", "This deletes levels, heroes, gems and coins on this device." + (cloud ? "\nYour cloud save is reset too." : "") + "\nPurchases are kept.", 1280f);
            PopupButton("Reset", ToyKit.Button(3), Color.white, 230f, DoReset);
            PopupButton("Cancel", Btn(0), Color.white, 60f, ClosePopup);
        }

        void DoReset()
        {
            progress.ResetAll();
            CharacterCatalog.Selected = CharacterCatalog.Momo;
            PushCloudSave();
            ClosePopup();
            ShowMenu();
        }

        /// <summary>Uploads the current save to the Google account (signed-in players only). Never blocks or throws; the result is ignored.</summary>
        void PushCloudSave()
        {
            if (!CloudSignedIn) return;
            try
            {
                IAccountService acc = Services.Account;
                if (acc == null || !acc.IsSignedIn) return;
                acc.SaveCloud(SaveSync.Serialize(progress.Data), ok => { if (ok) progress.MarkCloudSynced(); });
            }
            catch (Exception e) { Debug.LogWarning("[Settings] cloud save after reset failed: " + e.Message); }
        }

        // ---------------- restore purchases ----------------

        /// <summary>Restore Purchases: asks the store to re-deliver, then re-grants what the recorded purchases entitle the player to (docs/38).</summary>
        public void RestorePurchases()
        {
            try { Services.Payments.Initialize(); } catch (Exception e) { Debug.LogWarning("[Settings] store init failed: " + e.Message); }
            Services.Payments.Restore((ok, msg) =>
            {
                int n = progress.RestoreFromRecords(DateTime.UtcNow);
                if (!ok && n == 0) { ShowMessage("Store not reachable", "Could not reach the store. Check your connection and try again."); return; }
                ShowPurchasesRestored(n);
            });
        }

        /// <summary>The result popup: how many purchases were restored, or that there was nothing to restore.</summary>
        public void ShowPurchasesRestored(int count)
        {
            ShowMessage(count > 0 ? "Purchases restored" : "Nothing to restore", count > 0 ? "Purchases restored: " + count : "Nothing to restore");
            RefreshWallet();
        }

        // ---------------- admin popup ----------------

        const float AdminW = 920f, AdminPad = 28f, AdminMaxH = 1470f;
        RectTransform adminContent;
        int adminPage;

        sealed class AdminBtn
        {
            public string Label, A11y;
            public int Role;
            public Action Click;
            public string Icon;
            public AdminBtn(string label, int role, Action click, string a11y = null, string icon = null) { Label = label; Role = role; Click = click; A11y = a11y ?? label; Icon = icon; }
        }

        /// <summary>
        /// The Admin popup (public for the layout audit). Page 0 = System Test master switch with the PROD READY / TEST MODE pill, then (only while System Test is on)
        /// Game Test, Shop Test, Character Test and Cloud Test; page 1 = Hero test. Height follows the content; it scrolls only when it does not fit.
        /// </summary>
        public void ShowAdmin(int page = 0) { ShowAdmin(page, 0f); }

        void ShowAdmin(int page, float keepScroll)
        {
            adminPage = page;
            RectTransform overlay = NewOverlay();
            string title = page == 1 ? "Hero test" : "Admin";
            RectTransform panel = UiKit.BuildPopupFrame(overlay, title, art, new Vector2(AdminW, AdminMaxH), page == 1 ? (Action)(() => ShowAdmin(0)) : ClosePopup);
            float statusH = Mathf.Ceil(Mathf.Max(36f, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
            adminStatus = UiKit.AddText(panel, "Status", "", 36, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(adminStatus);
            UiKit.Place((RectTransform)adminStatus.transform, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(AdminW - 2f * AdminPad, statusH));
            float top = 186f + statusH + 8f;

            RectTransform content = PopupBodyBegin(panel, "AdminViewport", top, AdminPad);
            adminContent = content;
            float y = 0f;
            if (page == 1) BuildHeroTest(content, ref y);
            else BuildAdminMain(content, ref y);
            PopupBodyEnd(panel, content, y + 16f, top, AdminPad, AdminMaxH);
            if (keepScroll > 0f)
            {
                float vpH = panel.sizeDelta.y - top - AdminPad;
                content.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepScroll, 0f, Mathf.Max(0f, y + 16f - vpH)));
            }
        }

        /// <summary>Rebuilds the popup after a switch changed (the tool rows follow the switches), keeping the scroll position.</summary>
        void AdminRebuild()
        {
            float keep = adminContent != null ? adminContent.anchoredPosition.y : 0f;
            ShowAdmin(adminPage, keep);
        }

        void AdminSay(string text) { if (adminStatus != null) UiKit.SetText(adminStatus, text); }

        float AdminBtnH { get { return Mathf.Max(120f, UiKit.MinTouchRef); } }

        /// <summary>Real height of the text when wrapped to this width (lines wrap at 130 percent text), so rows are stacked from measured heights.</summary>
        static float MeasureH(Text t, float width)
        {
            TextGenerationSettings gs = t.GetGenerationSettings(new Vector2(width, 0f));
            float real = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, gs) / Mathf.Max(0.01f, t.pixelsPerUnit);
            // Pessimistic second estimate (glyph width 0.6 x font, line height 1.4 x font, per paragraph) in case the generator is not ready or is optimistic: take the larger.
            float lineH = t.fontSize * 1.4f, perLine = Mathf.Max(1f, width / (t.fontSize * 0.6f));
            int lines = 0;
            foreach (string para in (t.text ?? "").Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(para.Length / perLine));
            return Mathf.Max(real, lines * lineH) + lineH * 0.5f;   // half a line of safety
        }

        /// <summary>Adds a wrapped text at the running y with its measured height (plus padding) and moves y below it. Returns the height used.</summary>
        float AdminText(RectTransform c, ref float y, string name, string text, int size, Color color, TextAnchor anchor, float x, float width, float minH, float gapAfter)
        {
            Text t = UiKit.AddText(c, name, text, size, color, anchor, true);
            DropOutline(t);
            float h = Mathf.Max(minH, MeasureH(t, width) + 12f);
            UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, h));
            y += h + gapAfter;
            return h;
        }

        void AdminSection(RectTransform c, ref float y, string label, string note = null)
        {
            float w = AdminW - 2f * AdminPad - 16f;
            y += 12f;
            if (!string.IsNullOrEmpty(label)) AdminText(c, ref y, "Section" + label, label, 40, Theme.AccentDeep, TextAnchor.LowerLeft, 8f, w, 56f, 8f);
            if (note != null) AdminText(c, ref y, "Note" + label, note, 30, Theme.InkSoft, TextAnchor.UpperLeft, 8f, w, 40f, 12f);
        }

        /// <summary>Dims a row and makes it untouchable (the test tools while System Test is off).</summary>
        static void AdminDim(Component part, bool enabled)
        {
            if (enabled) return;
            var g = part.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0.45f;
            g.interactable = false;
            g.blocksRaycasts = false;
        }

        /// <summary>The PROD READY (green) or TEST MODE (orange) pill: the words differ as well as the colour.</summary>
        void AdminPill(RectTransform c, ref float y, bool systemTest)
        {
            const float pw = 440f, ph = 84f;
            Image pill = SoftPanel(c, "StatusPill", systemTest ? new Color(1f, 0.66f, 0.2f, 1f) : new Color(0.36f, 0.82f, 0.45f, 1f), 42f);
            UiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(pw, ph));
            Image ic = UiKit.AddImage(pill.transform, "Icon", ToyKit.Get(systemTest ? "icon_bolt" : "icon_lock"), Color.white);
            ic.preserveAspect = true;
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(22f, 0f);
            irt.sizeDelta = new Vector2(56f, 56f);
            Text t = UiKit.AddText(pill.transform, "Label", AdminAccess.PillText(systemTest), 40, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            DropOutline(t);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(84f, 0f);
            trt.offsetMax = new Vector2(-16f, 0f);
            y += ph + 20f;
        }

        /// <summary>An on/off row: icon and label on the left, the kit switch on the right (green knob right = on). A change rebuilds the popup so the tool rows follow.</summary>
        void AdminSwitchRow(RectTransform c, ref float y, string label, string icon, Func<bool> get, Action<bool> set, bool enabled)
        {
            float w = AdminW - 2f * AdminPad, bh = AdminBtnH, sw = 200f;
            Image ic = UiKit.AddImage(c, label + "Icon", ToyKit.Get(icon), Color.white);
            ic.preserveAspect = true;
            Text t = UiKit.AddText(c, label + "Label", label, 40, Theme.Ink, TextAnchor.MiddleLeft, true);
            DropOutline(t);
            float lw = w - 110f - sw - 32f;
            float rowH = Mathf.Max(bh, MeasureH(t, lw) + 12f);
            UiKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -(y + (rowH - 88f) * 0.5f)), new Vector2(88f, 88f));
            UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(110f, -y), new Vector2(lw, rowH));
            bool on = get();
            Button b = UiKit.AddButton(c, label + "Toggle", "", ToyKit.Button(on ? 1 : 2), Color.white, new Vector2(sw, bh), null);
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-8f, -y - (rowH - bh) * 0.5f), new Vector2(sw, bh));
            Image knob = UiKit.AddImage(b.transform, "Knob", Theme.RoundSprite, Color.white);
            knob.type = Image.Type.Sliced;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 0.74f : 0.26f, 0.5f);
            knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
            knob.rectTransform.sizeDelta = new Vector2(86f, 86f);
            Image ring = UiKit.AddImage(knob.transform, "Ring", Theme.RingSprite, UiKit.ButtonInk);
            ring.type = Image.Type.Sliced;
            ring.preserveAspect = false;
            UiKit.Stretch(ring.rectTransform);
            b.GetComponent<A11yLabel>().Text = label + (on ? ", on" : ", off");
            b.onClick.AddListener(() =>
            {
                set(!get());
                AdminRebuild();
            });
            AdminDim(ic, enabled);
            AdminDim(t, enabled);
            AdminDim(b, enabled);
            y += rowH + 12f;
        }

        /// <summary>Tool buttons in two columns (an odd last one spans the full width). Dimmed and untouchable when not enabled.</summary>
        void AdminButtons(RectTransform c, ref float y, bool enabled, params AdminBtn[] btns)
        {
            float w = AdminW - 2f * AdminPad, bh = AdminBtnH, gap = Mathf.Ceil(UiKit.MinSpacingPx);
            float half = (w - gap) * 0.5f;
            for (int i = 0; i < btns.Length; i += 2)
            {
                bool single = i + 1 >= btns.Length;
                for (int k = 0; k < (single ? 1 : 2); k++)
                {
                    AdminBtn d = btns[i + k];
                    float bw = single ? w : half;
                    Button b = UiKit.AddButton(c, d.Label.Replace(' ', '_') + "Button", d.Label, ToyKit.Button(d.Role), Color.white, new Vector2(bw, bh), d.Click, 40);
                    UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(k * (half + gap), -y), new Vector2(bw, bh));
                    b.GetComponent<A11yLabel>().Text = d.A11y;
                    if (d.Icon != null)
                    {
                        Image ic = UiKit.AddImage(b.transform, "Icon", ToyKit.Get(d.Icon), Color.white);
                        ic.preserveAspect = true;
                        ic.raycastTarget = false;
                        RectTransform irt = ic.rectTransform;
                        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                        irt.pivot = new Vector2(0f, 0.5f);
                        irt.anchoredPosition = new Vector2(16f, 0f);
                        irt.sizeDelta = new Vector2(72f, 72f);
                        Text lt = b.GetComponentInChildren<Text>();
                        if (lt != null) { RectTransform lrt = lt.rectTransform; lrt.offsetMin = new Vector2(96f, lrt.offsetMin.y); }
                    }
                    AdminDim(b, enabled);
                }
                y += bh + gap;
            }
            y += 6f;
        }

        void BuildAdminMain(RectTransform c, ref float y)
        {
            bool sys = GameSettings.SystemTest;
            bool on = AdminAccess.TestToolsEnabled(sys);   // everything under the master switch works only while System Test is on
            AdminPill(c, ref y, sys);
            AdminSwitchRow(c, ref y, "System Test", "icon_settings", () => GameSettings.SystemTest, v => { GameSettings.SystemTest = v; if (!v) progress.AdminSetMainRewardsReady(false); }, true);   // OFF clears the three switches below too

            // GAME TEST: the old Jump test (every level and region open) and the level tools
            AdminSwitchRow(c, ref y, AdminAccess.TestSwitchLabels[0], "icon_flag", () => GameSettings.GameJumpTest, v => GameSettings.GameJumpTest = v, on);
            if (on)
            {
                if (GameSettings.GameJumpTest)
                    AdminButtons(c, ref y, true,
                        new AdminBtn("Open regions", 0, () => AdminSay("Regions unlocked: " + progress.AdminUnlockAllRegions()), "Unlock all regions"),
                        new AdminBtn("Clear levels", 0, () => { int n = progress.AdminCompleteAllLevels(); RefreshWallet(); AdminSay("Levels cleared: " + n); }, "Clear all levels"),
                        MainRewardsBtn());
                else AdminButtons(c, ref y, true, MainRewardsBtn());   // the Main rewards review tool works without the jump switch
            }

            // SHOP TEST: test purchases, wallet and VIP, ads
            AdminSwitchRow(c, ref y, AdminAccess.TestSwitchLabels[1], "icon_shop", () => GameSettings.ShopTest, v => GameSettings.ShopTest = v, on);
            if (on && GameSettings.ShopTest)
                AdminButtons(c, ref y, true,
                    new AdminBtn("Big wallet", 0, () => { progress.AddCoins(1000000); progress.AddGems(100000); RefreshWallet(); AdminSay("+1M coins, +100k gems"); }, "Big wallet: add 1,000,000 coins and 100,000 gems"),
                    new AdminBtn("VIP monthly", 0, AdminVipOn, "VIP on (monthly)"),
                    new AdminBtn("+1,000 coins", 0, () => { progress.AddCoins(1000); RefreshWallet(); AdminSay("Coins: " + progress.Coins); }, "Add 1,000 coins"),
                    new AdminBtn("+100 gems", 0, () => { progress.AddGems(100); RefreshWallet(); AdminSay("Gems: " + progress.Gems); }, "Add 100 gems"),
                    new AdminBtn("Buy 50 gems", 0, AdminTestPurchase, "Buy 50 gems (store test)"),
                    new AdminBtn("Rewarded ad", 0, AdminTestAd, "Show a rewarded ad"));

            // CHARACTER TEST: every hero owned and the hero copies tool
            AdminSwitchRow(c, ref y, AdminAccess.TestSwitchLabels[2], "icon_heroes", () => GameSettings.CharacterTest, v => GameSettings.CharacterTest = v, on);
            if (on && GameSettings.CharacterTest)
                AdminButtons(c, ref y, true, new AdminBtn("Pick a hero...", 1, () => ShowAdmin(1), "Pick a hero and add copies"));

            // CLOUD TEST: only while System Test is on
            if (on) BuildAdminCloud(c, ref y);

            if (AdminAccess.Unlocked)
            {
                AdminSection(c, ref y, "ADMIN MODE");
                AdminButtons(c, ref y, true, new AdminBtn("Lock admin again", 3, () => { AdminAccess.Lock(); ClosePopup(); ShowSettings(); }));
            }
        }

        /// <summary>The "Main rewards" test button (Game Test group): icon-first, label shows ON/OFF. Marks every Main reward complete but unclaimed, tap again to restore.</summary>
        AdminBtn MainRewardsBtn()
        {
            bool on = progress.AdminMainRewardsReady;
            return new AdminBtn("Main rewards " + (on ? "ON" : "OFF"), on ? 1 : 0, () =>
            {
                bool now = progress.AdminSetMainRewardsReady(!progress.AdminMainRewardsReady);
                RefreshWallet();
                AdminSay(now ? "All Main rewards ready to claim" : "Main rewards back to real progress");
                AdminRebuild();
            }, "Main rewards test, " + (on ? "on" : "off") + ". Marks every Main reward complete, not claimed.", "icon_daily");
        }

        void AdminVipOn()
        {
            SubscriptionBuyResult r = progress.CreditSubscription("monthly", DateTime.UtcNow);   // no purchase record: this is a test grant
            AdminSay(r == SubscriptionBuyResult.Ok ? "VIP on: " + progress.DaysLeft() + " days" : "VIP: " + r);
            RefreshWallet();
        }

        void AdminTestAd()
        {
            AdminSay("Ad requested...");
            try { Services.Ads.ShowRewarded(AdPlacements.ShopFreeGems, ok => AdminSay(ok ? "Ad finished: reward ok" : "Ad closed: no reward")); }
            catch (Exception e) { AdminSay("Ad error: " + e.Message); }
        }

        void AdminTestPurchase()
        {
            IPaymentService pay = Services.Payments;
            pay.Initialize();
            pay.Purchase(ProductCatalog.Gems50, r =>
            {
                if (!r.Success) { AdminSay("Purchase " + r.Status + (string.IsNullOrEmpty(r.Error) ? "" : ": " + r.Error)); return; }
                GemPackDef pack = null;
                foreach (GemPackDef g in ShopCatalog.FromRepository(repo).GemPacks) if (g.Gems == 50) pack = g;
                if (pack != null && progress.CreditGemPack(pack) == ShopResult.Ok)
                {
                    progress.RecordPurchase(r.ProductId, PurchaseRecord.KindConsumable, r.TransactionId, DateTime.UtcNow);
                    RefreshWallet();
                    AdminSay("Bought 50 gems");
                }
                else AdminSay("Purchase ok, pack missing");
            });
        }

        void BuildAdminCloud(RectTransform c, ref float y)
        {
            IAccountService acc = Services.Account;
            AdminSection(c, ref y, "CLOUD TEST");
            if (acc == null) { AdminText(c, ref y, "CloudState", "No account service in this build.", 36, Theme.InkSoft, TextAnchor.MiddleLeft, 8f, AdminW - 2f * AdminPad - 16f, 60f, 8f); return; }
            AdminText(c, ref y, "CloudState", acc.IsSignedIn ? "Signed in: " + acc.DisplayName : "Signed out", 36, Theme.Ink, TextAnchor.MiddleLeft, 8f, AdminW - 2f * AdminPad - 16f, 60f, 8f);
            if (acc.IsSignedIn)
            {
                AdminButtons(c, ref y, true,
                    new AdminBtn("Upload save", 0, () =>
                    {
                        AdminSay("Uploading...");
                        acc.SaveCloud(SaveSync.Serialize(progress.Data), ok => { if (ok) progress.MarkCloudSynced(); AdminSay(ok ? "Cloud save uploaded" : "Upload failed"); });
                    }, "Upload the save now"),
                    new AdminBtn("Sign out", 3, () => { acc.SignOut(); AdminSay("Signed out"); AdminRebuild(); }));
            }
            else
            {
                AdminButtons(c, ref y, true, new AdminBtn("Sign in", 1, () =>
                {
                    AdminSay("Signing in...");
                    acc.SignIn(res => { if (res.Success) AdminRebuild(); else AdminSay("Sign-in " + res.Status); });
                }, "Sign in (Google)"));
            }
        }

        // ---------------- hero test ----------------

        void BuildHeroTest(RectTransform c, ref float y)
        {
            AdminSection(c, ref y, "ADD COPIES", "Tap +1 or +2 to add copies to the hero (3 copies merge to the next tier).");
            float w = AdminW - 2f * AdminPad, bh = AdminBtnH, bw = 150f;
            foreach (CharacterSpec spec in CharacterDefs.All)
            {
                CharacterSpec s = spec;
                float lw = w - 2f * bw - 40f;
                Text t = UiKit.AddText(c, s.Id + "Label", HeroLine(s), 34, Theme.Ink, TextAnchor.MiddleLeft, true);
                DropOutline(t);
                float rowH = Mathf.Max(bh, MeasureH(t, lw) + 16f);
                UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(lw, rowH));
                Action<int> add = n =>
                {
                    int now = progress.AddCopies(s.Id, n);
                    UiKit.SetText(t, HeroLine(s));
                    AdminSay(s.Name + ": " + now + " copies");
                };
                float by = -y - (rowH - bh) * 0.5f;
                Button b2 = UiKit.AddButton(c, s.Id + "Add2", "+2", ToyKit.Button(1), Color.white, new Vector2(bw, bh), () => add(2), 40);
                UiKit.Place((RectTransform)b2.transform, new Vector2(1f, 1f), new Vector2(-8f, by), new Vector2(bw, bh));
                b2.GetComponent<A11yLabel>().Text = "Add 2 copies of " + s.Name;
                Button b1 = UiKit.AddButton(c, s.Id + "Add1", "+1", ToyKit.Button(0), Color.white, new Vector2(bw, bh), () => add(1), 40);
                UiKit.Place((RectTransform)b1.transform, new Vector2(1f, 1f), new Vector2(-(8f + bw + 20f), by), new Vector2(bw, bh));
                b1.GetComponent<A11yLabel>().Text = "Add 1 copy of " + s.Name;
                y += rowH + 12f;
            }
        }

        string HeroLine(CharacterSpec s)
        {
            return s.Name + "  (" + CharacterDefs.RarityNames[(int)s.Rarity] + ")\ncopies: " + progress.Copies(s.Id);
        }
    }
}
