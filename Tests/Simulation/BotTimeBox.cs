using System.Threading;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Runs LevelBot.Solve with a wall-clock limit so a search that never ends (seen on region 3 level 10 at the hardest scroll step, 2026-11-10)
    /// FAILS the test with a message instead of hanging the whole run. The search thread is a background thread and is abandoned on timeout.
    /// </summary>
    public static class BotTimeBox
    {
        public const int DefaultSeconds = 20;

        /// <summary>Returns false (and a default result) when the search did not finish within the limit.</summary>
        public static bool Solve(out BotResult outResult, SimConfig cfg, LevelSpec spec, LevelData data, int budget, int seconds = DefaultSeconds)
        {
            BotResult result = default(BotResult);
            Thread t = new Thread(() => { result = new LevelBot(new Sim(cfg, data, spec), spec, data, budget).Solve(); });
            t.IsBackground = true;
            t.Start();
            if (!t.Join(seconds * 1000)) { outResult = default(BotResult); return false; }
            outResult = result;
            return true;
        }
    }
}
