using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>Everything the store needs to apply one VERIFIED run (the crown comes from the replay, never from the client).</summary>
    public sealed class ApplyInput
    {
        public string Uid, Pid, WeekKey;
        public RunSubmission Run;
        public ReplayResult Replay;
        public DateTime Now;
        public Limits Limits;
    }

    public sealed class ApplyOutcome
    {
        public bool Accepted;
        /// <summary>rate_limited, variant_locked, implausible, ok.</summary>
        public string Error;
        public bool Improved;
        public long PreviousCrown, SlotCrown;
        public TotalsDoc Totals;
        /// <summary>Boards whose total grew in this call (the boards worth a materialised top-100 rebuild).</summary>
        public List<string> GrownBoards = new List<string>();
    }

    /// <summary>
    /// The pure write rules, shared by the Firestore store (inside a transaction) and the in-memory store (tests): a lower or equal run never changes a slot, a better run replaces it and the weekly totals grow by the
    /// difference only. overall = levels + endless always.
    /// </summary>
    public static class SlotMath
    {
        public static ApplyOutcome Apply(ApplyInput inp, SlotDoc slot, TotalsDoc totals, PlayerDoc player, out SlotDoc newSlot, out TotalsDoc newTotals, out PlayerDoc newPlayer)
        {
            newSlot = null; newTotals = null;
            Limits lim = inp.Limits;
            ReplayResult rr = inp.Replay;
            var outcome = new ApplyOutcome();

            newPlayer = player ?? new PlayerDoc { CreatedAt = inp.Now };
            if (string.IsNullOrEmpty(newPlayer.Pid)) newPlayer.Pid = inp.Pid;
            if (newPlayer.HeroVariants == null) newPlayer.HeroVariants = new Dictionary<string, int>();

            // ---- durable rate limits (the cheap in-memory bucket in front of the replay is in LeaderboardApi) ----
            if (newPlayer.LastSubmitAt > Epoch.Utc && (inp.Now - newPlayer.LastSubmitAt).TotalMilliseconds < lim.MinSubmitIntervalMs)
            { outcome.Error = "rate_limited"; return outcome; }
            string day = inp.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (newPlayer.DayKey != day) { newPlayer.DayKey = day; newPlayer.TicksToday = 0; }
            if (newPlayer.TicksToday + rr.TicksStepped > lim.MaxTicksPerDay) { outcome.Error = "rate_limited"; return outcome; }

            // ---- plausibility ----
            if (rr.Crown > lim.MaxCrownPerRun) { outcome.Error = "implausible"; return outcome; }

            // ---- hero variant lock: a hero keeps the variant it was first used with (all variants are equal strength by construction) ----
            int lockedVariant;
            if (newPlayer.HeroVariants.TryGetValue(inp.Run.HeroId, out lockedVariant) && lockedVariant != inp.Run.HeroVariant && HeroRolls.IsValidVariant(inp.Run.HeroId, lockedVariant)
                && HeroRolls.IsRollable(inp.Run.HeroId))
            { outcome.Error = "variant_locked"; return outcome; }
            newPlayer.HeroVariants[inp.Run.HeroId] = inp.Run.HeroVariant;

            newPlayer.LastSubmitAt = inp.Now;
            newPlayer.TicksToday += rr.TicksStepped;
            outcome.Accepted = true;
            outcome.PreviousCrown = slot == null ? 0 : slot.Crown;
            outcome.SlotCrown = outcome.PreviousCrown;
            outcome.Totals = totals;

            // ---- the slot: only a strictly better crown writes ----
            if (rr.Crown <= outcome.PreviousCrown) return outcome;

            DateTime expire = WeekMath.WeekEndUtc(inp.Now).AddDays(lim.RetentionDays);
            newSlot = new SlotDoc
            {
                Uid = inp.Uid, SlotId = rr.SlotId, Board = rr.Board, Kind = inp.Run.Kind, LevelId = inp.Run.LevelId, RegionId = inp.Run.RegionId, Mode = inp.Run.Mode,
                Crown = rr.Crown, Floors = rr.Floors, Ticks = rr.Ticks, Score = rr.Score, Revives = rr.Revives, HeroId = inp.Run.HeroId, Variant = inp.Run.HeroVariant,
                Seed = inp.Run.Seed, SimVersion = inp.Run.SimVersion, GameVersion = inp.Run.GameVersion, RunId = inp.Run.RunId, At = inp.Now, ExpireAt = expire
            };
            long delta = rr.Crown - outcome.PreviousCrown;

            newTotals = totals == null
                ? new TotalsDoc { Uid = inp.Uid, ExpireAt = expire }
                : totals;
            newTotals.Pid = newPlayer.Pid;
            newTotals.Name = newPlayer.DisplayName;
            newTotals.Avatar = newPlayer.AvatarUrl;
            if (slot == null) newTotals.SlotCount++;
            if (rr.Board == Boards.Levels) { newTotals.Levels += delta; newTotals.LevelsAt = inp.Now; }
            else { newTotals.Endless += delta; newTotals.EndlessAt = inp.Now; }
            newTotals.Overall += delta;
            newTotals.OverallAt = inp.Now;
            newTotals.UpdatedAt = inp.Now;

            outcome.Improved = true;
            outcome.SlotCrown = rr.Crown;
            outcome.Totals = newTotals;
            outcome.GrownBoards.Add(Boards.Overall);
            outcome.GrownBoards.Add(rr.Board);
            return outcome;
        }

        /// <summary>Sort order of every board: crowns descending, then the earlier time the total was reached, then pid (stable, never visible).</summary>
        public static int Compare(long crownsA, DateTime atA, string idA, long crownsB, DateTime atB, string idB)
        {
            if (crownsA != crownsB) return crownsA > crownsB ? -1 : 1;
            int c = atA.CompareTo(atB);
            if (c != 0) return c;
            return string.CompareOrdinal(idA, idB);
        }
    }
}
