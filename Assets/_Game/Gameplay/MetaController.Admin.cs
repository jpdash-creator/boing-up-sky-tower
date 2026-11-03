using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Settings extras (docs/38-settings-admin.md): the version label and small Admin button, the hidden code box, the Admin popup (Game test, System test,
    /// Hero test, Economy test, Ads/IAP test, Cloud save test), Reset Progress with its confirm popup and Restore Purchases.
    /// </summary>
    public sealed partial class MetaController
    {
        readonly AdminGate.TapCounter versionTaps = new AdminGate.TapCounter();
        Text adminStatus;
        InputField codeField;

        // ---------------- settings footer: version label + Admin button ----------------

        /// <summary>Last line of the settings card: the version label at the left (7 quick taps open the code box) and, when allowed, the small Admin button at the right.</summary>
        void AddSettingsFooter(RectTransform card, float y, float h, bool showAdmin)
        {
            Button v = UiKit.AddHitArea(card, "VersionTap", "Version " + Application.version, new Vector2(0f, 1f), new Vector2(28f, -y), new Vector2(380f, h), OnVersionTapped);
            Text vt = UiKit.AddText(v.transform, "Label", "v" + Application.version, 34, Theme.InkSoft, TextAnchor.MiddleLeft);
            DropOutline(vt);
            UiKit.Stretch((RectTransform)vt.transform);
            if (!showAdmin) return;
            Button a = UiKit.AddButton(card, "Admin", "Admin", ToyKit.Button(2), Color.white, new Vector2(280f, h), () => ShowAdmin(), 40);
            UiKit.Place((RectTransform)a.transform, new Vector2(1f, 1f), new Vector2(-28f, -y), new Vector2(280f, Mathf.Max(h, UiKit.MinTouchRef)));
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

        const float AdminW = 920f, AdminH = 1340f, AdminPad = 28f;

        /// <summary>The Admin popup, page 0 = tests, page 1 = Hero test (public for the layout audit).</summary>
        public void ShowAdmin(int page = 0)
        {
            RectTransform overlay = NewOverlay();
            string title = page == 1 ? "Hero test" : "Admin";
            RectTransform panel = UiKit.BuildPopupFrame(overlay, title, art, new Vector2(AdminW, AdminH), page == 1 ? (Action)(() => ShowAdmin(0)) : ClosePopup);
            adminStatus = UiKit.AddText(panel, "Status", "", 36, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(adminStatus);
            UiKit.Place((RectTransform)adminStatus.transform, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(AdminW - 2f * AdminPad, 60f));

            RectTransform vp = UiKit.Rect(panel, "AdminViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(AdminPad, AdminPad);
            vp.offsetMax = new Vector2(-AdminPad, -254f);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            RectTransform content = UiKit.Rect(vp, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 60f;

            float y = 0f;
            if (page == 1) BuildHeroTest(content, ref y);
            else BuildAdminMain(content, ref y);
            content.sizeDelta = new Vector2(0f, y + 16f);
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

        /// <summary>A full-width action button.</summary>
        void AdminAction(RectTransform c, ref float y, string label, int role, Action onClick)
        {
            float w = AdminW - 2f * AdminPad;
            Button b = UiKit.AddButton(c, label.Replace(' ', '_') + "Button", label, ToyKit.Button(role), Color.white, new Vector2(w, AdminBtnH), onClick, 40);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(w, AdminBtnH));
            y += AdminBtnH + 18f;
        }

        /// <summary>An on/off row: label on the left, the kit switch on the right (green knob right = on).</summary>
        void AdminSwitch(RectTransform c, ref float y, string label, Func<bool> get, Action<bool> set)
        {
            float w = AdminW - 2f * AdminPad, bh = AdminBtnH, sw = 200f;
            Text t = UiKit.AddText(c, label + "Label", label, 40, Theme.Ink, TextAnchor.MiddleLeft, true);
            DropOutline(t);
            float lw = w - sw - 40f;
            float rowH = Mathf.Max(bh, MeasureH(t, lw) + 12f);
            UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(lw, rowH));
            Button b = UiKit.AddButton(c, label + "Toggle", "", ToyKit.Button(get() ? 1 : 2), Color.white, new Vector2(sw, bh), null);
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-8f, -y - (rowH - bh) * 0.5f), new Vector2(sw, bh));
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
                AdminSay(label + (on ? ": on" : ": off"));
            });
            y += rowH + 12f;
        }

        void BuildAdminMain(RectTransform c, ref float y)
        {
            AdminSection(c, ref y, "GAME TEST", "Level and region tools. All levels open needs System test on.");
            AdminSwitch(c, ref y, "All levels open", () => GameSettings.GameJumpTest, v => GameSettings.GameJumpTest = v);
            AdminAction(c, ref y, "Unlock all regions", 0, () => AdminSay("Regions unlocked: " + progress.AdminUnlockAllRegions()));
            AdminAction(c, ref y, "Clear all levels", 0, () => { int n = progress.AdminCompleteAllLevels(); RefreshWallet(); AdminSay("Levels cleared: " + n); });

            AdminSection(c, ref y, "SYSTEM TEST", "Opens everything: all heroes, test purchases, Autopilot.");
            AdminSwitch(c, ref y, "System test", () => GameSettings.SystemTest, v => GameSettings.SystemTest = v);
            AdminAction(c, ref y, "Big wallet", 0, () => { progress.AddCoins(1000000); progress.AddGems(100000); RefreshWallet(); AdminSay("+1,000,000 coins, +100,000 gems"); });
            AdminAction(c, ref y, "VIP on (monthly)", 0, AdminVipOn);

            AdminSection(c, ref y, "HERO TEST");
            AdminAction(c, ref y, "Pick a hero...", 1, () => ShowAdmin(1));

            AdminSection(c, ref y, "ECONOMY TEST");
            AdminAction(c, ref y, "Add 1,000 coins", 0, () => { progress.AddCoins(1000); RefreshWallet(); AdminSay("Coins: " + progress.Coins); });
            AdminAction(c, ref y, "Add 100 gems", 0, () => { progress.AddGems(100); RefreshWallet(); AdminSay("Gems: " + progress.Gems); });

            AdminSection(c, ref y, "ADS / IAP TEST", "Uses the stub services in this build.");
            AdminAction(c, ref y, "Show rewarded ad", 0, AdminTestAd);
            AdminAction(c, ref y, "Buy 50 gems (store)", 0, AdminTestPurchase);

            AdminSection(c, ref y, "CLOUD SAVE TEST");
            BuildAdminCloud(c, ref y);

            if (AdminAccess.Unlocked)
            {
                AdminSection(c, ref y, "ADMIN MODE");
                AdminAction(c, ref y, "Lock admin again", 3, () => { AdminAccess.Lock(); ClosePopup(); ShowSettings(); });
            }
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
                    AdminSay("Bought 50 gems (" + r.TransactionId + ")");
                }
                else AdminSay("Purchase ok, pack missing");
            });
        }

        void BuildAdminCloud(RectTransform c, ref float y)
        {
            IAccountService acc = Services.Account;
            if (acc == null) { AdminSection(c, ref y, "", "No account service in this build."); return; }
            AdminText(c, ref y, "CloudState", acc.IsSignedIn ? "Signed in: " + acc.DisplayName : "Signed out", 36, Theme.Ink, TextAnchor.MiddleLeft, 8f, AdminW - 2f * AdminPad - 16f, 60f, 8f);
            if (acc.IsSignedIn)
            {
                AdminAction(c, ref y, "Upload save now", 0, () =>
                {
                    AdminSay("Uploading...");
                    acc.SaveCloud(SaveSync.Serialize(progress.Data), ok => { if (ok) progress.MarkCloudSynced(); AdminSay(ok ? "Cloud save uploaded" : "Upload failed"); });
                });
                AdminAction(c, ref y, "Sign out", 3, () => { acc.SignOut(); AdminSay("Signed out"); ShowAdmin(); });
            }
            else
            {
                AdminAction(c, ref y, "Sign in (Google)", 1, () =>
                {
                    AdminSay("Signing in...");
                    acc.SignIn(res => { if (res.Success) ShowAdmin(); else AdminSay("Sign-in " + res.Status); });
                });
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
