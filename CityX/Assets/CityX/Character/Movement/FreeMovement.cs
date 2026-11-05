using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW A PLAYER'S BODY MOVES (docs/MOVEMENT.md; owner, 2026-11-04: "get rid of the gta style movement", "skyrims feels like a good example we should try to imitate", and of free
    /// movement alone, "it makes it feel a bit cheap and roblox like"). It faces the camera as it moves and the keys walk it forward, sideways and back. What this class adds to that:
    /// a backward walk slower than a forward one, and a little weight (MovementWeight, scaled here). Both numbers are a first go, for the owner to feel.
    /// </summary>
    public static class FreeMovement
    {
        /// <summary>Backing straight up goes at this share of a walk. Much under 0.72 the full walk's clip outruns the ground too far for the foot locks to hold (FootPlanting.LockPaceMax), and the feet slide.</summary>
        public const float BackwardSpeedShare = 0.75f;

        /// <summary>A player's footing against MovementWeight's own numbers: a walk reached in about 0.2 s and stopped in about 0.15 s, light enough that it still answers the keys.</summary>
        public const float PlayerFootScale = 1.3f;

        /// <summary>The share of its speed a body keeps with these keys held: all of it forward or sideways, less the further the keys point back.</summary>
        public static float SpeedShare(Vector2 keys)
        {
            float amount = keys.magnitude;
            return amount > 1e-4f ? Mathf.Lerp(1.0f, BackwardSpeedShare, Mathf.Clamp01(-keys.y / amount)) : 1.0f;
        }
    }
}
