using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The poison gas ability (key 4): the character blows out a cloud of purple gas. The same split IFireballCaster makes: PlayerInputReader (Character)
    /// asks, the implementation in CityX.Weapons owns the cloud prefab. An innate ability: found once with GetComponent, nothing to equip.
    /// </summary>
    public interface IPoisonGasCaster
    {
        bool CanCast { get; }

        /// <summary>True for a short window after a cast (the blowing-out pose): folded into the same "casting" checks the fireball uses.</summary>
        bool IsCasting { get; }

        /// <summary>1 right after a cast, easing to 0: drives the raised-arm cast pose (PlayerLocomotion.SetCastPose).</summary>
        float CastFraction { get; }

        void Tick(float deltaTime);

        /// <summary>Blows a cloud out from <paramref name="origin"/> along <paramref name="direction"/>. False on cooldown or when dead.</summary>
        bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders);
    }
}
