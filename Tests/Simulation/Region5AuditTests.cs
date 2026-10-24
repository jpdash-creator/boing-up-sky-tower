using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Region 5 (Bamboo Temple) QA: platform rows, gongs and pickups of lvl_005_01..15. A gong never kills, but it must not
    /// be unavoidable or unfair either: a swing must not sweep the standing spot of a platform, and it must stay inside the walls.
    /// </summary>
    public class Region5AuditTests
    {
        readonly ITestOutputHelper output;
        public Region5AuditTests(ITestOutputHelper output) { this.output = output; }

        static double D(Fx v) { return v.Raw / 65536.0; }

        /// <summary>Platforms (not the floor) a player standing anywhere on the surface could be hit on by gong <paramref name="h"/> at some tick.</summary>
        public static List<string> StandingSweeps(SimConfig cfg, LevelData data, int tickLimit)
        {
            Sim sim = new Sim(cfg, data, new LevelSpec());
            List<string> found = new List<string>();
            double hitR = D(cfg.PlayerHitRadius);
            for (int g = 0; g < data.Hazards.Length; g++)
            {
                Hazard h = data.Hazards[g];
                double r = D(h.Radius) + hitR;
                for (int i = 0; i < data.Platforms.Length; i++)
                {
                    Platform p = data.Platforms[i];
                    if (D(p.Width) >= 9) continue;
                    double top = D(p.Y) + D(cfg.PlayerCenterY);
                    if (Math.Abs(top - D(h.Y)) > D(h.Amplitude) + r + 1) continue;
                    double halfExt = D(p.Width) / 2 + D(cfg.LandingMargin) + (p.Kind == PlatformKind.Moving ? D(p.Amplitude) : 0);
                    bool hit = false;
                    int period = h.Period;
                    for (int t = 0; t < period && !hit; t++)
                    {
                        Fx gx, gy;
                        sim.HazardPosition(g, t, out gx, out gy);
                        double dy = top - D(gy);
                        if (Math.Abs(dy) >= r) continue;
                        double dx = Math.Sqrt(r * r - dy * dy);
                        double lo = D(gx) - dx, hi = D(gx) + dx;
                        if (hi >= D(p.X) - halfExt && lo <= D(p.X) + halfExt) hit = true;
                    }
                    if (hit) found.Add("gong#" + g + " (y=" + D(h.Y).ToString("F2") + " r=" + D(h.Radius).ToString("F2") + " amp=" + D(h.Amplitude).ToString("F2") + ") sweeps standing spot of platform #" + p.Id + " row " + p.Row + " " + p.Kind + " x=" + D(p.X).ToString("F2") + " w=" + D(p.Width).ToString("F2") + " y=" + D(p.Y).ToString("F2"));
                }
            }
            return found;
        }

        [Fact]
        public void Region5GongsNeverSweepAStandingSpot()
        {
            int totalSweeps = 0, totalGongs = 0;
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 5, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                StackStats st = StackAnalysis.Scan(d.Platforms);
                int stars = 0, gems = 0, hidden = 0, inPlat = 0;
                foreach (Pickup pk in d.Pickups)
                {
                    if (pk.Kind == PickupKind.Star) stars++;
                    else if (pk.Kind == PickupKind.Gem) gems++;
                    else if (pk.Kind == PickupKind.HiddenGem) hidden++;
                    if (!LevelBuilder.IsPickupClearOfPlatforms(d.Platforms, pk)) inPlat++;
                }
                int outside = 0;
                foreach (Hazard h in d.Hazards)
                    if (D(h.X) - D(h.Amplitude) - D(h.Radius) < 0 || D(h.X) + D(h.Amplitude) + D(h.Radius) > 10) outside++;
                List<string> sweeps = StandingSweeps(cfg, d, 0);
                output.WriteLine(ll.Id + " goal=" + ll.Spec.Goal.Type + " rows=" + d.Rows + " plats=" + d.Platforms.Length + " gongs=" + d.Hazards.Length + " stack=" + st.MaxCluster + " win3=" + st.MaxIn3u + " minGap=" + D(st.MinOverlapGap).ToString("F2")
                    + " stars=" + stars + " gems=" + gems + " hidden=" + hidden + " pickupsInPlatformZone=" + inPlat + " gongsOutsideWalls=" + outside + " standingSweeps=" + sweeps.Count);
                foreach (string s in sweeps) output.WriteLine("    " + s);
                totalSweeps += sweeps.Count; totalGongs += d.Hazards.Length;
                Assert.Equal(3, stars);
                Assert.Equal(0, inPlat - (ll.Spec.Goal.Type == GoalType.FindHiddenGem ? 1 : 0));
                Assert.Equal(0, outside);
            }
            Assert.True(totalSweeps == 0, totalSweeps + " gong/platform standing-spot sweeps");
            Assert.True(totalGongs >= 100, "placement filter must keep gongs in play, kept " + totalGongs);
        }

        [Fact]
        public void Region5GongsAvoidPlatformsAndPickups()
        {
            int bad = 0;
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 5, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Sim sim = new Sim(cfg, d, ll.Spec);
                // star/gem inside a gong's swept disc
                int pickupInGong = 0, gongInPlatform = 0; double minGapX = 99; string minInfo = "";
                for (int k = 0; k < d.Pickups.Length; k++)
                {
                    Pickup pk = d.Pickups[k];
                    for (int g = 0; g < d.Hazards.Length; g++)
                    {
                        Hazard h = d.Hazards[g];
                        if (Math.Abs(D(h.Y) - D(pk.Y)) > D(h.Amplitude) + 3) continue;
                        bool hit = false;
                        for (int t = 0; t < h.Period && !hit; t += 2)
                        {
                            Fx gx, gy; sim.HazardPosition(g, t, out gx, out gy);
                            double dx = D(gx) - D(pk.X), dy = D(gy) - D(pk.Y);
                            if (Math.Sqrt(dx * dx + dy * dy) < D(h.Radius) + 0.45 + 0.3) hit = true;
                        }
                        if (hit) { pickupInGong++; output.WriteLine(ll.Id + " " + pk.Kind + " #" + pk.Id + " at " + D(pk.X).ToString("F2") + "," + D(pk.Y).ToString("F2") + " is inside gong#" + g + " swing"); break; }
                    }
                }
                for (int g = 0; g < d.Hazards.Length; g++)
                {
                    Hazard h = d.Hazards[g];
                    for (int i = 0; i < d.Platforms.Length; i++)
                    {
                        Platform p = d.Platforms[i];
                        if (D(p.Width) >= 9) continue;
                        // gong disc crossing the platform slab (surface +-0.2) while above/below a wide span
                        bool over = false;
                        for (int t = 0; t < h.Period && !over; t += 2)
                        {
                            Fx gx, gy; sim.HazardPosition(g, t, out gx, out gy);
                            double cy = Math.Max(D(p.Y) - 0.25, Math.Min(D(gy), D(p.Y)));
                            double cx = Math.Max(D(p.X) - D(p.Width) / 2, Math.Min(D(gx), D(p.X) + D(p.Width) / 2));
                            double dx = D(gx) - cx, dy = D(gy) - cy;
                            if (Math.Sqrt(dx * dx + dy * dy) < D(h.Radius) * 0.6) over = true;
                        }
                        if (over) { gongInPlatform++; output.WriteLine(ll.Id + " gong#" + g + " passes through platform #" + p.Id + " row " + p.Row + " x=" + D(p.X).ToString("F2") + " y=" + D(p.Y).ToString("F2")); }
                    }
                }
                for (int i = 0; i < d.Platforms.Length; i++)
                    for (int j = i + 1; j < d.Platforms.Length && D(d.Platforms[j].Y) - D(d.Platforms[i].Y) < 1.5; j++)
                    {
                        Platform a = d.Platforms[i], b = d.Platforms[j];
                        if (D(a.Width) >= 9 || D(b.Width) >= 9) continue;
                        double gap = Math.Abs(D(a.X) - D(b.X)) - D(a.Width) / 2 - D(b.Width) / 2 - D(a.Amplitude) - D(b.Amplitude);
                        if (gap < minGapX) { minGapX = gap; minInfo = "#" + a.Id + " " + a.Kind + " / #" + b.Id + " " + b.Kind + " row " + a.Row + " dy=" + (D(b.Y) - D(a.Y)).ToString("F2"); }
                    }
                bad += pickupInGong + gongInPlatform;
                output.WriteLine(ll.Id + " pickupsInGong=" + pickupInGong + " gongThroughPlatform=" + gongInPlatform + " minSameRowGap=" + minGapX.ToString("F2") + " " + minInfo);
            }
            Assert.Equal(0, bad);
        }
    }
}