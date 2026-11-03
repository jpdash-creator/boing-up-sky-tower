namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// The cooldown clock of a hero's powers in one run (owner 2026-11-02, docs/34 section "Hero powers in a run"). Pure C#, no Unity types, counted in
    /// simulation ticks (RunSession.Step calls <see cref="Tick"/> once per tick), so it is deterministic and replays identically.
    /// Every power has its OWN cooldown. All powers are READY at the start of the run. A use costs nothing and starts the power's cooldown
    /// (<c>cooldownSeconds</c> x 60 ticks, same for every power of the hero: Uncommon 30 s, Rare 22 s, Elite 15 s, Legendary 10 s).
    /// Only implemented types count (PowerUps.IsImplemented), a type appears once, at most <see cref="MaxPowers"/> (4) powers are kept.
    /// </summary>
    public sealed class HeroPowerCooldown
    {
        public const int MaxPowers = 4;

        readonly PowerUp[] types = new PowerUp[MaxPowers];
        readonly int[] remaining = new int[MaxPowers];
        readonly int cooldownTicks;

        public int Count { get; private set; }

        /// <summary>Cooldown of every power in ticks (60 per second).</summary>
        public int CooldownTicks { get { return cooldownTicks; } }

        public HeroPowerCooldown(PowerUp[] powers, int cooldownSeconds)
        {
            cooldownTicks = (cooldownSeconds < 0 ? 0 : cooldownSeconds) * SimConfig.TicksPerSecond;
            if (powers == null) return;
            for (int i = 0; i < powers.Length && Count < MaxPowers; i++)
            {
                PowerUp p = powers[i];
                if (!PowerUps.IsImplemented(p)) continue;
                bool dup = false;
                for (int j = 0; j < Count; j++) if (types[j] == p) dup = true;
                if (dup) continue;
                types[Count++] = p;
            }
        }

        public PowerUp Type(int index) { return index >= 0 && index < Count ? types[index] : PowerUp.None; }

        /// <summary>Index of the power of this type, or -1.</summary>
        public int IndexOf(PowerUp p)
        {
            for (int i = 0; i < Count; i++) if (types[i] == p) return i;
            return -1;
        }

        public bool IsReady(int index) { return index >= 0 && index < Count && remaining[index] <= 0; }

        public int RemainingTicks(int index) { return index >= 0 && index < Count ? remaining[index] : 0; }

        /// <summary>Whole seconds left, rounded up (0 when ready).</summary>
        public int RemainingSeconds(int index)
        {
            int t = RemainingTicks(index);
            return t <= 0 ? 0 : (t + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond;
        }

        /// <summary>1 = just used (full cooldown left), 0 = ready.</summary>
        public float Fraction(int index)
        {
            if (cooldownTicks <= 0 || index < 0 || index >= Count) return 0f;
            float f = remaining[index] / (float)cooldownTicks;
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }

        /// <summary>Uses the power when it is ready and starts its cooldown. Returns false (nothing changes) when it is still cooling down or does not exist.</summary>
        public bool TryUse(int index)
        {
            if (!IsReady(index)) return false;
            remaining[index] = cooldownTicks;
            return true;
        }

        /// <summary>Advances every cooldown by one simulation tick.</summary>
        public void Tick()
        {
            for (int i = 0; i < Count; i++) if (remaining[i] > 0) remaining[i]--;
        }
    }
}
