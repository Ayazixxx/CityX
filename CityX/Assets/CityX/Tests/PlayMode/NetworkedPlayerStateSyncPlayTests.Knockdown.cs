using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        /// <summary>
        /// A knockdown sent after a blow stands only if the blow landed on the real body (bug hunt M5: a forwarded knockdown ignored the defender's
        /// block, parry or dash; for a 1v1 duel, owner 2026-11-12): blocked, B stays on its feet; landed, B goes down.
        /// </summary>
        [UnityTest]
        public IEnumerator AKnockdownAfterABlockedBlowThroughTheRelayLeavesTheBodyStanding()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();
            var copyRelay = _bOnA.GetComponent<NetworkedDamage>();
            var realRagdoll = _bOnB.GetComponent<Ragdoll>();

            realVitals.Roll = () => 0.0f;
            realVitals.StartBlock();
            float before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.2f, DamageKind.Physical, 0.0f);
            Assert.IsTrue(copyRelay.TryRelayKnockdown(new Vector3(0.0f, 3.0f, 5.0f)), "the copy did not send the knockdown on");
            yield return WaitUntil(() => realVitals.Health < before || _bOnB.GetComponent<BlockDeflectEffect>().Shown > 0, "the blow to reach B");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsFalse(realRagdoll.IsLimp, "a blocked blow's knockdown still threw B down");

            realVitals.EndBlock();   // nothing ticks B's block down in this harness
            yield return new WaitForSeconds(NetworkedDamage.KnockdownFollowsHitSeconds + 0.6f);
            before = realVitals.Health;
            DamageContext.Strike(copyVitals, 5.0f, _aOnA.transform.position, 0.0f, DamageKind.Physical, 0.0f);
            Assert.IsTrue(copyRelay.TryRelayKnockdown(new Vector3(0.0f, 3.0f, 5.0f)));
            yield return WaitUntil(() => realVitals.Health < before, "the landed blow to reach B");
            yield return WaitUntil(() => realRagdoll.IsLimp, "a landed blow's knockdown to throw B down");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
