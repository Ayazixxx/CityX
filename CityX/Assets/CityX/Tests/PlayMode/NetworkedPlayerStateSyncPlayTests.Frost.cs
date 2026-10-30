using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Frost over real sockets (docs/FROST.md): B's frost blows on B's copy of A carry their chill to A's own body, where the cold is decided;
    /// full, A freezes there, and B's copy of A is frozen with it.
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        [UnityTest]
        public IEnumerator FrostBlowsOnAnotherPlayersCopyChillTheirOwnBodyAndTheFreezeShowsOnTheCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var ownerChill = _aOnA.GetComponent<Chill>();
            var copyChill = _aOnB.GetComponent<Chill>();
            Assert.IsNotNull(ownerChill, "the joining player's body cannot be chilled");
            var copyVitals = _aOnB.GetComponent<PlayerVitals>();
            Vector3 from = _aOnB.transform.position + _aOnB.transform.forward * 3.0f;

            using (DamageContext.WithoutBlood())
            using (DamageContext.Chilling(FrostRules.ChillPerTick))
            {
                DamageContext.Strike(copyVitals, 1.0f, from, 0.0f, DamageKind.Magic, 0.0f, _aOnB.transform.position + Vector3.up);
            }
            yield return WaitUntil(() => ownerChill.Level > 0.0f, "B's frost blow on its copy of A to chill A's own body");
            for (int i = 0; i < Mathf.CeilToInt(1.0f / FrostRules.ChillPerTick) + 1; i++)
            {
                using (DamageContext.WithoutBlood())
                using (DamageContext.Chilling(FrostRules.ChillPerTick))
                {
                    DamageContext.Strike(copyVitals, 1.0f, from, 0.0f, DamageKind.Magic, 0.0f, _aOnB.transform.position + Vector3.up);
                }
            }
            yield return WaitUntil(() => ownerChill.IsFrozen, "A to freeze once the cold is full");
            yield return WaitUntil(() => copyChill.IsFrozen, "B's copy of A to show A frozen");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
