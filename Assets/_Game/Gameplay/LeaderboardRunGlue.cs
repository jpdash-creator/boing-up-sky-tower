using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Glue between a finished run and the leaderboard hub: builds the upload from the FRESH <see cref="RunRecord"/> of the run (never the stored best, which drops long inputs) and the session's score,
    /// and hands it to <see cref="LeaderboardHub.Submit"/>. Revived runs count (the revive ticks travel in <c>ReviveAt</c>), so <see cref="RunRecord.Counts"/> is not used. A run without a ticket
    /// (offline at the start, signed out, no consent, feature off) is simply not uploaded. Nothing here runs per frame.
    /// </summary>
    public static class LeaderboardRunGlue
    {
        /// <summary>Set when a run that would have been uploaded ended while the player is signed in but has not answered the consent popup: the menu then shows it once.</summary>
        public static bool ConsentPending;

        /// <summary>The service kind of a run record.</summary>
        public static string KindOf(RunKind kind)
        {
            return kind == RunKind.Level ? "level" : kind == RunKind.TimeAttack ? "timeattack" : "endless";
        }

        /// <summary>Builds the queued upload; null when the run has no inputs to replay.</summary>
        public static QueuedRun Build(RunRecord rec, int score, StartTicket ticket, int heroVariant, long nowUnix)
        {
            if (rec == null || ticket == null || rec.Inputs == null || rec.Inputs.Length == 0) return null;
            string kind = KindOf(rec.Kind);
            return new QueuedRun
            {
                RunId = Guid.NewGuid().ToString("N"),
                Ticket = ticket.Ticket,
                Kind = kind,
                LevelId = kind == "level" ? rec.LevelId : null,
                RegionId = kind == "level" ? null : rec.RegionId,
                Mode = rec.Mode,
                Seed = ticket.Seed,
                HeroId = rec.HeroId ?? "",
                HeroVariant = heroVariant,
                SimVersion = LeaderboardConfig.SimVersion,
                GameVersion = rec.GameVersion ?? "",
                InputsB64 = Convert.ToBase64String(rec.Inputs),
                ReviveAt = rec.ReviveAt ?? new int[0],
                Floors = rec.Floors,
                Ticks = rec.Ticks,
                Score = score,
                Crown = CrownScore.FromScore(score, rec.Mode),
                ExpiresAtUnix = ticket.ExpiresAtUnix,
                QueuedAtUnix = nowUnix
            };
        }

        /// <summary>Queues the finished run when it has a ticket and the player takes part. A campaign run counts only when the level was completed, so the caller passes only completed levels.</summary>
        public static void Upload(RunRecord rec, int score, StartTicket ticket, ProgressService progress)
        {
            if (rec == null || progress == null) return;
            if (ticket == null)
            {
                // a run that could have been ranked but the consent is missing: ask once on the menu
                if (LeaderboardHub.Enabled && LeaderboardHub.IsGoogleSignedIn && !LeaderboardHub.HasConsent && LeaderboardHub.ShouldAskConsent) ConsentPending = true;
                return;
            }
            if (!LeaderboardHub.CanUpload) return;
            QueuedRun q = Build(rec, score, ticket, progress.HeroVariant(rec.HeroId), LeaderboardTime.ToUnix(LeaderboardHub.Clock()));
            if (q != null) LeaderboardHub.Submit(q);
        }
    }
}
