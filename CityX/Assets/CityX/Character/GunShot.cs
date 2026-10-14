using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Everything a trigger pull turned out to be, handed to whoever wants to
    /// react -- the camera kick, the reticle's hit marker, the sounds. A plain
    /// struct rather than a pile of separate events so the gun stays ignorant
    /// of every one of them (PistolWeapon lives in an assembly that cannot see
    /// the camera or the HUD).
    /// </summary>
    public struct GunShot
    {
        /// <summary>A round actually left the barrel.</summary>
        public bool Fired;

        /// <summary>The trigger was pulled on an empty magazine (or mid-reload) -- a click, not a shot.</summary>
        public bool DryClick;

        /// <summary>That dry click started an automatic reload.</summary>
        public bool AutoReloading;

        /// <summary>The bullet struck something solid.</summary>
        public bool HitSomething;

        /// <summary>...and it was something that takes damage (a body, not a wall).</summary>
        public bool HitDamageable;

        /// <summary>...and that hit killed it.</summary>
        public bool Killed;

        /// <summary>The barrel was blocked by scenery, so the crosshair's own target was NOT what got hit.</summary>
        public bool BarrelBlocked;

        /// <summary>Where the bullet ended.</summary>
        public Vector3 Point;

        /// <summary>The wielder was aiming down sights.</summary>
        public bool Aiming;

        /// <summary>
        /// How far to kick the camera up, in degrees, already scaled for
        /// aiming (a steadied shot kicks less). The gun decides its own kick;
        /// whoever moves the camera just applies it.
        /// </summary>
        public float RecoilPitchDegrees;

        /// <summary>Camera roll shake, degrees -- a few tenths, for the "that had weight" jolt.</summary>
        public float ShakeDegrees;

        /// <summary>How far to kick the camera to the RIGHT, degrees (negative: left): an automatic's spray wanders (FirearmProfile.RecoilPattern).</summary>
        public float RecoilYawDegrees;

        /// <summary>How long the camera holds this kick before it starts to settle, seconds: an automatic holds through its burst, so a spray climbs.</summary>
        public float RecoilHoldSeconds;
    }
}
