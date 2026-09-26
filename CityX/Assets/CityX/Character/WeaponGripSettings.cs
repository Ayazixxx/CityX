using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How a weapon is held, hand-tuned in the Unity editor and saved as an asset so it survives
    /// leaving Play Mode -- the same idea as HairFitSettings.
    ///
    /// Each weapon has a DERIVED hold worked out in code (the sword's from the mocap, the pistol's
    /// free hand from measurements: see SwordWeapon and PistolWeapon). While the game runs, the held
    /// weapon is an ordinary object under the right hand and it carries a small "left hand" marker;
    /// drag them in the Scene view and the hands follow, then press Save on the weapon's Inspector.
    /// What it ends up at is written here and every equip after that uses it. "Tuned" is false until
    /// something is saved, so an untouched project behaves exactly as the derived hold does.
    /// </summary>
    public sealed class WeaponGripSettings : ScriptableObject
    {
        public const string ResourceName = "WeaponGrips";

        /// <summary>Where the asset lives: a Resources folder, so it loads by name with nothing wired.</summary>
        public const string AssetPath = "Assets/CityX/Character/Resources/WeaponGrips.asset";

        [Header("Two-handed sword")]
        [Tooltip("False until a hold is saved: the sword then uses the hold derived from the animation.")]
        public bool SwordTuned;

        [Tooltip("Where the sword sits, in the RIGHT HAND bone's space.")]
        public Vector3 SwordPosition;

        [Tooltip("How the sword is turned, in the right hand's space (degrees). Its blade runs along its own +Y.")]
        public Vector3 SwordEuler;

        [Tooltip("Where the left palm holds, in the SWORD's own space (the blade runs along +Y from the guard).")]
        public Vector3 SwordLeftHand;

        [Header("Pistol")]
        [Tooltip("False until saved: the free hand then uses the derived point.")]
        public bool PistolTuned;

        [Tooltip("Where the free (left) palm holds, in the PISTOL's own space. The model is on its side: +Z is the barrel, +Y the shooter's left, +X up.")]
        public Vector3 PistolLeftHand;

        [Header("Sword swings")]
        [Range(0.0f, 1.0f)]
        [Tooltip("How much of the animation's spine and hips motion a swing or guard keeps. 0 stays upright, 1 is the full lean and twist.")]
        public float TorsoFollowStrength = 0.65f;

        private static WeaponGripSettings _current;

        /// <summary>
        /// The settings in force: the saved asset, or a plain default one (nothing tuned) when there is
        /// none -- so a fresh project, and a test that puts its own in place, both work.
        /// </summary>
        public static WeaponGripSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<WeaponGripSettings>(ResourceName);
                    if (_current == null)
                    {
                        _current = CreateInstance<WeaponGripSettings>();
                    }
                }
                return _current;
            }
            set => _current = value;
        }

        /// <summary>Keeps the sword's hold: where the sword is on the hand, and where the left palm is on the sword.</summary>
        public void SaveSword(Transform sword, Transform leftHand)
        {
            SwordTuned = true;
            SwordPosition = sword.localPosition;
            SwordEuler = Wrapped(sword.localEulerAngles);
            SwordLeftHand = leftHand.localPosition;
        }

        /// <summary>Forgets the saved sword hold; the next equip uses the derived one again.</summary>
        public void ResetSword() => SwordTuned = false;

        /// <summary>Keeps where the pistol's free palm holds.</summary>
        public void SavePistol(Transform leftHand)
        {
            PistolTuned = true;
            PistolLeftHand = leftHand.localPosition;
        }

        public void ResetPistol() => PistolTuned = false;

        /// <summary>Angles wrapped to -180..180 so they read as small numbers.</summary>
        private static Vector3 Wrapped(Vector3 euler)
        {
            return new Vector3(Wrap(euler.x), Wrap(euler.y), Wrap(euler.z));
        }

        private static float Wrap(float degrees)
        {
            degrees %= 360.0f;
            if (degrees > 180.0f) degrees -= 360.0f;
            if (degrees < -180.0f) degrees += 360.0f;
            return degrees;
        }
    }
}
