using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>Firestore needs UTC DateTimes; unset timestamps are this instant (never DateTime.MinValue with an unspecified kind).</summary>
    public static class Epoch { public static readonly DateTime Utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); }

    // Firestore documents (names = LEADERBOARD-DESIGN.md section 2). Times are UTC DateTime. The in-memory store uses the same classes.

    /// <summary>players/{uid}. displayName and avatarUrl are the only fields a client may write (firestore.rules); the rest is written by the service.</summary>
    [FirestoreData]
    public sealed class PlayerDoc
    {
        [FirestoreProperty("displayName")] public string DisplayName { get; set; }
        [FirestoreProperty("avatarUrl")] public string AvatarUrl { get; set; }
        [FirestoreProperty("nameLocked")] public bool NameLocked { get; set; }
        [FirestoreProperty("updatedAt")] public DateTime UpdatedAt { get; set; } = Epoch.Utc;
        /// <summary>Public id shown on boards: HMAC(salt, uid), so a board never exposes the Firebase uid.</summary>
        [FirestoreProperty("pid")] public string Pid { get; set; }
        /// <summary>heroId to the variant index first used for a leaderboard run. A hero keeps this variant (all variants are equal strength by construction); changed only if it left the pool.</summary>
        [FirestoreProperty("heroVariants")] public Dictionary<string, int> HeroVariants { get; set; }
        [FirestoreProperty("lastSubmitAt")] public DateTime LastSubmitAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("dayKey")] public string DayKey { get; set; }
        [FirestoreProperty("ticksToday")] public long TicksToday { get; set; }
        [FirestoreProperty("createdAt")] public DateTime CreatedAt { get; set; } = Epoch.Utc;
    }

    /// <summary>weeks/{weekKey}/slots/{uid}_{slotId}: the best crown of the week in one slot. A lower or equal run never changes it.</summary>
    [FirestoreData]
    public sealed class SlotDoc
    {
        [FirestoreProperty("uid")] public string Uid { get; set; }
        [FirestoreProperty("slotId")] public string SlotId { get; set; }
        [FirestoreProperty("board")] public string Board { get; set; }
        [FirestoreProperty("kind")] public string Kind { get; set; }
        [FirestoreProperty("levelId")] public string LevelId { get; set; }
        [FirestoreProperty("regionId")] public string RegionId { get; set; }
        [FirestoreProperty("mode")] public int Mode { get; set; }
        [FirestoreProperty("crown")] public long Crown { get; set; }
        [FirestoreProperty("floors")] public int Floors { get; set; }
        [FirestoreProperty("ticks")] public int Ticks { get; set; }
        [FirestoreProperty("score")] public int Score { get; set; }
        [FirestoreProperty("revives")] public int Revives { get; set; }
        [FirestoreProperty("heroId")] public string HeroId { get; set; }
        [FirestoreProperty("variant")] public int Variant { get; set; }
        [FirestoreProperty("seed")] public int Seed { get; set; }
        [FirestoreProperty("simVersion")] public int SimVersion { get; set; }
        [FirestoreProperty("gameVersion")] public string GameVersion { get; set; }
        [FirestoreProperty("runId")] public string RunId { get; set; }
        [FirestoreProperty("at")] public DateTime At { get; set; } = Epoch.Utc;
        [FirestoreProperty("expireAt")] public DateTime ExpireAt { get; set; } = Epoch.Utc;
    }

    /// <summary>weeks/{weekKey}/totals/{uid}: the three weekly totals. overall = levels + endless. The *At fields are when the total last increased (tie-break: earlier wins).</summary>
    [FirestoreData]
    public sealed class TotalsDoc
    {
        [FirestoreProperty("uid")] public string Uid { get; set; }
        [FirestoreProperty("pid")] public string Pid { get; set; }
        [FirestoreProperty("name")] public string Name { get; set; }
        [FirestoreProperty("avatar")] public string Avatar { get; set; }
        [FirestoreProperty("overall")] public long Overall { get; set; }
        [FirestoreProperty("overallAt")] public DateTime OverallAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("levels")] public long Levels { get; set; }
        [FirestoreProperty("levelsAt")] public DateTime LevelsAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("endless")] public long Endless { get; set; }
        [FirestoreProperty("endlessAt")] public DateTime EndlessAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("slotCount")] public int SlotCount { get; set; }
        [FirestoreProperty("updatedAt")] public DateTime UpdatedAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("expireAt")] public DateTime ExpireAt { get; set; } = Epoch.Utc;

        public long Get(string board) { return board == Boards.Overall ? Overall : board == Boards.Levels ? Levels : Endless; }
        public DateTime At(string board) { return board == Boards.Overall ? OverallAt : board == Boards.Levels ? LevelsAt : EndlessAt; }
    }

    [FirestoreData]
    public sealed class BoardEntry
    {
        [FirestoreProperty("rank")] public int Rank { get; set; }
        [FirestoreProperty("pid")] public string Pid { get; set; }
        [FirestoreProperty("name")] public string Name { get; set; }
        [FirestoreProperty("avatar")] public string Avatar { get; set; }
        [FirestoreProperty("crowns")] public long Crowns { get; set; }
    }

    /// <summary>weeks/{weekKey}/boards/{board}: the materialised top 100 clients read (1 document read per board view). final = the week is closed, the ranks and the limits used are frozen.</summary>
    [FirestoreData]
    public sealed class BoardDoc
    {
        [FirestoreProperty("board")] public string Board { get; set; }
        [FirestoreProperty("weekKey")] public string WeekKey { get; set; }
        [FirestoreProperty("entries")] public List<BoardEntry> Entries { get; set; }
        /// <summary>Crowns of the last (100th) entry when the board is full, else 0: a total below it cannot enter, so it needs no rebuild.</summary>
        [FirestoreProperty("cutoff")] public long Cutoff { get; set; }
        [FirestoreProperty("rebuiltAt")] public DateTime RebuiltAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("final")] public bool Final { get; set; }
        [FirestoreProperty("limitUsed")] public long LimitUsed { get; set; }
        [FirestoreProperty("topThreeFactorPercent")] public int TopThreeFactorPercent { get; set; }
        [FirestoreProperty("expireAt")] public DateTime ExpireAt { get; set; } = Epoch.Utc;
    }

    /// <summary>prizeClaims/{weekKey}_{board}_{uid}: written only by the claim transaction. state "issued" until the client acknowledges it saved the grant.</summary>
    [FirestoreData]
    public sealed class ClaimDoc
    {
        [FirestoreProperty("uid")] public string Uid { get; set; }
        [FirestoreProperty("weekKey")] public string WeekKey { get; set; }
        [FirestoreProperty("board")] public string Board { get; set; }
        [FirestoreProperty("rank")] public int Rank { get; set; }
        [FirestoreProperty("crowns")] public long Crowns { get; set; }
        [FirestoreProperty("bundles")] public int Bundles { get; set; }
        [FirestoreProperty("goldChests")] public int GoldChests { get; set; }
        [FirestoreProperty("ironChests")] public int IronChests { get; set; }
        [FirestoreProperty("powerUps")] public int PowerUps { get; set; }
        [FirestoreProperty("coins")] public int Coins { get; set; }
        [FirestoreProperty("state")] public string State { get; set; }
        [FirestoreProperty("createdAt")] public DateTime CreatedAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("ackedAt")] public DateTime AckedAt { get; set; } = Epoch.Utc;
        [FirestoreProperty("expireAt")] public DateTime ExpireAt { get; set; } = Epoch.Utc;

        public PrizeItems Items { get { return new PrizeItems { Bundles = Bundles, GoldChests = GoldChests, IronChests = IronChests, PowerUps = PowerUps, Coins = Coins }; } }
    }
}
