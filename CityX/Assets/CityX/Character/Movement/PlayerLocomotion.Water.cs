using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BODY IN WATER (docs/WATER.md): wading slows it and weakens its jump in step with the depth at its feet; water over its chest
    /// floats it (sinking slowly, rising while Space is held, moving slowly); a flowing body of water carries it along. The rules are
    /// <see cref="Wading"/>'s; the water is whatever <see cref="CurrentWater"/> holds (nothing, in a dry world, where this does nothing).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How deep the water at the feet is, metres (0 dry), as of this frame's Tick.</summary>
        public float WaterDepth { get; private set; }

        /// <summary>The same as a share of the body's own height.</summary>
        public float WaterDepthShare { get; private set; }

        /// <summary>True while water over the chest floats the body.</summary>
        public bool IsFloating { get; private set; }

        /// <summary>Water this deep breaks a fall: landing in it does not hurt (PlayerInputReader asks before the fall is measured).</summary>
        public bool WaterBreaksFall => WaterDepthShare >= Wading.BreaksFallDepth;

        private Vector2 _waterFlow;

        /// <summary>Finds the water at the feet: its depth, whether it floats the body, and its flow. Cheap and nothing at all in a dry world.</summary>
        private void SenseWater()
        {
            if (!CurrentWater.Any)
            {
                WaterDepth = 0.0f;
                WaterDepthShare = 0.0f;
                IsFloating = false;
                _waterFlow = Vector2.zero;
                return;
            }
            WaterDepth = Mathf.Max(0.0f, CurrentWater.DepthAt(transform.position, out WaterBody body));
            WaterDepthShare = WaterDepth / Mathf.Max(0.01f, WorldHeight);
            IsFloating = Wading.Floats(IsFloating, WaterDepthShare);
            _waterFlow = body != null ? body.Flow : Vector2.zero;
        }

        /// <summary>The walking or running speed in the water: slowed by the depth, or floating's slow paddle.</summary>
        private float WaterSpeed(float speed)
        {
            if (IsFloating)
            {
                return Mathf.Min(speed, WalkSpeed * SizeSpeedFactor) * Wading.FloatingSpeed;
            }
            return speed * Wading.SpeedShare(WaterDepthShare);
        }

        /// <summary>The share of its launch a jump keeps out of the water at the feet.</summary>
        private float WaterJumpShare => Wading.JumpShare(WaterDepthShare);

        /// <summary>How the water moves the body this step: its flow, carried in step with the depth (all of it while floating).</summary>
        private Vector3 WaterCarry()
        {
            if (_waterFlow == Vector2.zero || WaterDepth <= 0.0f)
            {
                return Vector3.zero;
            }
            float carry = Wading.FlowCarry(WaterDepthShare, IsFloating);
            return new Vector3(_waterFlow.x, 0.0f, _waterFlow.y) * carry;
        }
    }
}
