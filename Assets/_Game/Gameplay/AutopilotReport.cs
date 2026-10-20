using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SkyHopper.Simulation;
using UnityEngine;

namespace SkyHopper.Gameplay
{
    /// <summary>
    /// The contents of persistentDataPath/autoplay.json, written by the host (Tools/android_test/autoplay_r1.py):
    /// {"region":1,"levels":[1,2,...,15],"skill":0.5,"difficulty":0,"stopOnFail":false,"seeds":1}.
    /// Every field is optional.
    /// </summary>
    [Serializable]
    public sealed class AutoplayOptions
    {
        public int region = 1;
        /// <summary>Level numbers inside the region (1..15). Empty = all levels of the region.</summary>
        public int[] levels;
        /// <summary>0 perfect player, 1 human-like (reaction lag and timing jitter).</summary>
        public float skill = 0.5f;
        /// <summary>Difficulty setting index 0..4 (the scroll step table in GameSettings).</summary>
        public int difficulty;
        public bool stopOnFail;
        /// <summary>Runs per level, each with a different jitter seed.</summary>
        public int seeds = 1;
        public int seedBase = 1;
        /// <summary>Game speed 1..4 (Time.timeScale). 1 = real time like a player.</summary>
        public float speed = 1f;
        /// <summary>Game seconds after which a run counts as a timeout.</summary>
        public int maxSeconds = 300;
        /// <summary>Real seconds the death / clear is left on screen before the next level (time for the host screenshot).</summary>
        public float holdFail = 3f;
        public float holdClear = 1.5f;
        /// <summary>Planner look-ahead in landings.</summary>
        public int depth = 2;

        public static AutoplayOptions Parse(string json)
        {
            AutoplayOptions o = JsonUtility.FromJson<AutoplayOptions>(string.IsNullOrEmpty(json) ? "{}" : json);
            if (o == null) o = new AutoplayOptions();
            o.Normalize();
            return o;
        }

        public void Normalize()
        {
            if (region < 1) region = 1;
            if (levels == null || levels.Length == 0)
            {
                levels = new int[15];
                for (int i = 0; i < levels.Length; i++) levels[i] = i + 1;
            }
            skill = Mathf.Clamp01(skill);
            difficulty = Mathf.Clamp(difficulty, 0, GameSettings.DifficultyNames.Length - 1);
            if (seeds < 1) seeds = 1;
            speed = Mathf.Clamp(speed, 1f, 4f);
            if (maxSeconds < 20) maxSeconds = 20;
            if (holdFail < 0f) holdFail = 0f;
            if (holdClear < 0f) holdClear = 0f;
            if (depth < 1) depth = 1;
        }
    }

    /// <summary>What the suite records for one run of one level.</summary>
    public sealed class AutoResult
    {
        public const string Cleared = "cleared", Fell = "fell", Timeout = "timeout";

        public string RunId = "";
        public string Version = "";
        public string LevelId = "";
        public int LevelNo;
        public int Attempt = 1;
        public int Difficulty;
        public int StepPercent;
        public float Skill;
        public string Goal = "";
        public string Result = "";
        public int Floors;
        public int TargetRow;
        public float Seconds;
        public int Tick;
        public int DeathRow;
        public float X, Y;
        public int GroundId;
        public string GroundKind = "";
        public int[] NearIds = new int[0];
        public string NearKinds = "";
        public int ScrollPercent;
        public float KillGap;
        public int StackCount;
        public bool Dense;
        public int Hazards;
        public int Hits;
        public int Coins;
        public int Plans;
        public int Misses;
        public int PlanMaxMs;
    }

    /// <summary>Builds, formats and writes the autoplay records (logcat tag prefix "BTAUTO ", CSV in persistentDataPath).</summary>
    public static class AutopilotReport
    {
        public const string Prefix = "BTAUTO ";
        /// <summary>logcat cuts long lines; every emitted line (prefix included) stays under this.</summary>
        public const int MaxLine = 560;
        const int ChunkChars = 250;
        const int MaxNear = 12;
        public const string CsvFileName = "autoplay_results.csv";

        static int seq;

        // ------------------------------------------------------------------ capture

