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
        public IEnumerator HeHas20LessHealthThanAPlayer()
        {
            // Owner, 2026-11-15: "give the swordsmen like 20 less health".
            SwordsmanBrain brain = Build(seed: 424242);
            yield return null;
            var vitals = brain.GetComponent<PlayerVitals>();
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - 20.0f, vitals.MaxHealth, 1e-3f, "his health is not a player's less 20");
            Assert.AreEqual(vitals.MaxHealth, vitals.Health, 1e-3f, "he does not start whole");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, _player.GetComponent<PlayerVitals>().MaxHealth, 1e-3f, "the player's health changed too");
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
                // This player lets go of the keys while it swings. A swing takes nothing from a player's movement (2026-11-13), and one that
                // walks after him swinging lands every cut: he is stunned again before he can raise his guard or finish a wind-up, and
                // nothing this test is about (his guard, the rebound, the punish, the dash, the cut back) ever happens. Found by this test.
                if (combat.IsCommitted || vitals.IsStunned) move = Vector2.zero;
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

        /// <summary>
        /// Owner, 2026-10-24: "the dash feels pretty useless ... because the enemies just auto lock onto you anyways ... the telegraph of the
        /// enemies attack is either very short or non existent". Against a player standing in his reach, his cut holds at the top of its wind-up
        /// for its tell, glinting, before it strikes; and a player who dashes round behind him as the tell starts is not followed: the cut goes
        /// where he faced and misses.
        /// </summary>
        [UnityTest]
        public IEnumerator HisCutHoldsItsTellAndDoesNotFollowADashRoundHim()
        {
            SwordsmanBrain brain = Build(seed: 424242);
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            _swordsman.transform.position = new Vector3(0.0f, 0.05f, 2.2f);   // the player in his reach
            Physics.SyncTransforms();
            SoulsSwordCombat his = brain.Combat;   // equipped as he woke: listened to before he can start a cut
            Assert.IsNotNull(his, "the swordsman's sword did not equip");

            SoulsAttack attack = null;
            float started = -1.0f, told = -1.0f, tellSeconds = 0.0f, live = -1.0f;
            int hits = 0, glints = AttackGlint.Shown;
            float clock = 0.0f;
            his.AttackStarted += a => { if (attack == null) { attack = a; started = clock; } };
            his.Telegraphed += (a, seconds) =>
            {
                if (told >= 0.0f) return;
                told = clock;
                tellSeconds = seconds;
                // The dash: round behind him on the same circle as the tell starts -- to his back-left, away from where a forehand slash starts
                // behind his right shoulder (the blade itself passes there).
                Vector3 centre = _swordsman.transform.position;
                Vector3 toPlayer = _player.transform.position - centre;
                toPlayer.y = 0.0f;
                _player.transform.position = centre + Quaternion.Euler(0.0f, -150.0f, 0.0f) * toPlayer;
                Physics.SyncTransforms();
            };
            vitals.Hit += from => hits++;
            int glintsAnnounced = 0;   // told to other players' copies of him (PlayerAnimationDriver.AnnounceState)
            _swordsman.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) => { if (state == AttackGlint.StateName) glintsAnnounced++; };

            float awayAtLive = 0.0f, yawAtLive = 0.0f, turnedWhileLive = 0.0f;
            for (; clock < 6.0f && (live < 0.0f || his.Phase == SoulsPhase.Active); clock += Dt)
            {
                yield return null;
                if (attack == null || his.Phase != SoulsPhase.Active)
                {
                    continue;
                }
                if (live < 0.0f)
                {
                    live = clock;
                    Vector3 toPlayer = _player.transform.position - _swordsman.transform.position;
                    toPlayer.y = 0.0f;
                    awayAtLive = Vector3.Angle(_swordsman.transform.forward, toPlayer);
                    yawAtLive = _swordsman.transform.eulerAngles.y;
                }
                turnedWhileLive = Mathf.Max(turnedWhileLive, Mathf.Abs(Mathf.DeltaAngle(yawAtLive, _swordsman.transform.eulerAngles.y)));
            }
            Assert.IsNotNull(attack, "he never attacked a player standing in his reach");
            Assert.GreaterOrEqual(told, 0.0f, "his cut had no tell");
            Assert.Greater(tellSeconds, 0.0f);
            Assert.AreEqual(glints + 1, AttackGlint.Shown, "his blade did not glint as it held");
            Assert.AreEqual(1, glintsAnnounced, "his glint was not announced: other players would not see his tell");
            Assert.GreaterOrEqual(live - started, attack.Startup + tellSeconds - 2.0f * Dt,
                $"his cut went live {live - started:0.00} s after it started: the tell was not held ({attack.Startup:0.00} s of wind-up + {tellSeconds:0.00} s)");
            Debug.Log($"CityX swordsman tell: {attack.Animation} live {live - started:0.00} s after it started ({tellSeconds:0.00} s held); " +
                      $"the player {awayAtLive:0} degrees off his facing as it went live, turned {turnedWhileLive:0.0} degrees while live; hits {hits}");
            float mostTurn = SwordsmanBrain.WindUpTurnDegreesPerSecond * (tellSeconds + attack.Startup) + 5.0f;
            Assert.Greater(awayAtLive, 150.0f - mostTurn, $"his wind-up followed the player round ({awayAtLive:0} degrees off as it went live)");
            Assert.Less(turnedWhileLive, 1.0f, $"he turned {turnedWhileLive:0.0} degrees with his blade live: the cut is not committed");
            Assert.AreEqual(0, hits, "his cut followed a player who dashed round behind him");
        }

        /// <summary>
        /// Thrown to the ground in the middle of a cut, the cut is gone. His mind stops while he lies limp, and his sword's clock stopped
        /// with it, mid-swing: once he was back on his feet the cut went on from where it had stopped, with no swing to be seen.
        /// </summary>
        [UnityTest]
        public IEnumerator KnockedDownInTheMiddleOfACutTheCutIsGone()
        {
            SwordsmanBrain brain = Build(seed: 424242);
            _player.GetComponent<PlayerVitals>().InfiniteHealth = true;
            _swordsman.transform.position = new Vector3(0.0f, 0.05f, 2.2f);   // the player in his reach
            Physics.SyncTransforms();
            SoulsSwordCombat his = brain.Combat;
            Assert.IsNotNull(his, "the swordsman's sword did not equip");
            SoulsAttack attack = null;
            his.AttackStarted += a => { if (attack == null) attack = a; };
            for (float clock = 0.0f; clock < 6.0f && attack == null; clock += Dt)
            {
                yield return null;
            }
            Assert.IsNotNull(attack, "setup: he never attacked a player standing in his reach");
            Assert.AreNotEqual(SoulsPhase.Idle, his.Phase, "setup: his cut was over as it began");

            _swordsman.GetComponent<PlayerVitals>().KnockDown(new Vector3(0.0f, 2.0f, 3.0f));
            Assert.IsTrue(_swordsman.GetComponent<Ragdoll>().IsLimp, "setup: the knockdown did not throw him");
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            Assert.AreEqual(SoulsPhase.Idle, his.Phase, "lying limp, his cut is still under way: it goes on when he is back on his feet");
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
