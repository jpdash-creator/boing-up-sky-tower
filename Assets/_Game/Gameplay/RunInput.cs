using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Supplies the input word (SimInput bits) for each simulation tick. Used by the development-only Autopilot.</summary>
    public interface IInputSource
    {
        int ReadTick();
    }

    public enum TouchPhaseKind { Began, Held, Ended }

    public struct TouchSample
    {
        public int Id;
        public TouchPhaseKind Phase;
        public float X;
        public float Time;
    }

    /// <summary>
    /// Pure touch rules (doc 02 section 3): the oldest finger that is held moves the player by screen half (left half = left,
    /// right half = right); a second finger touching down while another is held jumps; a short single tap also jumps.
    /// Kept free of Unity types so it can be unit-tested with synthetic samples.
    /// </summary>
    public sealed class TouchControlLogic
    {
        public const float TapMaxSeconds = 0.20f;

        sealed class Finger { public int Id; public float StartTime; public float X; public bool Secondary; }

        readonly List<Finger> held = new List<Finger>();

        /// <summary>Movement bits (SimInput.Left / Right) for the oldest held finger; 0 when nothing is held.</summary>
        public int Move { get; private set; }

        /// <summary>Feeds this frame's touch samples. Returns true when a jump was triggered this frame.</summary>
        public bool Update(IList<TouchSample> samples, float screenWidth)
        {
            bool jump = false;
            for (int i = 0; i < samples.Count; i++)
            {
                TouchSample s = samples[i];
                Finger f = Find(s.Id);
                switch (s.Phase)
                {
                    case TouchPhaseKind.Began:
                        if (f == null)
                        {
                            f = new Finger { Id = s.Id, StartTime = s.Time, X = s.X, Secondary = held.Count > 0 };
                            if (f.Secondary) jump = true;   // second finger = jump
                            held.Add(f);
                        }
                        break;
                    case TouchPhaseKind.Held:
                        if (f != null) f.X = s.X;
                        break;
                    case TouchPhaseKind.Ended:
                        if (f != null)
                        {
                            // A quick single tap (the only finger of its gesture) is a jump.
                            if (!f.Secondary && held.Count == 1 && s.Time - f.StartTime <= TapMaxSeconds) jump = true;
                            held.Remove(f);
                        }
                        break;
                }
            }
            Move = 0;
            if (held.Count > 0) Move = held[0].X < screenWidth * 0.5f ? SimInput.Left : SimInput.Right;
            return jump;
        }

        Finger Find(int id)
        {
            for (int i = 0; i < held.Count; i++) if (held[i].Id == id) return held[i];
            return null;
        }

        public void Clear() { held.Clear(); Move = 0; }
    }

    /// <summary>
    /// Reads touch (and keyboard / mouse for editor testing) every frame and hands the simulation one input word per tick.
    /// Jump presses are latched until the next tick so no tap is lost between fixed steps.
    /// </summary>
    public sealed class RunInputReader
    {
        readonly TouchControlLogic touch = new TouchControlLogic();
        readonly List<TouchSample> samples = new List<TouchSample>(8);
        bool jumpLatched;
        int powerLatched;   // SimInput.PowerSlotN edges, latched like the jump until the next tick
        int keyboardMove;

        /// <summary>Optional override for tests: when set, Update() uses it instead of the devices.</summary>
        public System.Func<int> ScriptedMove;
        public System.Func<bool> ScriptedJump;

        /// <summary>The on-screen controls (set by the HUD once built).</summary>
        public ControlPad Pad;

        /// <summary>Development / test hook (the Autopilot): when set, ReadTick() takes the input word from it instead of the devices.</summary>
        public IInputSource Source;

        public void Update()
        {
            if (ScriptedMove != null || ScriptedJump != null)
            {
                keyboardMove = ScriptedMove != null ? ScriptedMove() : 0;
                if (ScriptedJump != null && ScriptedJump()) jumpLatched = true;
                return;
            }

            // On-screen pad (touch and mouse both arrive through the UI event system).
            keyboardMove = 0;
            if (Pad != null)
            {
                if (Pad.Left != null && Pad.Left.Held) keyboardMove |= SimInput.Left;
                if (Pad.Right != null && Pad.Right.Held) keyboardMove |= SimInput.Right;
                if (Pad.Jump != null && Pad.Jump.PressedEdge) { jumpLatched = true; Pad.Jump.PressedEdge = false; }
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardMove |= SimInput.Left;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardMove |= SimInput.Right;
                if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) jumpLatched = true;
                if (kb.digit1Key.wasPressedThisFrame) LatchPower(0);
                if (kb.digit2Key.wasPressedThisFrame) LatchPower(1);
                if (kb.digit3Key.wasPressedThisFrame) LatchPower(2);
            }
        }

        /// <summary>Input word for the next simulation tick (movement bits plus the Jump edge, which is then cleared).</summary>
        public int ReadTick()
        {
            if (Source != null) return Source.ReadTick();
            int move = touch.Move | keyboardMove;
            if ((move & SimInput.Left) != 0 && (move & SimInput.Right) != 0) move = 0;   // both pressed cancels
            int bits = move;
            if (jumpLatched)
            {
                bits |= SimInput.Jump;
                jumpLatched = false;
            }
            bits |= powerLatched;
            powerLatched = 0;
            return bits;
        }

        /// <summary>A power-up button was pressed: the activation bit of the loadout slot goes into the next tick's input (never lost between ticks).</summary>
        public void LatchPower(int slot)
        {
            if (slot < 0 || slot >= PowerUps.SlotCount) return;
            powerLatched |= PowerUps.InputBitForSlot(slot);
        }

        public void Clear()
        {
            touch.Clear();
            jumpLatched = false;
            powerLatched = 0;
            keyboardMove = 0;
        }
    }
}