        /// <summary>Snapshot of a finished run (the state at the moment the run ended).</summary>
        public static AutoResult Capture(RunSession ses, string runId, string version, int attempt, float skill, int difficulty, string result, AutopilotBrain brain)
        {
            SimState s = ses.State;
            Sim sim = ses.Sim;
            AutoResult r = new AutoResult();
            r.RunId = runId;
            r.Version = version;
            r.LevelId = ses.Level != null ? ses.Level.Id : "endless";
            r.LevelNo = ses.Level != null ? ses.Level.Index : 0;
            r.Attempt = attempt;
            r.Difficulty = difficulty;
            r.StepPercent = GameSettings.DifficultyStepPercent[Mathf.Clamp(difficulty, 0, GameSettings.DifficultyStepPercent.Length - 1)];
            r.Skill = skill;
            r.Goal = ses.Spec.Goal.Type.ToString();
            r.Result = result;
            r.Floors = s.Row;
            r.TargetRow = ses.Spec.TargetRow();
            r.Tick = s.Tick;
            r.Seconds = s.Tick / (float)SimConfig.TicksPerSecond;
            r.DeathRow = s.Row;
            r.X = s.X.F();
            r.Y = s.Y.F();
            r.GroundId = s.GroundId;
            r.GroundKind = KindCode(sim.Platforms[s.GroundId].Kind);
            int[] ids = new int[MaxNear];
            int stored;
            r.StackCount = StackCounter.Count(sim, s.Tick, s.X, s.Y, StackCounter.Window, ids, out stored);
            r.Dense = StackCounter.IsDense(r.StackCount);
            r.NearIds = new int[stored];
            StringBuilder kinds = new StringBuilder();
            for (int i = 0; i < stored; i++)
            {
                r.NearIds[i] = ids[i];
                if (i > 0) kinds.Append(',');
                kinds.Append(KindCode(sim.Platforms[ids[i]].Kind));
            }
            r.NearKinds = kinds.ToString();
            r.ScrollPercent = ScrollPercentAt(sim, s);
            r.KillGap = (s.Y - s.KillY).F();
            r.Hazards = CountHazards(sim, s);
            r.Hits = s.Hits;
            r.Coins = s.Coins;
            if (brain != null)
            {
                r.Plans = brain.Plans;
                r.Misses = brain.Misses;
                r.PlanMaxMs = (int)Math.Round(brain.MaxPlanMs);
            }
            return r;
        }

        /// <summary>Scroll speed relative to the level's base speed: 100 + the step-ups after the first jump (+ the boss ramp), as Sim.Step computes it.</summary>
        public static int ScrollPercentAt(Sim sim, SimState s)
        {
            if (s.StartTick < 0) return 0;
            SimConfig cfg = sim.Cfg;
            int steps = (s.Tick - s.StartTick) / (cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
            int pct = sim.CampaignRamp ? 0 : steps * cfg.ScrollStepPercent;
            if (pct > cfg.ScrollStepCapPercent) pct = cfg.ScrollStepCapPercent;
            int total = 100 + pct;
            if (sim.CampaignRamp)
            {
                int ramp = cfg.RampPercentAt(s.Row);
                if (ramp > cfg.CampaignRampCapPercent) ramp = cfg.CampaignRampCapPercent;
                total = total * (100 + ramp) / 100;
            }
            return total;
        }

        static int CountHazards(Sim sim, SimState s)
        {
            int n = 0;
            for (int i = 0; i < sim.Hazards.Length; i++)
            {
                Fx hx, hy;
                sim.HazardPosition(i, s.Tick, out hx, out hy);
                if (Fx.Abs(hx - s.X) <= StackCounter.Window && Fx.Abs(hy - s.Y) <= StackCounter.Window) n++;
            }
            return n;
        }

        /// <summary>One letter per platform kind (N normal, M moving, C crumble, B bounce, V vanishing, S sticky, L slippery, P bounce-cap).</summary>
        public static string KindCode(PlatformKind k)
        {
            switch (k)
            {
                case PlatformKind.Normal: return "N";
                case PlatformKind.Moving: return "M";
                case PlatformKind.Crumble: return "C";
                case PlatformKind.Bounce: return "B";
                case PlatformKind.Vanishing: return "V";
                case PlatformKind.Sticky: return "S";
                case PlatformKind.Slippery: return "L";
                case PlatformKind.BounceCap: return "P";
                default: return "?";
            }
        }

        // ------------------------------------------------------------------ JSON

        static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        public static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        static void Str(StringBuilder sb, string key, string value) { sb.Append(",\"").Append(key).Append("\":\"").Append(Escape(value)).Append('"'); }
        static void Num(StringBuilder sb, string key, int value) { sb.Append(",\"").Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture)); }
        static void Num(StringBuilder sb, string key, float value) { sb.Append(",\"").Append(key).Append("\":").Append(F(value)); }
        static void Bool(StringBuilder sb, string key, bool value) { sb.Append(",\"").Append(key).Append("\":").Append(value ? "true" : "false"); }

