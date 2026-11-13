using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The three weekly boards (ids as the service names them).</summary>
    public static class LbBoards
    {
        public const string Overall = "overall", Levels = "levels", Endless = "endless";
        public static readonly string[] All = { Overall, Levels, Endless };
        public static readonly string[] Titles = { "Overall", "Regions", "Endless" };

        public static int IndexOf(string board)
        {
            for (int i = 0; i < All.Length; i++) if (All[i] == board) return i;
            return -1;
        }

        public static string TitleOf(string board)
        {
            int i = IndexOf(board);
            return i < 0 ? "Board" : Titles[i];
        }
    }

    /// <summary>Item counts of a weekly prize (what the service sends).</summary>
    public sealed class PrizeItemsDto
    {
        public int Bundles, GoldChests, IronChests, PowerUps, Coins;
        public bool IsEmpty { get { return Bundles + GoldChests + IronChests + PowerUps + Coins == 0; } }

        /// <summary>"1 Bundle, 1 Gold chest, 3 power-ups, 3,000 coins" (only the non-zero parts).</summary>
        public string Describe()
        {
            var parts = new List<string>(5);
            if (Bundles > 0) parts.Add(Bundles + (Bundles == 1 ? " Bundle" : " Bundles"));
            if (GoldChests > 0) parts.Add(GoldChests + (GoldChests == 1 ? " Gold chest" : " Gold chests"));
            if (IronChests > 0) parts.Add(IronChests + (IronChests == 1 ? " Iron chest" : " Iron chests"));
            if (PowerUps > 0) parts.Add(PowerUps + (PowerUps == 1 ? " power-up" : " power-ups"));
            if (Coins > 0) parts.Add(Coins.ToString("N0", CultureInfo.InvariantCulture) + " coins");
            return string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>The prize table of the owner decision (display copy of the service rules; the service decides what is really paid).</summary>
    public static class LbPrizeTable
    {
        public const int PaidRanks = 10;

        public static PrizeItemsDto For(string board, int rank)
        {
            if (rank < 1 || rank > PaidRanks) return new PrizeItemsDto();
            if (board == LbBoards.Overall)
            {
                if (rank == 1) return new PrizeItemsDto { Bundles = 1, GoldChests = 1 };
                if (rank == 2) return new PrizeItemsDto { Bundles = 1, IronChests = 1 };
                if (rank == 3) return new PrizeItemsDto { GoldChests = 1 };
                return new PrizeItemsDto { IronChests = 1, PowerUps = 2 };
            }
            if (rank == 1) return new PrizeItemsDto { GoldChests = 1, PowerUps = 3 };
            if (rank == 2) return new PrizeItemsDto { GoldChests = 1 };
            if (rank == 3) return new PrizeItemsDto { IronChests = 1, PowerUps = 2 };
            return new PrizeItemsDto { Coins = 3000, PowerUps = 1 };
        }

        /// <summary>The prize table rows of a board: ranks 1, 2, 3 and the shared "4 to 10" row (label, prize text).</summary>
        public static string[,] Rows(string board)
        {
            var rows = new string[4, 2];
            rows[0, 0] = "1st"; rows[0, 1] = For(board, 1).Describe();
            rows[1, 0] = "2nd"; rows[1, 1] = For(board, 2).Describe();
            rows[2, 0] = "3rd"; rows[2, 1] = For(board, 3).Describe();
            rows[3, 0] = "4th to 10th"; rows[3, 1] = For(board, 4).Describe();
            return rows;
        }
    }

    /// <summary>One line of a board: rank, public id, name, avatar URL, crowns.</summary>
    public sealed class BoardRow
    {
        public int Rank;
        public string Pid = "", Name = "", Avatar = "";
        public long Crowns;
    }

    public sealed class BoardData
    {
        public string WeekKey = "", Board = "";
        public List<BoardRow> Entries = new List<BoardRow>();
        public bool Final;
    }

    /// <summary>The player's own standing on one board (from GET /my-week).</summary>
    public sealed class BoardRank
    {
        public string Board = "";
        public long Crowns, EntryLimit;
        /// <summary>1-based rank, 0 = not ranked yet (no crowns this week).</summary>
        public int Rank;
        public bool ReachedLimit;
    }

    /// <summary>A finished week in which the player has a paid rank (from GET /my-week?prizes=1). <see cref="State"/> is open, issued or granted.</summary>
    public sealed class PendingPrize
    {
        public string WeekKey = "", Board = "", State = "open";
        public int Rank;
        public long Crowns;
        public PrizeItemsDto Prize = new PrizeItemsDto();
        public bool Claimable { get { return State != "granted" && !Prize.IsEmpty; } }
        /// <summary>The claim id the service uses (weekKey_board_uid) is not known here; the device keys idempotence on the id the claim answer returns.</summary>
        public string Key { get { return WeekKey + "_" + Board; } }
    }

    public sealed class MyWeekData
    {
        public string WeekKey = "", Pid = "";
        public long WeekEndsAtUnix;
        public long[] Totals = new long[3];
        public BoardRank[] Ranks = new BoardRank[3];
        public List<PendingPrize> Pending = new List<PendingPrize>();
        /// <summary>The answer carried the prize list (GET /my-week?prizes=1), so an empty <see cref="Pending"/> really means "nothing to claim".</summary>
        public bool PrizesKnown;

        public MyWeekData()
        {
            for (int i = 0; i < 3; i++) Ranks[i] = new BoardRank { Board = LbBoards.All[i] };
        }

        public BoardRank RankOf(string board)
        {
            int i = LbBoards.IndexOf(board);
            return i < 0 ? null : Ranks[i];
        }
    }

    /// <summary>The answer of POST /start-run.</summary>
    public sealed class StartTicket
    {
        public string Ticket = "";
        public int Seed;
        public long ExpiresAtUnix;
        public string Kind = "", Target = "";
        public int Mode;
    }

    /// <summary>The answer of POST /claim-prize.</summary>
    public sealed class ClaimData
    {
        public string ClaimId = "", State = "";
        public bool Grant;
        public PrizeItemsDto Prize = new PrizeItemsDto();
    }

    /// <summary>JSON helpers: parse the service answers and the Firestore REST board document. All tolerant: a missing field is a default, never an exception.</summary>
    public static class LbJson
    {
        static readonly JsonSerializerSettings NoDates = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None, NullValueHandling = NullValueHandling.Ignore };

        public static JObject Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<JObject>(json, NoDates); }
            catch (Exception) { return null; }
        }

        public static string Str(JToken t, string def = "")
        {
            if (t == null || t.Type == JTokenType.Null) return def;
            return t.Type == JTokenType.String ? (string)t : Convert.ToString(t, CultureInfo.InvariantCulture);
        }

        public static long Long(JToken t, long def = 0)
        {
            if (t == null || t.Type == JTokenType.Null) return def;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return (long)t;
            long v;
            return long.TryParse(Str(t), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        public static bool Bool(JToken t) { return t != null && t.Type == JTokenType.Boolean && (bool)t; }

        public static PrizeItemsDto ParsePrize(JToken p)
        {
            var d = new PrizeItemsDto();
            JObject o = p as JObject;
            if (o == null) return d;
            d.Bundles = (int)Long(o["bundles"]);
            d.GoldChests = (int)Long(o["goldChests"]);
            d.IronChests = (int)Long(o["ironChests"]);
            d.PowerUps = (int)Long(o["powerUps"]);
            d.Coins = (int)Long(o["coins"]);
            return d;
        }

        public static StartTicket ParseStartRun(string json)
        {
            JObject o = Parse(json);
            if (o == null || o["ticket"] == null) return null;
            var t = new StartTicket { Ticket = Str(o["ticket"]), Seed = (int)Long(o["seed"]) };
            DateTime exp;
            if (LeaderboardTime.TryParseUtc(Str(o["expiresAt"]), out exp)) t.ExpiresAtUnix = LeaderboardTime.ToUnix(exp);
            return t.Ticket.Length == 0 || t.Seed == 0 ? null : t;
        }

        public static MyWeekData ParseMyWeek(string json)
        {
            JObject o = Parse(json);
            if (o == null || o["weekKey"] == null) return null;
            var d = new MyWeekData { WeekKey = Str(o["weekKey"]), Pid = Str(o["pid"]) };
            DateTime end;
            if (LeaderboardTime.TryParseUtc(Str(o["weekEndsAt"]), out end)) d.WeekEndsAtUnix = LeaderboardTime.ToUnix(end);
            JObject totals = o["totals"] as JObject;
            if (totals != null)
            {
                d.Totals[0] = Long(totals["overall"]);
                d.Totals[1] = Long(totals["levels"]);
                d.Totals[2] = Long(totals["endless"]);
            }
            JArray ranks = o["ranks"] as JArray;
            if (ranks != null)
                foreach (JToken r in ranks)
                {
                    BoardRank br = d.RankOf(Str(r["board"]));
                    if (br == null) continue;
                    br.Crowns = Long(r["crowns"]);
                    br.Rank = (int)Long(r["rank"]);
                    br.EntryLimit = Long(r["entryLimit"]);
                    br.ReachedLimit = Bool(r["reachedLimit"]);
                }
            JArray pending = o["pendingPrizes"] as JArray;
            if (pending != null) d.PrizesKnown = true;
            if (pending != null)
                foreach (JToken p in pending)
                    d.Pending.Add(new PendingPrize
                    {
                        WeekKey = Str(p["weekKey"]), Board = Str(p["board"]), Rank = (int)Long(p["rank"]), Crowns = Long(p["crowns"]),
                        Prize = ParsePrize(p["prize"]), State = Str(p["state"], "open")
                    });
            return d;
        }

        public static ClaimData ParseClaim(string json)
        {
            JObject o = Parse(json);
            if (o == null) return null;
            return new ClaimData { ClaimId = Str(o["claimId"]), State = Str(o["state"]), Grant = Bool(o["grant"]), Prize = ParsePrize(o["prize"]) };
        }

        /// <summary>
        /// A board from the Firestore REST document <c>weeks/{weekKey}/boards/{board}</c> (typed values: <c>fields.entries.arrayValue.values[].mapValue.fields.rank.integerValue</c> ...).
        /// A document without entries (or a 404 body) is an empty board. Entries are sorted by rank and cut at 100.
        /// </summary>
        public static BoardData ParseFirestoreBoard(string json, string weekKey, string board)
        {
            var data = new BoardData { WeekKey = weekKey, Board = board };
            JObject doc = Parse(json);
            JObject fields = doc != null ? doc["fields"] as JObject : null;
            if (fields == null) return data;
            data.Final = Bool(fields["final"] != null ? fields["final"]["booleanValue"] : null);
            JArray values = fields["entries"] != null && fields["entries"]["arrayValue"] != null ? fields["entries"]["arrayValue"]["values"] as JArray : null;
            if (values == null) return data;
            foreach (JToken v in values)
            {
                JObject f = v["mapValue"] != null ? v["mapValue"]["fields"] as JObject : null;
                if (f == null) continue;
                data.Entries.Add(new BoardRow
                {
                    Rank = (int)Long(Typed(f["rank"])),
                    Pid = Str(Typed(f["pid"])),
                    Name = Str(Typed(f["name"])),
                    Avatar = Str(Typed(f["avatar"])),
                    Crowns = Long(Typed(f["crowns"]))
                });
            }
            data.Entries.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            if (data.Entries.Count > 100) data.Entries.RemoveRange(100, data.Entries.Count - 100);
            return data;
        }

        /// <summary>The value inside a Firestore typed value ({"integerValue":"5"}, {"stringValue":"x"}, ...); null when there is none.</summary>
        static JToken Typed(JToken typed)
        {
            JObject o = typed as JObject;
            if (o == null) return null;
            foreach (JProperty p in o.Properties()) return p.Value;
            return null;
        }

        /// <summary>Body of the Firestore REST commit that writes the display profile: displayName and avatarUrl plus <c>updatedAt</c> = the request time (the security rules demand exactly this).</summary>
        public static string BuildProfileCommit(string project, string uid, string displayName, string avatarUrl)
        {
            string doc = "projects/" + project + "/databases/(default)/documents/players/" + uid;
            var body = new JObject
            {
                ["writes"] = new JArray
                {
                    new JObject
                    {
                        ["update"] = new JObject
                        {
                            ["name"] = doc,
                            ["fields"] = new JObject
                            {
                                ["displayName"] = new JObject { ["stringValue"] = SafeName(displayName) },
                                ["avatarUrl"] = new JObject { ["stringValue"] = SafeAvatar(avatarUrl) }
                            }
                        },
                        ["updateMask"] = new JObject { ["fieldPaths"] = new JArray("displayName", "avatarUrl") },
                        ["updateTransforms"] = new JArray { new JObject { ["fieldPath"] = "updatedAt", ["setToServerValue"] = "REQUEST_TIME" } }
                    }
                }
            };
            return body.ToString(Formatting.None);
        }

        /// <summary>The rules accept 1 to 32 characters: trimmed, cut without splitting a surrogate pair, "Player" when empty.</summary>
        public static string SafeName(string name)
        {
            string n = (name ?? "").Trim();
            if (n.Length > 32)
            {
                int cut = 32;
                if (char.IsHighSurrogate(n[cut - 1])) cut--;
                n = n.Substring(0, cut).TrimEnd();
            }
            return n.Length == 0 ? "Player" : n;
        }

        /// <summary>The rules accept an empty string or an https URL of at most 512 characters.</summary>
        public static string SafeAvatar(string url)
        {
            string u = (url ?? "").Trim();
            if (u.Length == 0 || u.Length > 512) return "";
            return u.StartsWith("https://", StringComparison.Ordinal) ? u : "";
        }

        /// <summary>Serialises a queued run as the /submit-run body (camelCase; the level kind has no regionId and the other kinds no levelId).</summary>
        public static string BuildSubmitBody(FluffyClimbers.Economy.QueuedRun r)
        {
            var body = new JObject
            {
                ["simVersion"] = r.SimVersion,
                ["gameVersion"] = r.GameVersion ?? "",
                ["kind"] = r.Kind,
                ["mode"] = r.Mode,
                ["seed"] = r.Seed,
                ["heroId"] = r.HeroId ?? "",
                ["heroVariant"] = r.HeroVariant,
                ["inputsB64"] = r.InputsB64 ?? "",
                ["reviveAt"] = new JArray(r.ReviveAt ?? new int[0]),
                ["runId"] = r.RunId ?? "",
                ["ticket"] = r.Ticket ?? "",
                ["claimed"] = new JObject { ["floors"] = r.Floors, ["ticks"] = r.Ticks, ["score"] = r.Score, ["crown"] = r.Crown }
            };
            if (r.Kind == "level") body["levelId"] = r.LevelId; else body["regionId"] = r.RegionId;
            return body.ToString(Formatting.None);
        }
    }
}
