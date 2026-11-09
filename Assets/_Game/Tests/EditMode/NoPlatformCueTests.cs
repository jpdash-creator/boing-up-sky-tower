using System.Collections.Generic;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests.EditModeTests
{
    /// <summary>
    /// Owner decision 2026-11-01 (issue list 30 item 3, overrides the telegraph part of CUX-6 / CUX-7): platform behaviour is discovered by the player,
    /// so no code-drawn cue (arrow, bee, pip, warning icon, speed line, pattern strip) is ever drawn on a special, hazard or unique platform.
    /// Hazards still never kill, are never unavoidable and every level is bot-proven solvable (Simulation tests).
    /// </summary>
    [Category("CriticalUX")]
    public class NoPlatformCueTests
    {
        static readonly PlatformKind[] Region1Kinds =
        {
            PlatformKind.WindPetal, PlatformKind.HiveLift, PlatformKind.SnapDaisy, PlatformKind.Dandelion,
            PlatformKind.ThornBush, PlatformKind.Ladybug, PlatformKind.Seesaw,
            // region 2 (doc 30): placeholders are tinted normal platforms, no cue sprites either
            PlatformKind.SquishCap, PlatformKind.SproutLift, PlatformKind.PuffFloat, PlatformKind.FairyRing,
            PlatformKind.OrbitCap, PlatformKind.DizzySpores, PlatformKind.SporePop
        };

        [Test]
        public void SpecialPlatforms_DrawNoOverlaySprites()
        {
            int made = 0;
            var fx = new SpecialPlatformFx((n, sp, o) => { made++; return new GameObject(n).AddComponent<SpriteRenderer>(); }, false);
            fx.SetKnob(null, 0f);
            fx.Resize(Region1Kinds.Length);
            for (int i = 0; i < Region1Kinds.Length; i++)
            {
                var p = new Platform { Id = i, Kind = Region1Kinds[i], Width = Fx.FromRatio(40, 10), Dir = 1 };
                fx.Attach(i, p, true);
                fx.Attach(i, p, false);
            }
            Assert.AreEqual(0, made, "no code-drawn cue sprite may be created for any special platform kind");
        }

        [Test]
        public void OnlyThePaintedSeesawKnobIsAnExtraSprite()
        {
            var made = new List<GameObject>();
            var fx = new SpecialPlatformFx((n, sp, o) => { var go = new GameObject(n); made.Add(go); return go.AddComponent<SpriteRenderer>(); }, false);
            var tex = new Texture2D(4, 4);
            fx.SetKnob(Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f), 0f);
            fx.Resize(2);
            fx.Attach(0, new Platform { Id = 0, Kind = PlatformKind.WindPetal, Width = Fx.FromRatio(40, 10) }, true);
            Assert.AreEqual(0, made.Count);
            fx.Attach(1, new Platform { Id = 1, Kind = PlatformKind.Seesaw, Width = Fx.FromRatio(40, 10) }, true);
            Assert.AreEqual(1, made.Count, "the painted seesaw knob is art, not a cue");
            foreach (var go in made) Object.DestroyImmediate(go);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void SpecialPlatformFx_HasNoCueTypesOrProceduralShapes()
        {
            var methods = typeof(SpecialPlatformFx).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var m in methods)
                Assert.False(m.Name == "Tri" || m.Name == "Disc" || m.Name == "Hide" || m.Name == "Put" || m.Name == "SpriteOf" || m.Name == "PartCount", "cue helper still present: " + m.Name);
        }
    }
}
