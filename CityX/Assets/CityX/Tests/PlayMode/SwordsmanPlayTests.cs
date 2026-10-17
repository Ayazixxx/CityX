using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The swordsman fought for real (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him. make him
    /// utilize dashes as well as blocking and make him take advantage of the stagger if i attack their block. implement a stagger system for if
    /// you swing at a block", "make the npc have all the movement animations that i do"): Swordsman.prefab, his own brain driving him, against a
    /// player body that keeps coming in and swinging, the way the player's keys drive it. Neither can die here (it is the fight that is under
    /// test, not the ending): over forty seconds he must block, have the player's blade bounce off his guard, punish that, dash, land hits, face
    /// the player, and move on the sword's own walk -- and the player's rebound must travel by name, as everything other players see does.
    /// </summary>
    public sealed class SwordsmanPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _swordsman;
        private SwordWeapon _playerSword;

        [TearDown]
        public void TearDown()
        {
            if (_playerSword != null) Object.Destroy(_playerSword.gameObject);
            if (_player != null) Object.Destroy(_player);
            if (_swordsman != null) Object.Destroy(_swordsman);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private SwordsmanBrain Build(int seed)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            var swordsmanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
            Assert.IsNotNull(swordsmanPrefab, "Swordsman.prefab is missing -- rebuild the scene");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _playerSword = Object.Instantiate(swordPrefab, new Vector3(30.0f, 1.0f, 30.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _playerSword.OnEquip(_player, CityX.Items.ItemInstanceData.None);

            _swordsman = Object.Instantiate(swordsmanPrefab, new Vector3(0.0f, 0.05f, 6.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            var brain = _swordsman.GetComponent<SwordsmanBrain>();
            Assert.IsNotNull(brain, "the swordsman has no brain");
            brain.Seed = seed;
            brain.Target = _player.GetComponent<PlayerVitals>();
            return brain;
#else
            Assert.Ignore("the prefabs can only be loaded by path inside the editor");
            return null;
#endif
        }

        private static float YawTo(Transform from, Vector3 point)
        {
            Vector3 toward = point - from.position;
            toward.y = 0.0f;
            return toward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toward).eulerAngles.y : from.eulerAngles.y;
        }

        [UnityTest]
        public IEnumerator HeBlocksPunishesTheReboundDashesAndHitsBackOnHisOwnWalk()
        {
            SwordsmanBrain brain = Build(seed: 20261017);
            var vitals = _player.GetComponent<PlayerVitals>();
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            var theirVitals = _swordsman.GetComponent<PlayerVitals>();
            var theirAnimator = _swordsman.GetComponentInChildren<Animator>();
            var theirLocomotion = _swordsman.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(combat, "the player's sword did not equip");
            yield return null;
            Assert.IsNotNull(brain.Combat, "the swordsman's sword did not equip");
            Assert.IsTrue(brain.Combat.IsOneHanded, "the swordsman's sword is not the one-handed one");

            vitals.InfiniteHealth = true;
            vitals.InfiniteStamina = true;
            theirVitals.InfiniteHealth = true;

            int rebounds = 0, punishes = 0, hitsOnPlayer = 0, facingFrames = 0, judgedFrames = 0, swordWalkFrames = 0, movingFrames = 0;
            bool reboundAnnounced = false;
            float clock = 0.0f, lastRebound = -10.0f, nextSwing = 1.5f;
            combat.Rebounded += () => { rebounds++; lastRebound = clock; };
            vitals.Hit += from =>
            {
                hitsOnPlayer++;
                if (clock - lastRebound < 1.0f) punishes++;
            };
            driver.TimedStatePlayed += (state, speed, start) => reboundAnnounced |= state == SwordStrikes.Rebound1HName;

            Vector3 lastTheirs = _swordsman.transform.position;
            for (; clock < 40.0f; clock += Dt)
            {
                // The player: comes in to cutting distance and swings when it can, as the keys would.
                combat.Tick(Dt);
                vitals.TickHitstun(Dt);
                vitals.TickStamina(false, Dt);
                Vector3 apart = _swordsman.transform.position - _player.transform.position;
                apart.y = 0.0f;
                float distance = apart.magnitude;
                float yaw = YawTo(_player.transform, _swordsman.transform.position);
                if (!vitals.IsStunned && !combat.IsCommitted && clock >= nextSwing && distance < 2.6f)
                {
                    combat.RequestLight(yaw);
                    nextSwing = clock + 0.9f;
                }
                Vector2 move = distance > 2.3f ? new Vector2(0.0f, 1.0f) : (distance < 1.6f ? new Vector2(0.0f, -0.5f) : Vector2.zero);
                if (combat.IsCommitted) move *= combat.CommittedMoveSpeedFraction;
                if (vitals.IsStunned) move = Vector2.zero;
                locomotion.Tick(new LocomotionInput { Move = move, YawDegrees = yaw, Sword1HEquipped = true, LockedOn = true }, Dt);
                yield return null;

                // How he moves: facing the player, on the one-handed sword's own walk.
                if (!theirLocomotion.IsDashing)
                {
                    Vector3 toPlayer = _player.transform.position - _swordsman.transform.position;
                    toPlayer.y = 0.0f;
                    judgedFrames++;
                    if (Vector3.Angle(_swordsman.transform.forward, toPlayer) < 35.0f) facingFrames++;
                }
                Vector3 step = _swordsman.transform.position - lastTheirs;
                step.y = 0.0f;
                lastTheirs = _swordsman.transform.position;
                if (step.magnitude / Dt > 0.5f && !theirLocomotion.IsDashing)
                {
                    movingFrames++;
                    if (theirAnimator.GetCurrentAnimatorStateInfo(0).IsName("Sword1HLocomotion")) swordWalkFrames++;
                }
            }

            Debug.Log($"CityX swordsman duel: {brain.GuardsRaised} guards, {rebounds} player rebounds, {punishes} punished, {brain.Dashes} dashes, " +
                      $"{hitsOnPlayer} hits on the player, facing {facingFrames}/{judgedFrames}, sword walk {swordWalkFrames}/{movingFrames}");
            Assert.Greater(brain.GuardsRaised, 0, "he never raised his guard");
            Assert.Greater(rebounds, 0, "the player's blade never bounced off his guard");
            Assert.IsTrue(reboundAnnounced, "the player's rebound was not announced by name, so other players would not see it");
            Assert.Greater(punishes, 0, "he never punished a blade that bounced off his guard");
            Assert.Greater(brain.Dashes, 0, "he never dashed");
            Assert.Greater(hitsOnPlayer, 1, "he never hit back");
            Assert.Greater(facingFrames, judgedFrames * 0.8f, "he does not face whoever he fights");
            Assert.Greater(movingFrames, 60, "he hardly moved");
            Assert.Greater(swordWalkFrames, movingFrames * 0.8f, "he does not move on the one-handed sword's own walk");   // (the blend back out of a dash is the rest)
        }

        [UnityTest]
        public IEnumerator HeCanBeBeatenAndPaysXp()
        {
            SwordsmanBrain brain = Build(seed: 7);
            var theirVitals = _swordsman.GetComponent<PlayerVitals>();
            yield return null;
            Assert.AreEqual(brain.XpReward, theirVitals.XpRewardOnDeath, "beating him pays nothing");
            for (int i = 0; i < 40 && theirVitals.IsAlive; i++)
            {
                theirVitals.TakeDamage(25.0f, false);
                yield return null;
            }
            Assert.IsTrue(theirVitals.IsDead, "he cannot be beaten");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(DuelistState.Idle, brain.State, "beaten, he fights on");
        }
    }
}
