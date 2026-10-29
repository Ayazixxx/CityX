using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A held light (the flashlight, docs/FLASHLIGHT.md) as the body's <see cref="FlashlightSync"/> sees it, without the Weapons assembly (the
    /// way IEquippableGun lets the input reader see a gun).
    /// </summary>
    public interface ITorch
    {
        /// <summary>It is on: switched on by its holder (on another player's copy, by its owner's word).</summary>
        bool IsLit { get; }

        /// <summary>Where it points: what is under the crosshair, or the end of its reach.</summary>
        Vector3 AimPoint { get; }

        /// <summary>Its holder switches it on or off (F, as in the Godot game; a click does it too).</summary>
        void Toggle();

        /// <summary>On another player's copy: on or off, and pointed at <paramref name="point"/>, as its owner's is.</summary>
        void ShowLit(bool lit, Vector3 point);
    }
}
