using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Wraith as built (owner, 2026-10-26): the player's body from the hips up, fading out at the cut; its own plain skull on the head
    /// bone; wisps trailing from the hips to a point; every surface the ghost's see-through glow, outlined and shadowed by nothing; and dead
    /// in two punches, fading away.
    /// </summary>
    public sealed class WraithTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
        }

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WraithBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "no Wraith.prefab: rebuild the scene (CityMassingSceneBuilder)");
            return prefab;
        }

        private Wraith Spawn()
        {
            GameObject instance = Object.Instantiate(Prefab());
            _spawned.Add(instance);
            var wraith = instance.GetComponent<Wraith>();
            wraith.Bind();
            return wraith;
        }

        [Test]
        public void TheBodyIsTheOwnersModelOnThePlayersArmsWornAwayAboveItsTip()
        {
            Wraith wraith = Prefab().GetComponent<Wraith>();
            var skin = wraith.transform.Find(BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = skin.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Color[] colours = mesh.colors;
            Assert.AreEqual(vertices.Length, colours.Length, "the body has no vertex alpha to wear away");
            Assert.AreEqual(vertices.Length, mesh.boneWeights.Length, "the body is not skinned");
            // (The player's arms and hands are most of it, as they were of the old upper body at 72k: no lighter arms until bodies get their lighter versions.)
            Assert.LessOrEqual(mesh.triangles.Length / 3, 75000, "heavier than it was");

            float lowest = float.MaxValue, highest = float.MinValue;
            bool leftHand = false, rightHand = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                lowest = Mathf.Min(lowest, vertices[i].y);
                highest = Mathf.Max(highest, vertices[i].y);
                if (Vector3.Distance(vertices[i], new Vector3(-BodyRig.HandX, BodyRig.HandY, 0.0f)) < 0.2f)
                {
                    leftHand = true;
                    Assert.AreEqual(1.0f, colours[i].a, 1e-3f, "the player's arm is not solid at the hand");
                }
                if (Vector3.Distance(vertices[i], new Vector3(BodyRig.HandX, BodyRig.HandY, 0.0f)) < 0.2f)
                {
                    rightHand = true;
                }
                if (Mathf.Abs(vertices[i].x) < 0.2f && vertices[i].y <= WraithBuilder.TailGoneY)
                {
                    Assert.AreEqual(0.0f, colours[i].a, 1e-3f, "the tail's tip is still there: it has an end you can see");
                }
                if (Mathf.Abs(vertices[i].x) < 0.2f && vertices[i].y >= WraithBuilder.TailSolidY)
                {
                    Assert.AreEqual(1.0f, colours[i].a, 1e-3f, "the torso is worn away above where the tail starts");
                }
            }
            Assert.IsTrue(leftHand && rightHand, "it does not have the player's arms, down to the hands");
            Assert.AreEqual(BodyRig.HeadCentreY + FusedBodyShape.HeadRadius, highest, 0.05f, "the model's head is not the size and place of the player's");
            Assert.Less(lowest, WraithBuilder.TailGoneY, "the model's tail was cut short: it should wear away, not stop");
            Assert.Greater(lowest, 0.0f, "the tail goes through the ground its root stands on");

            Assert.AreEqual(0.0f, WraithBuilder.TailSwayAt(1.5f), 1e-3f, "the chest writhes");
            Assert.AreEqual(1.0f, WraithBuilder.TailSwayAt(WraithBuilder.TailTipY), 1e-3f, "the tip does not writhe");
        }

        [Test]
        public void ItsHeadRidesTheHeadBoneAndItsTailTheHips()
        {
            Wraith wraith = Spawn();
            var skin = wraith.transform.Find(BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = skin.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(vertices[i].x) > 0.25f)
                {
                    continue;
                }
                if (vertices[i].y > BodyRig.HeadCentreY + 0.1f)
                {
                    Assert.AreSame(wraith.Head, skin.bones[weights[i].boneIndex0], "the top of the head does not ride the head bone");
                }
                if (vertices[i].y < WraithBuilder.HipsFullY - 0.05f)
                {
                    Assert.AreSame(wraith.Hips, skin.bones[weights[i].boneIndex1], "the tail does not ride the hips");
                    Assert.AreEqual(1.0f, weights[i].weight1, 1e-3f, "the tail does not ride the hips alone");
                }
            }
        }

        [Test]
        public void GasComesOffItsTailAndStopsWhenItDies()
        {
            Wraith wraith = Spawn();
            Assert.IsNotNull(wraith.Gas, "it has no gas");
            Assert.IsTrue(wraith.Gas.transform.IsChildOf(wraith.Hips), "the gas does not go where the body goes");
            ParticleSystem.MainModule main = wraith.Gas.main;
            Assert.AreEqual(ParticleSystemSimulationSpace.World, main.simulationSpace, "the gas is dragged round with the body instead of hanging where it came off");
            Assert.IsTrue(wraith.Gas.colorOverLifetime.enabled, "the gas does not thin to nothing");
            Assert.Less(wraith.Gas.transform.position.y, WraithBuilder.TailSolidY, "the gas does not come off the tail");
            Assert.IsNotNull(wraith.Gas.GetComponent<ParticleSystemRenderer>().sharedMaterial, "the gas has no material");

            ((IDamageable)wraith).ApplyHit(Wraith.MaxHealth, Vector3.zero, 0.0f);
            Assert.IsFalse(wraith.Gas.isEmitting, "dead, it goes on giving off gas");
        }

        [Test]
        public void TheGhostIsSeeThroughItsFaceSolidAndNothingThrowsAShadow()
        {
            GameObject prefab = Prefab();
            var wraith = prefab.GetComponent<Wraith>();
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(5, renderers.Length, "the Wraith is not a body, gas, two eyes and teeth");
            foreach (Renderer renderer in renderers)
            {
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, $"{renderer.name} throws a shadow");
                if (renderer.transform == wraith.Gas.transform)
                {
                    continue;
                }
                bool face = renderer.transform.IsChildOf(wraith.Face);
                Assert.AreEqual(!face, renderer.sharedMaterial.shader.name == WraithBuilder.ShaderName,
                    face ? $"{renderer.name} is smoke, not solid" : $"{renderer.name} is not on the ghost shader");
                Assert.AreEqual(!face, renderer.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    face ? $"{renderer.name} is see-through" : $"{renderer.name} draws with the opaque world: the cel lines would outline it");
            }
            Assert.IsNotNull(wraith.Glow, "it has no glow");
            Assert.AreEqual(LightType.Point, wraith.Glow.type);
            Assert.IsNotNull(wraith.MainCollision, "nothing can hit it");
            Assert.AreEqual(1, wraith.Renderers.Length, "its fade and flash do not reach its body");
        }

        /// <summary>Owner, 2026-11-05: "make the eyes on the wraith glowing red". The eyes sit in the model's sockets; the teeth are its own, solid white.</summary>
        [Test]
        public void ItHasGlowingRedEyesInTheModelsSocketsAndItsTeethSolidWhite()
        {
            Wraith wraith = Prefab().GetComponent<Wraith>();
            Assert.IsNotNull(wraith.Face, "it has no face");
            Assert.IsTrue(wraith.Face.IsChildOf(wraith.Head), "the face is not on the head");
            foreach ((string name, float side) in new[] { (WraithBuilder.EyeLeftName, -1.0f), (WraithBuilder.EyeRightName, 1.0f) })
            {
                Transform eye = wraith.Face.Find(name);
                Assert.IsNotNull(eye, $"no {name}");
                Assert.AreEqual(side * WraithBuilder.EyeOffsetX, eye.localPosition.x, 1e-3f, $"{name} is not out to its side");
                Assert.Greater(eye.localPosition.z, 0.0f, $"{name} looks backward");
                Material material = eye.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.Less(Vector4.Distance(WraithBuilder.EyeColour, material.GetColor("_BaseColor")), 0.01f, $"{name} is not red");
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), $"{name} does not glow");
                Color glow = material.GetColor("_EmissionColor");
                Assert.Greater(glow.r, 2.0f, $"{name} glows no brighter than white: the bloom will not take it");
                Assert.Greater(glow.r, glow.g * 8.0f, $"{name} does not glow red");
                Assert.Greater(glow.r, glow.b * 8.0f, $"{name} does not glow red");
            }
            Assert.IsNotNull(wraith.Jaw, "it has no point to put on a neck");
            Transform teeth = wraith.Jaw.Find(WraithBuilder.TeethName);
            Assert.IsNotNull(teeth, "it has no teeth");
            Mesh mesh = teeth.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.triangles.Length / 3, 200, "these are not the model's teeth");
            Assert.Less(Vector4.Distance(WraithBuilder.ToothColour, teeth.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor")), 0.01f, "its teeth are not white");
            // They hang from the front of the head: around the jaw point, and below the head's middle.
            Assert.Less(Vector3.Distance(mesh.bounds.center, Vector3.zero), 0.2f, "the teeth are not around the jaw point");
            Assert.Greater(wraith.Jaw.localPosition.z, 0.0f, "the jaw is not at the front");
            Assert.Less(wraith.Jaw.localPosition.y, 0.0f, "the jaw is not below the head's middle");
        }

        [Test]
        public void ItDiesInTwoPunchesAwardsXpAndDoesNotBleed()
        {
            Wraith wraith = Spawn();
            IDamageable target = wraith;
            Assert.LessOrEqual(Wraith.MaxHealth, 2.0f * CombatTuning.PunchDamage, "it does not die very quickly");
            Assert.IsTrue(wraith is IBloodless, "it bleeds");
            Assert.Greater(((IXpReward)wraith).XpReward, 0, "killing it is worth nothing");
            Assert.AreEqual(HitResponse.Landed, target.ApplyHit(CombatTuning.PunchDamage, Vector3.zero, CombatTuning.HitstunSeconds));
            Assert.IsTrue(wraith.IsAlive, "one punch killed it");
            Assert.AreEqual(1.0f, wraith.Hurt, 1e-4f, "it did not flinch");
            Assert.AreEqual(HitResponse.Landed, target.ApplyHit(CombatTuning.PunchDamage, Vector3.zero, CombatTuning.HitstunSeconds));
            Assert.IsFalse(wraith.IsAlive, "two punches did not kill it");
            Assert.IsFalse(wraith.MainCollision.enabled, "dead, it can still be hit");
            Assert.AreEqual(HitResponse.Ignored, target.ApplyHit(5.0f, Vector3.zero, 0.0f), "a dead ghost took a hit");
        }

        [Test]
        public void DeadItFadesAwayRisingOverASecond()
        {
            Wraith wraith = Spawn();
            float y = wraith.transform.position.y;
            ((IDamageable)wraith).ApplyHit(Wraith.MaxHealth, Vector3.zero, 0.0f);
            wraith.Tick(Wraith.FadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, wraith.FadeProgress, 1e-3f, "it does not fade");
            Assert.Greater(wraith.transform.position.y, y, "dead, it does not rise");
            Assert.Less(wraith.Face.localScale.x, 0.6f, "its face does not shrink away with it");
            wraith.Tick(Wraith.FadeSeconds);
            Assert.AreEqual(1.0f, wraith.FadeProgress, 1e-4f, "it never fades away");
            Assert.Less(wraith.Face.localScale.x, 0.01f, "faded away, its face is still there");
        }

        [Test]
        public void PosedItsArmsHangBelowItsShoulders()
        {
            Wraith wraith = Spawn();
            wraith.Tick(0.3f);
            wraith.ApplyPose();
            Assert.Less(wraith.HandLeft.position.y, wraith.ArmLeft.position.y - 0.5f, "the left arm does not hang");
            Assert.Less(wraith.HandRight.position.y, wraith.ArmRight.position.y - 0.5f, "the right arm does not hang");
            Assert.Greater(wraith.Head.position.y, wraith.Spine.position.y, "the head is not over the spine");
        }
    }
}