        /// <summary>The per-level record as one JSON object (event "level").</summary>
        public static string ToJson(AutoResult r)
        {
            StringBuilder sb = new StringBuilder(480);
            sb.Append("{\"ev\":\"level\"");
            Str(sb, "run", r.RunId);
            Str(sb, "v", r.Version);
            Str(sb, "lvl", r.LevelId);
            Num(sb, "n", r.LevelNo);
            Num(sb, "att", r.Attempt);
            Num(sb, "diff", r.Difficulty);
            Num(sb, "step", r.StepPercent);
            Num(sb, "skill", r.Skill);
            Str(sb, "goal", r.Goal);
            Str(sb, "res", r.Result);
            Num(sb, "floors", r.Floors);
            Num(sb, "target", r.TargetRow);
            Num(sb, "sec", r.Seconds);
            Num(sb, "tick", r.Tick);
            Num(sb, "x", r.X);
            Num(sb, "y", r.Y);
            Num(sb, "gid", r.GroundId);
            Str(sb, "gk", r.GroundKind);
            sb.Append(",\"near\":[");
            for (int i = 0; i < r.NearIds.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(r.NearIds[i].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(']');
            Str(sb, "nk", r.NearKinds);
            Num(sb, "scr", r.ScrollPercent);
            Num(sb, "gap", r.KillGap);
            Num(sb, "stack", r.StackCount);
            Bool(sb, "dense", r.Dense);
            Num(sb, "haz", r.Hazards);
            Num(sb, "hits", r.Hits);
            Num(sb, "coins", r.Coins);
            Num(sb, "plans", r.Plans);
            Num(sb, "miss", r.Misses);
            Num(sb, "planMs", r.PlanMaxMs);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Splits a record into logcat lines: one "BTAUTO {json}" line when short enough, else "BTAUTO {chunk,of,seq,d}" lines the host joins.</summary>
        public static void ToLogLines(string json, List<string> lines)
        {
            if (Prefix.Length + json.Length <= MaxLine)
            {
                lines.Add(Prefix + json);
                return;
            }
            int id = ++seq;
            int parts = (json.Length + ChunkChars - 1) / ChunkChars;
            for (int i = 0; i < parts; i++)
            {
                int from = i * ChunkChars;
                int len = Math.Min(ChunkChars, json.Length - from);
                lines.Add(Prefix + "{\"chunk\":" + i + ",\"of\":" + parts + ",\"seq\":" + id + ",\"d\":\"" + Escape(json.Substring(from, len)) + "\"}");
            }
        }

        /// <summary>Writes a record to logcat (Debug.Log), chunked as needed.</summary>
        public static void Emit(string json)
        {
            List<string> lines = new List<string>(4);
            ToLogLines(json, lines);
            for (int i = 0; i < lines.Count; i++) Debug.Log(lines[i]);
        }

        // ------------------------------------------------------------------ CSV

        public static readonly string[] CsvColumns =
        {
            "run", "version", "level", "n", "attempt", "difficulty", "stepPercent", "skill", "goal", "result", "floors", "targetRow", "seconds", "tick",
            "x", "y", "groundId", "groundKind", "nearIds", "nearKinds", "scrollPercent", "killGap", "stackCount", "dense", "hazards", "hits", "coins",
            "plans", "misses", "planMaxMs"
        };

        public static string CsvHeader() { return string.Join(",", CsvColumns); }

        static string Csv(string field)
        {
            if (field == null) return "";
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>One CSV row, same column order as CsvColumns. The near ids are joined with '|'.</summary>
        public static string ToCsvRow(AutoResult r)
        {
            string[] cells =
            {
                r.RunId, r.Version, r.LevelId, r.LevelNo.ToString(CultureInfo.InvariantCulture), r.Attempt.ToString(CultureInfo.InvariantCulture),
                r.Difficulty.ToString(CultureInfo.InvariantCulture), r.StepPercent.ToString(CultureInfo.InvariantCulture), F(r.Skill), r.Goal, r.Result,
                r.Floors.ToString(CultureInfo.InvariantCulture), r.TargetRow.ToString(CultureInfo.InvariantCulture), F(r.Seconds), r.Tick.ToString(CultureInfo.InvariantCulture),
                F(r.X), F(r.Y), r.GroundId.ToString(CultureInfo.InvariantCulture), r.GroundKind, JoinIds(r.NearIds), r.NearKinds.Replace(',', '|'),
                r.ScrollPercent.ToString(CultureInfo.InvariantCulture), F(r.KillGap), r.StackCount.ToString(CultureInfo.InvariantCulture), r.Dense ? "1" : "0",
                r.Hazards.ToString(CultureInfo.InvariantCulture), r.Hits.ToString(CultureInfo.InvariantCulture), r.Coins.ToString(CultureInfo.InvariantCulture),
                r.Plans.ToString(CultureInfo.InvariantCulture), r.Misses.ToString(CultureInfo.InvariantCulture), r.PlanMaxMs.ToString(CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < cells.Length; i++) cells[i] = Csv(cells[i]);
            return string.Join(",", cells);
        }

        static string JoinIds(int[] ids)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(ids[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Appends a row to the CSV (the header first when the file is new). Never throws: a full disk must not stop the run.</summary>
        public static void AppendCsv(string path, AutoResult r)
        {
            try
            {
                bool fresh = !File.Exists(path);
                File.AppendAllText(path, (fresh ? CsvHeader() + "\n" : "") + ToCsvRow(r) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("autoplay csv: " + e.Message);
            }
        }

        public static string CsvPath { get { return Path.Combine(Application.persistentDataPath, CsvFileName); } }
    }
}
