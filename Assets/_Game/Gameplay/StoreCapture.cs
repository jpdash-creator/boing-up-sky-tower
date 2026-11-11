using System.Globalization;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Development-only store listing capture (owner request 2026-11-10): sets up a chosen hero, power-up stock and wallet on the in-memory save of an
    /// AutopilotSuite, then (per run) uses the powers on a schedule and saves full-resolution screenshots while the player is inside a platform row window.
    /// Driven by the optional "capture" fields of autoplay.json (AutoplayOptions). Never runs in a release build (Autopilot.Allowed).
    /// </summary>
    public static class StoreCaptureSetup
    {
        const string SelectedKey = "settings.character";
        static bool applied, hadPref;
        static string prevPref;

        public static void Apply(AutoplayOptions o)
        {
            if (o == null || !o.capture || GameSession.Progress == null) return;
            ProgressService p = GameSession.Progress;
            applied = true;
            hadPref = PlayerPrefs.HasKey(SelectedKey);
            prevPref = PlayerPrefs.GetString(SelectedKey, "");
            if (!string.IsNullOrEmpty(o.hero))
            {
                p.AddCopies(o.hero, 1);
                p.MarkHeroRevealed(o.hero);
                CharacterDef def = CharacterCatalog.Get(o.hero);
                if (def != null) CharacterCatalog.Selected = def;
            }
            if (o.powerUnits > 0)
                foreach (PowerUpDef d in p.PowerUpDefs.Implemented) p.GrantPowerUp(d.Id, o.powerUnits);
            if (o.walletCoins > 0) p.AddCoins(o.walletCoins);
            if (o.walletGems > 0) p.AddGems(o.walletGems);
            AutopilotReport.Emit("{\"ev\":\"capture_setup\",\"hero\":\"" + AutopilotReport.Escape(o.hero ?? "") + "\",\"units\":" + o.powerUnits + "}");
        }

        public static void Restore()
        {
            if (!applied) return;
            applied = false;
            if (hadPref) PlayerPrefs.SetString(SelectedKey, prevPref); else PlayerPrefs.DeleteKey(SelectedKey);
            PlayerPrefs.Save();
        }
    }

    public sealed class StoreCaptureDriver : MonoBehaviour
    {
        static readonly PowerUp[] Order = { PowerUp.Shield, PowerUp.Magnet, PowerUp.SlowTime, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Flight, PowerUp.Rocket };

        RunController rc;
        Autopilot ap;
        AutoplayOptions o;
        string dir;
        float runTime, nextPowerAt, nextShotAt;
        int orderIdx, shots, powerUses;
        bool windowSeen;

        public bool Finished { get; private set; }

        System.Collections.IEnumerator FrameLoop(string fd)
        {
            int n = 0;
            while (!Finished)
            {
                yield return new WaitForEndOfFrame();
                if (rc == null || rc.Session == null || rc.Hud == null || rc.Hud.IntroRemaining > 0f || runTime <= 0f) continue;
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] jpg = tex.EncodeToJPG(Mathf.Clamp(o.jpegQuality, 50, 100));
                Destroy(tex);
                File.WriteAllBytes(Path.Combine(fd, "f" + n.ToString("00000") + ".jpg"), jpg);
                n++;
            }
            Time.captureFramerate = 0;
            AutopilotReport.Emit("{\"ev\":\"cap_frames\",\"n\":" + n + "}");
        }

        void OnDestroy() { if (o != null && o.frames) Time.captureFramerate = 0; }

        public void Init(RunController controller, Autopilot autopilot, AutoplayOptions options)
        {
            rc = controller;
            ap = autopilot;
            o = options;
            dir = Path.Combine(Application.persistentDataPath, "shots");
            try { Directory.CreateDirectory(dir); } catch (System.Exception) { }
            nextPowerAt = o.powerStartSec;
            if (o.frames)
            {
                string fd = Path.Combine(Application.persistentDataPath, "frames");
                try { if (Directory.Exists(fd)) Directory.Delete(fd, true); Directory.CreateDirectory(fd); } catch (System.Exception) { }
                Time.captureFramerate = Mathf.Clamp(o.fps, 10, 60);   // fixed game clock: every frame is exactly 1/fps seconds, however slowly the frames are saved
                StartCoroutine(FrameLoop(fd));
            }
        }

        void Update()
        {
            if (Finished || rc == null || rc.Session == null || rc.Hud == null) return;
            RunSession s = rc.Session;
            if (s.Over || rc.Paused || rc.Hud.IntroRemaining > 0f) return;
            runTime += Time.deltaTime;
            if (o.usePowers && runTime >= nextPowerAt) { FirePower(s); nextPowerAt = runTime + Mathf.Max(0.5f, o.powerEverySec); }
            int row = s.State.Row;
            if (o.shotRowTo > 0)
            {
                if (row >= o.shotRowFrom && row <= o.shotRowTo)
                {
                    windowSeen = true;
                    if (runTime >= nextShotAt) { Shoot(s, row); nextShotAt = runTime + Mathf.Max(0.05f, o.shotEverySec); }
                }
                else if (windowSeen && row > o.shotRowTo) Finished = true;
            }
            if (o.runSeconds > 0 && runTime >= o.runSeconds) Finished = true;
        }

        void FirePower(RunSession s)
        {
            // the hero's free powers first (when ready), then the bought ones in rotation
            if (s.Hero != null)
            {
                for (int k = 0; k < s.Hero.Count; k++)
                {
                    PowerUp hp = s.Hero.Type(k);
                    if (s.Hero.IsReady(k) && PowerUps.CanActivateHero(in s.State, hp)) { Use(PowerUps.HeroInputBit(hp), hp, "hero"); return; }
                }
            }
            for (int n = 0; n < Order.Length; n++)
            {
                PowerUp p = Order[(orderIdx + n) % Order.Length];
                if (s.PowerStock(p) <= 0 || !PowerUps.CanActivate(in s.State, p)) continue;
                orderIdx = (orderIdx + n + 1) % Order.Length;
                Use(PowerUps.InputBit(p), p, "bought");
                return;
            }
        }

        void Use(int bit, PowerUp p, string kind)
        {
            ap.ExtraBits |= bit;
            powerUses++;
            AutopilotReport.Emit("{\"ev\":\"cap_power\",\"t\":" + runTime.ToString("0.0", CultureInfo.InvariantCulture) + ",\"p\":\"" + p + "\",\"kind\":\"" + kind + "\",\"row\":" + rc.Session.State.Row + "}");
        }

        void Shoot(RunSession s, int row)
        {
            Camera cam = Camera.main;
            int gems = 0, stars = 0;
            if (cam != null)
            {
                float lo = cam.transform.position.y - cam.orthographicSize, hi = cam.transform.position.y + cam.orthographicSize;
                Pickup[] pk = s.Sim.Pickups;
                bool[] taken = s.State.PickupTaken;
                for (int i = 0; i < pk.Length; i++)
                {
                    if (taken != null && i < taken.Length && taken[i]) continue;
                    float y = pk[i].Y.F();
                    if (y < lo || y > hi) continue;
                    if (pk[i].Kind == PickupKind.Star) stars++; else gems++;
                }
            }
            shots++;
            string name = (o.shotPrefix ?? "shot") + "_n" + shots.ToString("000") + "_r" + row + "_g" + gems + "_s" + stars + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
            AutopilotReport.Emit("{\"ev\":\"cap_shot\",\"file\":\"" + name + "\",\"row\":" + row + ",\"gems\":" + gems + ",\"stars\":" + stars + "}");
        }
    }
}
