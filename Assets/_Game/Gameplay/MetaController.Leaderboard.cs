using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using FluffyClimbers.Monetization;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Weekly leaderboard entry points of the Meta scene: the screen, the consent popup and the hub start (Planning/LEADERBOARD-DESIGN.md).</summary>
    public sealed partial class MetaController
    {
        LeaderboardScreen leaderboard;
        /// <summary>Where Back goes from the Leaderboard screen (World map, Level screen, otherwise home).</summary>
        Action leaderboardReturn;

        /// <summary>The Leaderboard screen while it is showing (null on every other screen). Tests drive it through this.</summary>
        public LeaderboardScreen CurrentLeaderboard { get { return CurrentScreen == LeaderboardScreen.ScreenName ? leaderboard : null; } }

        /// <summary>Starts the leaderboard hub once per meta scene: the remote config switch, the sign-in parts and the consent that may be pending after a ranked-looking run.</summary>
        void StartLeaderboard()
        {
            LeaderboardHub.AccountModeIsGoogle = () => AccountSync.IsGoogleMode;
            LeaderboardHub.Init(progress, repo != null ? repo.RemoteConfig : null, GameSettings.LeaderboardTest);
            LeaderboardHub.OnMenuStart();
            if (LeaderboardRunGlue.ConsentPending)
            {
                LeaderboardRunGlue.ConsentPending = false;
                if (LeaderboardHub.Enabled && LeaderboardHub.IsGoogleSignedIn && LeaderboardHub.ShouldAskConsent) ShowLeaderboardConsent();
            }
        }

        /// <summary>The weekly leaderboard (tab 0 Overall, 1 Levels, 2 Endless). Back returns to the screen that opened it (World map, Level screen, otherwise home).</summary>
        public void ShowLeaderboard(int board = 0)
        {
            if (CurrentScreen != LeaderboardScreen.ScreenName) leaderboardReturn = OriginReturnFor(CurrentScreen);
            if (leaderboard != null) leaderboard.Dispose();
            NewScreen(LeaderboardScreen.ScreenName);
            Background(ToyKit.Get("leaderboard_bg"), new Color(0.18f, 0.28f, 0.60f, 1f));   // the owner's picture, cover-fitted (uniform scale, cropped), never stretched
            AddBack("Back", () => { if (leaderboardReturn != null) leaderboardReturn(); else ShowMenu(); });
            var host = new LeaderboardScreen.Host
            {
                NewOverlay = NewOverlay,
                CloseOverlay = ClosePopup,
                ShowConsent = ShowLeaderboardConsent,
                SignIn = LeaderboardSignIn
            };
            leaderboard = new LeaderboardScreen(screen, progress, art, host);
            leaderboard.Build(board);
        }

        /// <summary>Google Play Games sign-in through the account flow (links the account like Settings does), then the Firebase sign-in of the leaderboard.</summary>
        void LeaderboardSignIn(Action<bool> done)
        {
            if (LeaderboardHub.IsGoogleSignedIn) { LeaderboardHub.SignIn(done); return; }
            AccountSync.SignInAndLink((ok, message) =>
            {
                if (!ok) { if (done != null) done(false); return; }
                LeaderboardHub.SignIn(done);
            });
        }

        /// <summary>The one-time consent popup. "Join leaderboards" stores the consent and (on the Leaderboard screen) rebuilds it; "Not now" stores nothing and asks again tomorrow at the earliest.</summary>
        public void ShowLeaderboardConsent()
        {
            ClosePopup();
            popup = LeaderboardConsentPopup.Build(canvas.transform, art,
                () => { LeaderboardHub.AcceptConsent(); ClosePopup(); RefreshLeaderboardScreen(); },
                () => { LeaderboardHub.DeclineConsent(); ClosePopup(); RefreshLeaderboardScreen(); },
                LegalLinks.OpenPrivacy);
        }

        void RefreshLeaderboardScreen()
        {
            if (CurrentScreen == LeaderboardScreen.ScreenName) ShowLeaderboard(leaderboard != null ? leaderboard.Board : 0);
        }
    }
}
