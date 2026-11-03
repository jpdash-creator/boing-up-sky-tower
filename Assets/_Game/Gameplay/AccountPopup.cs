using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Account pop-up opened by the round account button on Home (owner issue 35.3, docs/35 section 8). Three states: Guest (Sign in with Google Play),
    /// SignedIn (Sync now, Sign out) and Unavailable (a release build without the Google plugin: only a message). The state logic and texts are pure
    /// (unit-tested); the view is built in code on its own dim layer named Popup, sized from the text size so nothing clips at 130 percent (CUX-3/5).
    /// </summary>
    public static class AccountPopup
    {
        public enum State { Guest, SignedIn, Unavailable }

        public const string RootName = "Popup";
        public const string UnavailableText = "Google sign-in is not available in this build.";
        public const string GuestHint = "Sign in to keep your progress safe on any device.";
        public const string SignedInHint = "Your progress is saved to your Google account.";
        public const string SignedOutText = "Signed out. Your progress stays on this device.";
        public const string SignInLabel = "Sign in with Google";
        public const string UnlinkLabel = "Unlink Google Account";
        public const string SaveLabel = "Save Progress";
        /// <summary>Short results of Save Progress (they replace the button label for a moment, so each fits one line at 130 percent text).</summary>
        public const string SavedLocalText = "Saved on this phone";
        public const string SavedGoogleText = "Saved to Google";
        public const string SavedLocalOnlyText = "Saved on phone only";
        public const string NothingToSaveText = "Nothing to save";

        // ---------------- pure logic ----------------

        /// <summary>A linked Google account is SignedIn (even offline); otherwise Guest, or Unavailable when this build cannot sign in at all.</summary>
        public static State StateFor(bool googleLinked, bool available)
        {
            if (googleLinked) return State.SignedIn;
            return available ? State.Guest : State.Unavailable;
        }

        public static State Current { get { return StateFor(AccountSync.IsGoogleMode, AccountSync.Account.IsAvailable); } }

        /// <summary>First letter of the player's name, upper case ("" when there is none).</summary>
        public static string InitialOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            for (int i = 0; i < name.Length; i++)
                if (char.IsLetterOrDigit(name[i])) return char.ToUpperInvariant(name[i]).ToString();
            return "";
        }

        /// <summary>The letter on the round Home button: the player's initial when signed in, else the Google "G".</summary>
        public static string ButtonLetter(State state, string displayName)
        {
            if (state != State.SignedIn) return "G";
            string initial = InitialOf(displayName);
            return initial.Length > 0 ? initial : "G";
        }

        public static string ButtonA11y(State state) { return state == State.SignedIn ? "Account, signed in" : "Account"; }

        /// <summary>The line under the title ("Signed in as X" / "Guest: ..."); Unavailable builds are guests too.</summary>
        public static string LineFor(State state)
        {
            return AccountSync.AccountLine;
        }

        /// <summary>The small helper text under the line: a hint in the normal states, the notice when sign-in does not exist in this build.</summary>
        public static string HintFor(State state)
        {
            switch (state)
            {
                case State.SignedIn: return SignedInHint;
                case State.Unavailable: return UnavailableText;
                default: return GuestHint;
            }
        }

        /// <summary>Label of the Google button in Settings: Sign in with Google (guest) or Unlink Google Account (signed in); "" when there is none (unavailable build).</summary>
        public static string SettingsButtonLabel(State state)
        {
            switch (state)
            {
                case State.SignedIn: return UnlinkLabel;
                case State.Guest: return SignInLabel;
                default: return "";
            }
        }

        /// <summary>The player-facing text after an action: the message when there is one, else a short success line.</summary>
        public static string ResultText(bool ok, string message)
        {
            if (!string.IsNullOrEmpty(message)) return message;
            return ok ? "Signed in." : "Something went wrong. Try again.";
        }

        /// <summary>Sign in (guest) or sign out (signed in): the one action shared by this pop-up and the Settings button. done(ok, text) runs once.</summary>
        public static void RunPrimary(State state, Action<bool, string> done)
        {
            switch (state)
            {
                case State.Guest:
                    AccountSync.SignInAndLink((ok, message) => { if (done != null) done(ok, ResultText(ok, message)); });
                    break;
                case State.SignedIn:
                    AccountSync.SignOut();
                    if (done != null) done(true, SignedOutText);
                    break;
                default:
                    if (done != null) done(false, UnavailableText);
                    break;
            }
        }

        /// <summary>
        /// Settings > Save Progress. The save is always written to this device first. A guest is done ("Saved on this phone"); a player linked to Google
        /// also pushes it to Google Saved Games (AccountSync.SyncNow, the SaveSync upload): "Saved to Google", or "Saved on phone only" when the upload failed.
        /// done(ok, text) runs once with a short text.
        /// </summary>
        public static void SaveProgress(ProgressService progress, Action<bool, string> done)
        {
            if (progress == null) { if (done != null) done(false, NothingToSaveText); return; }
            try { progress.SaveLocalNow(); }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] local save failed: " + e.Message);
                if (done != null) done(false, NothingToSaveText);
                return;
            }
            if (!AccountSync.IsGoogleMode) { if (done != null) done(true, SavedLocalText); return; }
            AccountSync.SyncNow((ok, text) => { if (done != null) done(ok, ok ? SavedGoogleText : SavedLocalOnlyText); });
        }

        // ---------------- view ----------------

        /// <summary>Handles to the parts that change after an action.</summary>
        public sealed class View
        {
            public RectTransform Root;
            public Text Line, Hint, Status;
            public State State;
            public Button Sync, SignOut, SignIn;
        }

        static float Lh(int font, int lines)
        {
            return lines * Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
        }

        /// <summary>The pop-up on its own dim layer. onClose runs from the X; onChanged after a sign-in or sign-out (the caller refreshes its account button).</summary>
        public static View Build(Transform canvas, ArtCatalog art, Action onClose, Action onChanged)
        {
            var view = new View();
            RectTransform root = UiKit.Rect(canvas, RootName);
            UiKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            view.Root = root;
            Fill(view, art, onClose, onChanged, "");
            return view;
        }

        /// <summary>(Re)builds the panel for the current state, keeping the dim layer.</summary>
        static void Fill(View view, ArtCatalog art, Action onClose, Action onChanged, string status)
        {
            Transform old = view.Root.Find("Panel");
            if (old != null) { old.name = "PanelOld"; old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }

            State st = Current;
            view.State = st;
            const float W = 740f, gap = 24f, pad = 52f;
            float btnH = Mathf.Max(150f, UiKit.MinTouchRef);
            float googleH = Mathf.Max(170f, UiKit.MinTouchRef);
            float lineH = Lh(46, 3), hintH = Lh(40, 2), statusH = Lh(40, 2);
            float buttons = st == State.Guest ? googleH : (st == State.SignedIn ? btnH * 2f + gap : 0f);
            float top = 214f;
            float height = top + lineH + gap + hintH + gap + statusH + (buttons > 0f ? gap + buttons : 0f) + pad;

            RectTransform panel = UiKit.BuildPopupFrame(view.Root, "Account", art, new Vector2(860f, height), onClose);
            float y = top;
            view.Line = UiKit.OnPanel(UiKit.AddText(panel, "Line", LineFor(st), 46, Color.white, TextAnchor.MiddleCenter, true));
            UiKit.Place((RectTransform)view.Line.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, lineH));
            y += lineH + gap;
            view.Hint = UiKit.OnPanel(UiKit.AddText(panel, "Hint", HintFor(st), 40, Color.white, TextAnchor.MiddleCenter));
            UiKit.Place((RectTransform)view.Hint.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, hintH));
            y += hintH + gap;
            view.Status = UiKit.OnPanel(UiKit.AddText(panel, "Status", status ?? "", 40, Color.white, TextAnchor.MiddleCenter));
            UiKit.Place((RectTransform)view.Status.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, statusH));
            y += statusH + gap;

            bool busy = false;
            view.SignIn = view.Sync = view.SignOut = null;
            if (st == State.Guest)
            {
                view.SignIn = LoginScreen.BuildGoogleButton(panel, SignInLabel, () =>
                {
                    if (busy) return;
                    busy = true;
                    UiKit.SetText(view.Status, "Signing in...");
                    RunPrimary(State.Guest, (ok, text) =>
                    {
                        busy = false;
                        if (ok && onChanged != null) onChanged();
                        if (view.Root == null) return;
                        Fill(view, art, onClose, onChanged, ok ? "Signed in." : text);
                    });
                });
                UiKit.Place((RectTransform)view.SignIn.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, googleH));
            }
            else if (st == State.SignedIn)
            {
                view.Sync = UiKit.AddButton(panel, "SyncNow", "Sync now", ToyKit.Button(1), Color.white, new Vector2(W, btnH), () =>
                {
                    if (busy) return;
                    busy = true;
                    UiKit.SetText(view.Status, "Syncing...");
                    AccountSync.SyncNow((ok, text) =>
                    {
                        busy = false;
                        if (view.Root == null) return;
                        UiKit.SetText(view.Status, text ?? "");
                        UiKit.SetText(view.Line, AccountSync.AccountLine);
                        if (onChanged != null) onChanged();
                    });
                }, 46);
                UiKit.Place((RectTransform)view.Sync.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, btnH));
                y += btnH + gap;
                view.SignOut = UiKit.AddButton(panel, "SignOut", "Sign out", ToyKit.Button(0), Color.white, new Vector2(W, btnH), () =>
                {
                    if (busy) return;
                    RunPrimary(State.SignedIn, (ok, text) =>
                    {
                        if (onChanged != null) onChanged();
                        if (view.Root != null) Fill(view, art, onClose, onChanged, text);
                    });
                }, 46);
                UiKit.Place((RectTransform)view.SignOut.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W, btnH));
            }
        }
    }
}
