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
        public void TheBodyIsThePlayersFromTheHipsUpFadingOutAtTheCut()
        {
            Transform skin = Prefab().transform.Find(BodySurface.SkinName);
            Assert.IsNotNull(skin, "the Wraith has no skin");
            var renderer = skin.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Color[] colours = mesh.colors;
            Assert.AreEqual(vertices.Length, colours.Length, "the body has no vertex alpha to fade with");
            Assert.AreEqual(vertices.Length, mesh.boneWeights.Length, "the body lost its skinning");
            Assert.AreEqual(mesh.bindposes.Length, renderer.bones.Length, "the skin's bones do not match the body's bind poses");
            Assert.AreEqual(BodyRigBuilder.SkinBoneOrder().Count, renderer.bones.Length);
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                lowest = Mathf.Min(lowest, vertices[i].y);
                highest = Mathf.Max(highest, vertices[i].y);
                if (vertices[i].y >= WraithBuilder.FadeTopTorsoY)
                {
                    Assert.AreEqual(1.0f, colours[i].a, 1e-3f, "the body is see-through above where it should be solid");
                }
                Assert.AreEqual(WraithBuilder.BodyAlphaAt(vertices[i]), colours[i].a, 1e-3f, "a vertex is not as solid as the body is there");
                if (vertices[i].y <= WraithBuilder.CutY + 0.005f)
                {
                    Assert.Less(colours[i].a, 0.02f, "the cut edge is drawn: the legs end instead of fading");
                }
            }
            Assert.GreaterOrEqual(lowest, WraithBuilder.CutY - 1e-4f, "the body goes below the hips");
            Assert.Greater(highest, BodyRig.ShoulderY, "the body has no shoulders");
            Assert.Greater(mesh.bounds.max.x, BodyRig.HandX - 0.05f, "the body has no arms: it is not the player's width");
            // The arms hang beside the smoke, solid to just over the hands; the torso is turning to smoke by then.
            Assert.AreEqual(1.0f, WraithBuilder.BodyAlphaAt(new Vector3(BodyRig.HandX, BodyRig.HandY + 0.2f, 0.0f)), 1e-3f, "the arms turn to smoke");
            Assert.Less(WraithBuilder.BodyAlphaAt(new Vector3(0.0f, BodyRig.HandY + 0.2f, 0.0f)), 0.7f, "the torso is still solid at the hips");
        }

        [Test]
        public void TheHeadIsItsOwnPlainSkullOnTheHeadBone()
        {
            var wraith = Prefab().GetComponent<Wraith>();
            Transform skull = wraith.Head.Find(BodySurface.HeadName);
            Assert.IsNotNull(skull, "no skull under the head bone");
            Mesh head = skull.GetComponent<MeshFilter>().sharedMesh;
            Mesh plain = HeadMesh.Build();
            Assert.AreEqual(plain.vertexCount, head.vertexCount, "the skull is not the player's plain head");
            Assert.AreEqual(plain.triangles.Length, head.triangles.Length);
            Assert.AreEqual(WraithBuilder.ShaderName, skull.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
            Assert.AreEqual(BodyRig.HeadCentreY, skull.position.y, 0.01f, "the skull is not where the player's head is");
            Object.DestroyImmediate(plain);
        }

        [Test]
        public void TheWispsTrailFromTheHipsToAPointAndSway()
        {
            var wraith = Prefab().GetComponent<Wraith>();
            Transform wisps = wraith.Hips.Find(WraithBuilder.WispsName);
            Assert.IsNotNull(wisps, "no wisps under the hips");
            Mesh mesh = wisps.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Color[] colours = mesh.colors;
            Assert.AreEqual(WraithBuilder.WispTopY, mesh.bounds.max.y, 0.02f, "the wisps do not start on the torso");
            Assert.AreEqual(WraithBuilder.WispTipY, mesh.bounds.min.y, 0.02f, "the wisps do not trail down to their tips");
            Assert.Less(mesh.bounds.max.y, WraithBuilder.FadeTopTorsoY, "the wisps start above where the torso turns to smoke");
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = wisps.TransformPoint(vertices[i]);
                Assert.AreEqual(vertices[i].y, world.y, 1e-3f, "the wisps hang off their place on the body");
                if (vertices[i].y <= WraithBuilder.WispTipY + 0.01f)
                {
                    Assert.Less(colours[i].a, 0.02f, "a wisp ends solid instead of fading out");
                }
                if (vertices[i].y >= WraithBuilder.WispTopY - 0.01f)
                {
                    Assert.AreEqual(WraithBuilder.BodyAlphaAt(new Vector3(0.0f, WraithBuilder.WispTopY, 0.0f)) * WraithBuilder.WispSolidShare,
                        colours[i].a, 1e-3f, "a wisp starts more or less solid than the torso it grows out of");
                }
            }
            Material material = wisps.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual(WraithBuilder.WispSway, material.GetFloat("_Sway"), 1e-4f, "the wisps do not sway");
            Assert.AreEqual(1.0f, material.GetFloat("_EdgeFeather"), 1e-4f, "the wisps' sides are not feathered: they would show as paper");
            Assert.AreEqual(0.0f, Prefab().transform.Find(BodySurface.SkinName).GetComponent<Renderer>().sharedMaterial.GetFloat("_Sway"), 1e-4f,
                "the body sways like a wisp");
        }

        [Test]
        public void EverySurfaceIsTheGhostsSeeThroughGlowThrowingNoShadowAndItGlows()
        {
            GameObject prefab = Prefab();
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(3, renderers.Length, "the Wraith is not a body, a skull and wisps");
            foreach (Renderer renderer in renderers)
            {
                Assert.AreEqual(WraithBuilder.ShaderName, renderer.sharedMaterial.shader.name, $"{renderer.name} is not on the ghost shader");
                Assert.GreaterOrEqual(renderer.sharedMaterial.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    $"{renderer.name} draws with the opaque world: the cel lines would outline it");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, $"{renderer.name} throws a shadow");
            }
            var wraith = prefab.GetComponent<Wraith>();
            Assert.IsNotNull(wraith.Glow, "it has no glow");
            Assert.AreEqual(LightType.Point, wraith.Glow.type);
            Assert.IsNotNull(wraith.MainCollision, "nothing can hit it");
            Assert.AreEqual(3, wraith.Renderers.Length, "its fade and flash do not reach every surface");
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
            wraith.Tick(Wraith.FadeSeconds);
            Assert.AreEqual(1.0f, wraith.FadeProgress, 1e-4f, "it never fades away");
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
