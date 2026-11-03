using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// One attempt at one level: owns the deterministic Sim and its state, advances it one tick at a time and decides when
    /// the run is over (owner decision: a run ends at goal completion, or when the player falls). Plain C#, no Unity types,
    /// so it is unit-testable and the same code a server could replay.
    /// </summary>
    public sealed class RunSession
    {
        /// <summary>Owner decision 2026-10-25 (doc 22, point 9): 5 gems for the first revive of a run, 10 for the second, never more than two. The first can be free through a rewarded ad.</summary>
        public const int FirstReviveGems = RevivePolicy.FirstGemCost, SecondReviveGems = RevivePolicy.LaterGemCost;
        /// <summary>Revive schedule of this run (owner 2026-11-02, new per run): ads 30 s then 60 s then none; gems 5 then 10 and stays 10. See <see cref="RevivePolicy"/>.</summary>
        public readonly RevivePolicy Revive = new RevivePolicy();
        public static int ReviveCostFor(int gemRevivesUsed) { return RevivePolicy.GemCostFor(gemRevivesUsed); }
        public int NextReviveCost { get { return Revive.NextGemCost; } }
        /// <summary>True once an ad revive was taken in this run.</summary>
        public bool AdReviveUsed { get { return Revive.AdsUsed > 0; } }
        /// <summary>An ad was watched to the end but the revive did not happen yet; the next try revives for free without another ad.</summary>
        public bool AdRewardPending { get; private set; }
        /// <summary>The ad option is offered while the run has an ad left (2 per run).</summary>
        public bool AdReviveOffered { get { return CanRevive && Revive.AdAvailable; } }
        /// <summary>Gems still missing for the next gem revive (0 when affordable).</summary>
        public int ReviveShortfall(ProgressService progress) { return progress == null ? NextReviveCost : System.Math.Max(0, NextReviveCost - progress.Gems); }
        /// <summary>Endless runs have no revive: the result (best floor, coins) is saved the moment the player falls (CUX-8), so a revive would double pay.</summary>
        public bool CanRevive { get { return !IsEndless && !GoalLost; } }

        public readonly SimConfig Cfg;
        /// <summary>The campaign level, or null in endless mode.</summary>
        public readonly LevelDef Level;
        public readonly RegionDef Region;
        public readonly LevelSpec Spec;
        /// <summary>Current world. In endless mode the tower grows in chunks: Data and Sim are replaced (same prefix) when WorldVersion changes.</summary>
        public LevelData Data { get; private set; }
        public Sim Sim { get; private set; }
        public SimState State;

        /// <summary>Endless mode (owner decision 2026-10-20): an ever growing tower of the region, the run ends only when the player falls.</summary>
        public readonly bool IsEndless;
        /// <summary>Seed of the endless tower; stored with the run so it can be rebuilt (replay, server check).</summary>
        public readonly int Seed;
        /// <summary>Campaign: the attempt seed of this run's layout (0 = base layout). Endless: always 0 (its tower seed is <see cref="Seed"/>).</summary>
        public readonly int AttemptSeed;
        /// <summary>One-line description of the layout for logs and bug reports: level, attempt seed and the resulting layout seed.</summary>
        public string LayoutInfo
        {
            get { return IsEndless ? "endless " + Region.Id + " seed " + Seed : Level.Id + " attempt " + AttemptSeed + " layoutSeed " + Spec.Seed; }
        }
        /// <summary>Endless tower, null for campaign levels.</summary>
        public readonly EndlessTower Tower;
        /// <summary>Changes every time the endless tower grew (the view then extends its world). Always 0 for campaign levels.</summary>
        public int WorldVersion { get; private set; }

        public bool GoalReached { get; private set; }
        /// <summary>The goal became impossible while alive (NoWallBounce after a wall bounce): the run ends, a revive cannot undo it.</summary>
        public bool GoalLost { get; private set; }
        public bool Failed { get { return (State.Dead || GoalLost) && !GoalReached; } }
        public bool Over { get { return GoalReached || State.Dead || GoalLost; } }

        /// <summary>
        /// Campaign attempt. <paramref name="attemptSeed"/> picks the layout: 0 = the level's base layout (tests), any other value a different layout of the
        /// same difficulty. The same (level, attemptSeed) always rebuilds the same layout, so a run can be reproduced from <see cref="LayoutInfo"/>.
        /// </summary>
        public RunSession(ContentRepository repo, SimConfig cfg, string levelId, bool stripGems = false, int attemptSeed = 0)
        {
            Cfg = cfg;
            Level = repo.GetLevel(levelId);
            Region = repo.GetRegion(Level.Region);
            AttemptSeed = attemptSeed;
            Spec = LevelSpecFactory.Build(Level, Region, attemptSeed);
            Data = LevelBuilder.Build(cfg, Spec);
            if (stripGems) StripGems(Data);   // owner 2026-10-24: a level that was cleared before shows no gems again
            Sim = new Sim(cfg, Data, Spec);
            State = Sim.NewState();
        }

        /// <summary>Removes the gem pickups (the hidden gem of a FindHiddenGem goal stays: it is the goal, and pays nothing on a replay).</summary>
        static void StripGems(LevelData data)
        {
            var keep = new System.Collections.Generic.List<Pickup>(data.Pickups.Length);
            for (int i = 0; i < data.Pickups.Length; i++) if (data.Pickups[i].Kind != PickupKind.Gem) keep.Add(data.Pickups[i]);
            data.Pickups = keep.ToArray();
        }

        /// <summary>Endless run in a region. <paramref name="seed"/> picks the tower; the same seed always gives the same tower.</summary>
        public RunSession(ContentRepository repo, SimConfig cfg, RegionDef region, int seed)
        {
            Cfg = cfg;
            Level = null;
            Region = region;
            IsEndless = true;
            Seed = seed;
            Spec = LevelSpecFactory.BuildEndless(region, seed, repo.RemoteConfig.Generator.EndlessDifficultyFloors);
            Tower = new EndlessTower(cfg, Spec);
            Data = Tower.Data;
            Sim = Tower.CreateSim();
            State = Sim.NewState();
            WorldVersion = Tower.Version;
        }

        /// <summary>Advances one 1/60 s tick. Does nothing once the run is over.</summary>
        public void Step(int input)
        {
            if (Over) return;
            if ((input & SimInput.PowerMask) != 0) input = TakePowerUnits(input);
            if ((input & SimInput.HeroMask) != 0) input = TakeHeroPowers(input);
            Sim.Step(ref State, input);
            if (Hero != null) Hero.Tick();   // hero cooldowns run in simulation ticks
            if (State.Combo >= 3 && lastCombo < 3) comboChains++;   // reward statistics: a chain reached 3 landings
            lastCombo = State.Combo;
            if (State.BestCombo > bestComboSeen) bestComboSeen = State.BestCombo;
            if (IsEndless)
            {
                // Keep the tower 60 rows above the player. Growth happens once per 40 rows (about every 15 s), never per frame.
                if (Tower.EnsureAhead(State.Row))
                {
                    Data = Tower.Data;
                    Sim = Tower.CreateSim();
                    Sim.GrowState(ref State);
                    WorldVersion = Tower.Version;
                }
                return;
            }
            if (GoalEvaluator.IsComplete(Spec, State)) GoalReached = true;
            else if (GoalEvaluator.IsFailed(Spec, State)) GoalLost = true;
        }

        /// <summary>Pays the revive price from the wallet and puts the player back on the highest landed platform.</summary>
        public bool TryRevive(ProgressService progress)
        {
            if (progress == null || !State.Dead || GoalReached || !CanRevive) return false;
            int cost = NextReviveCost;
            if (progress.Gems < cost) return false;
            Sim.Revive(ref State);
            // The wallet is charged only after the revive happened. The run's own gems stay in the state (a revive keeps them).
            if (!progress.TrySpendGems(cost)) { State.RevivesUsed--; return false; }
            Revive.RecordGem();
            return true;
        }

        /// <summary>The rewarded ad was watched to the end: remember it so the free revive can be granted again if it did not go through.</summary>
        public void MarkAdWatched()
        {
            if (AdReviveOffered) AdRewardPending = true;
        }

        /// <summary>Free revive after a completed rewarded ad (first revive only, once per run). Costs nothing; keeps gems and effects.</summary>
        public bool TryReviveFree()
        {
            if (!AdRewardPending || !State.Dead || GoalReached || !AdReviveOffered) return false;
            Sim.Revive(ref State);
            AdRewardPending = false;
            Revive.RecordAd();
            return true;
        }

        // ---- power-ups (docs/15, reworked 2026-10-31): every owned type is available (no loadout, no carry cap, no stock cap).
        // The owned units are counted here, outside the simulation state; a tap whose type has no unit left is dropped before it reaches the
        // simulation, every accepted tap takes one unit. Used units are taken from the saved stock when the run pauses or ends. ----

        readonly int[] stock = new int[PowerUps.MaxType + 1];
        readonly int[] settledStock = new int[PowerUps.MaxType + 1];
        readonly bool[] ownedAtStart = new bool[PowerUps.MaxType + 1];

        /// <summary>Puts the owned units into the fresh run (units[i] for PowerUp id i, from ProgressService.BuildRunStock).</summary>
        public void ApplyStock(int[] units)
        {
            if (units == null) return;
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                int n = i < units.Length && PowerUps.IsImplemented((PowerUp)i) ? System.Math.Max(0, units[i]) : 0;
                stock[i] = n;
                settledStock[i] = n;
                ownedAtStart[i] = n > 0;
            }
        }

        /// <summary>Units of the type still owned in this run.</summary>
        public int PowerStock(PowerUp type)
        {
            int i = (int)type;
            return i > 0 && i < stock.Length ? stock[i] : 0;
        }

        /// <summary>True when the player owned this type when the run started (the HUD shows one button per owned type and keeps it while the run lasts).</summary>
        public bool OwnedAtStart(PowerUp type)
        {
            int i = (int)type;
            return i > 0 && i < ownedAtStart.Length && ownedAtStart[i];
        }

        /// <summary>True when the player owned at least one power-up when the run started.</summary>
        public bool HasPowerUps
        {
            get
            {
                for (int i = 1; i <= PowerUps.MaxType; i++) if (ownedAtStart[i]) return true;
                return false;
            }
        }

        /// <summary>
        /// Drops the activation bits of types with no unit left (or that cannot start now) and takes one unit for every accepted bit.
        /// </summary>
        int TakePowerUnits(int input)
        {
            int result = input & ~SimInput.PowerMask;
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp p = (PowerUp)i;
                int bit = PowerUps.InputBit(p);
                if ((input & bit) == 0 || stock[i] <= 0 || !PowerUps.CanActivate(in State, p)) continue;
                // Flight and Rocket exclude each other inside one tick as well (the lower id wins, like the simulation)
                if (p == PowerUp.Rocket && (result & PowerUps.InputBit(PowerUp.Flight)) != 0) continue;
                stock[i]--;
                powerUpsUsed++;
                result |= bit;
            }
            return result;
        }

        // ---- hero powers (owner 2026-11-02, docs/34): the chosen hero's Powers are free buttons, READY at the start of a run, each with its own cooldown.
        // A use is a normal power-up activation (same effect, SimConfig.HeroPowerDurationFactor = 0.6 of the duration) that takes NO inventory unit. ----

        /// <summary>The hero's powers and their cooldowns, or null when the hero has none (Common heroes).</summary>
        public HeroPowerCooldown Hero { get; private set; }

        /// <summary>Gives the run the powers of the chosen hero. Null or a hero without powers = no hero buttons. Call before the HUD is built.</summary>
        public void ApplyHeroPowers(CharacterSpec hero)
        {
            Hero = null;
            if (hero == null || hero.Powers == null || hero.Powers.Length == 0) return;
            int r = (int)hero.Rarity;
            var h = new HeroPowerCooldown(hero.Powers, r >= 0 && r < CharacterDefs.CooldownSeconds.Length ? CharacterDefs.CooldownSeconds[r] : 0);
            if (h.Count > 0) Hero = h;
        }

        /// <summary>
        /// Keeps the hero activation bits that may start now (the hero has the power, its cooldown is over, the effect is not running and can start) and starts
        /// the cooldown of each accepted one. Every other hero bit is dropped, so nothing outside the hero's own powers ever reaches the simulation for free.
        /// </summary>
        int TakeHeroPowers(int input)
        {
            int result = input & ~SimInput.HeroMask;
            if (Hero == null) return result;
            for (int k = 0; k < Hero.Count; k++)
            {
                PowerUp p = Hero.Type(k);
                int bit = PowerUps.HeroInputBit(p);
                if ((input & bit) == 0 || !Hero.IsReady(k) || !PowerUps.CanActivateHero(in State, p)) continue;
                if ((result & PowerUps.InputBit(p)) != 0) continue;   // a bought tap of the same type in this tick runs first (it would make this one a no-op)
                // Flight and Rocket exclude each other inside one tick as well (the lower id wins, like the simulation)
                if (p == PowerUp.Rocket && ((result & (PowerUps.InputBit(PowerUp.Flight) | PowerUps.HeroInputBit(PowerUp.Flight))) != 0)) continue;
                if (p == PowerUp.Flight && (result & PowerUps.InputBit(PowerUp.Rocket)) != 0) continue;
                Hero.TryUse(k);
                result |= bit;
            }
            return result;
        }

        /// <summary>
        /// Takes the units used since the last call out of the owned stock and saves (used units are gone, also on a failed run; unused ones
        /// stay in the stock). Called when the run pauses, ends or is left, never per tick (a save is a file write).
        /// </summary>
        public int SettlePowerUps(ProgressService progress)
        {
            int used = 0;
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                if (stock[i] >= settledStock[i]) continue;
                int n = settledStock[i] - stock[i];
                settledStock[i] = stock[i];
                if (progress != null) progress.TryConsumePowerUp(PowerUps.IdOf((PowerUp)i), n);
                used += n;
            }
            return used;
        }

        public RunOutcome BuildOutcome()
        {
            return new RunOutcome
            {
                Completed = GoalReached,
                StarMask = GoalReached ? StarEvaluator.Earned(Spec, State, State.RevivesUsed, State.Tick) : 0,
                Score = State.Score,
                CoinsCollected = State.Coins,
                GemsCollected = State.Gems,
                HiddenGems = State.HiddenGems,
                GemPickupsTotal = CountGemPickups(false),
                GemPickupsTaken = CountGemPickups(true),
                ComboChains = comboChains - reportedChains,
                BestCombo = bestComboSeen,
                PowerUpsUsed = powerUpsUsed - reportedPowerUps
            };
        }

        // ---- reward statistics (docs/26 section 4): combo chains reaching 3 landings, best combo, power-up units used ----

        int lastCombo, comboChains, bestComboSeen, powerUpsUsed, reportedChains, reportedPowerUps;

        /// <summary>Marks the combo chains and power-up units counted so far as reported, so the next outcome only carries new ones (a revive keeps the session).</summary>
        public void MarkStatsReported()
        {
            reportedChains = comboChains;
            reportedPowerUps = powerUpsUsed;
        }

        /// <summary>Gem pickups the level holds (or, with <paramref name="takenOnly"/>, the ones collected). Called at the end of a run only.</summary>
        int CountGemPickups(bool takenOnly)
        {
            int n = 0;
            if (Data == null || Data.Pickups == null) return 0;
            for (int i = 0; i < Data.Pickups.Length; i++)
            {
                if (Data.Pickups[i].Kind != PickupKind.Gem) continue;
                if (takenOnly && (State.PickupTaken == null || i >= State.PickupTaken.Length || !State.PickupTaken[i])) continue;
                n++;
            }
            return n;
        }

        /// <summary>The extra rule line shown under the goal before the level starts ("Don't touch the walls."), or null when the level has no such rule.</summary>
        public static string GoalRule(LevelSpec spec)
        {
            return !spec.Endless && spec.Goal.Type == GoalType.NoWallBounce ? "Don't touch the walls." : null;
        }

        /// <summary>Short goal text for the HUD and the level info popup.</summary>
        public static string DescribeGoal(LevelSpec spec)
        {
            if (spec.Endless) return "Climb as high as you can";
            GoalSpec g = spec.Goal;
            switch (g.Type)
            {
                case GoalType.ReachFloor: return spec.LevelIndex == 8 || spec.LevelIndex == 15 ? "Reach floor " + g.Value + " and grab the gems" : "Reach floor " + g.Value;
                case GoalType.CollectCoins: return "Collect " + g.Count + " coins";
                case GoalType.ComboChain: return "Chain " + g.Count + " jump" + (g.Count == 1 ? "" : "s") + " of " + g.Value + "+ floors";
                case GoalType.NoWallBounce: return "Reach floor " + g.Value;
                case GoalType.FindHiddenGem: return "Find the hidden gem";
                case GoalType.Speedrun: return "Reach floor " + g.Value + " in " + g.TimeSec + " s";
                case GoalType.SurviveWave: return "Survive the wave and reach the top";
                case GoalType.BossChase: return "Outclimb the chase: floor " + g.Value;
                default: return "Reach the top";
            }
        }

        public static string DescribeStar(StarSpec star)
        {
            switch (star.Kind)
            {
                case StarSpec.KindClear: return "Clear the level";
                case StarSpec.KindCoins: return "Collect " + star.Value + " coins";
                case StarSpec.KindNoRevive: return "Do not revive";
                case StarSpec.KindTime: return "Finish in " + star.Value + " s";
                default: return "";
            }
        }
    }
}
