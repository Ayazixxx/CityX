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
        public void TheGhostIsSeeThroughItsFaceSolidAndNothingThrowsAShadow()
        {
            GameObject prefab = Prefab();
            var wraith = prefab.GetComponent<Wraith>();
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(7, renderers.Length, "the Wraith is not a body, a skull, wisps, two eyes, a mouth and teeth");
            foreach (Renderer renderer in renderers)
            {
                bool face = renderer.transform.IsChildOf(wraith.Face);
                Assert.AreEqual(!face, renderer.sharedMaterial.shader.name == WraithBuilder.ShaderName,
                    face ? $"{renderer.name} is smoke, not solid" : $"{renderer.name} is not on the ghost shader");
                Assert.AreEqual(!face, renderer.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    face ? $"{renderer.name} is see-through" : $"{renderer.name} draws with the opaque world: the cel lines would outline it");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, $"{renderer.name} throws a shadow");
            }
            Assert.IsNotNull(wraith.Glow, "it has no glow");
            Assert.AreEqual(LightType.Point, wraith.Glow.type);
            Assert.IsNotNull(wraith.MainCollision, "nothing can hit it");
            Assert.AreEqual(3, wraith.Renderers.Length, "its fade and flash do not reach every surface");
        }

        /// <summary>
        /// Owner, 2026-10-26: "glowing red eyes and 1 row of solid white sharp teeth ... in the exact or similar placement to the image",
        /// then "eyes dont need to glow just make them solid red". Measured off the picture: the eyes high and wide on the skull, the row of
        /// nine teeth hanging from a lip just below its centre, the middle ones to its chin, under a jaw pivot at the lip's middle.
        /// </summary>
        [Test]
        public void ItHasSolidRedEyesAndARowOfSharpTeethPlacedLikeTheOwnersPicture()
        {
            var wraith = Prefab().GetComponent<Wraith>();
            Transform skull = wraith.Head.Find(BodySurface.HeadName);
            Assert.IsNotNull(wraith.Face, "it has no face");
            Assert.IsTrue(wraith.Face.IsChildOf(skull), "the face is not on the skull");
            float radius = FusedBodyShape.HeadRadius;
            foreach ((string name, float side) in new[] { (WraithBuilder.EyeLeftName, -1.0f), (WraithBuilder.EyeRightName, 1.0f) })
            {
                Transform eye = wraith.Face.Find(name);
                Assert.IsNotNull(eye, $"no {name}");
                Vector3 at = eye.localPosition;
                Assert.AreEqual(side * WraithBuilder.EyeOffsetX, at.x, 0.015f, $"{name} is not out to its side like the picture");
                Assert.AreEqual(WraithBuilder.EyeY, at.y, 0.01f, $"{name} is not high on the skull like the picture");
                Assert.Greater(at.z, 0.2f, $"{name} is not on the skull's front");
                Assert.AreEqual(radius - WraithBuilder.EyeSink, at.magnitude, 1e-3f, $"{name} is not sunk into the skull's surface");
                Material material = eye.GetComponent<MeshRenderer>().sharedMaterial;
                Color colour = material.GetColor("_BaseColor");
                Assert.Greater(colour.r, 0.6f, $"{name} is not red");
                Assert.Less(Mathf.Max(colour.g, colour.b), 0.15f, $"{name} is not red");
                Assert.IsFalse(material.IsKeywordEnabled("_EMISSION"), $"{name} glows: the owner wants it solid red");
                Assert.AreEqual(WraithBuilder.EyeRadius, eye.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x, 1e-3f, $"{name} is the wrong size");
            }

            Transform lip = wraith.Face.Find(WraithBuilder.LipName);
            Assert.IsNotNull(lip, "it has no mouth");
            Mesh mouth = lip.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mouth.bounds.max.y, WraithBuilder.LipY + WraithBuilder.ToothBaseRadius, "the mouth's line is hidden behind the teeth's roots");
            AssertFacesOut(mouth, "the mouth");
            AssertFacesOut(wraith.Face.Find(WraithBuilder.EyeLeftName).GetComponent<MeshFilter>().sharedMesh, "an eye");
            Assert.IsNotNull(wraith.Jaw, "it has no jaw for the bite to come");
            Assert.AreEqual(new Vector3(0.0f, WraithBuilder.LipY, WraithBuilder.LipRadius(radius)), wraith.Jaw.localPosition,
                "the jaw does not pivot at the middle of the lip");
            Transform teeth = wraith.Jaw.Find(WraithBuilder.TeethName);
            Assert.IsNotNull(teeth, "the teeth are not on the jaw");
            Mesh mesh = teeth.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(WraithBuilder.TeethCount * (WraithBuilder.ToothSides * 2 + 1), mesh.vertexCount, "not one row of nine teeth");
            Bounds row = mesh.bounds;   // in the skull's frame: the teeth are made there and the mesh offset back by the jaw's place
            Assert.AreEqual(WraithBuilder.LipY, row.max.y, WraithBuilder.ToothBaseRadius + 0.01f, "the teeth do not root at the lip");
            Assert.Less(row.min.y, WraithBuilder.LipY - WraithBuilder.ToothLengthMiddle * 0.9f, "the middle teeth do not reach the chin");
            Assert.Greater(row.min.y, -radius - 0.08f, "the teeth hang far below the chin");
            float reach = WraithBuilder.LipRadius(radius) * Mathf.Sin(WraithBuilder.TeethHalfAngleDegrees * Mathf.Deg2Rad);
            Assert.AreEqual(reach, row.max.x, 0.06f, "the row does not reach as far round as the picture's");
            Assert.AreEqual(-reach, row.min.x, 0.06f, "the row does not reach as far round as the picture's");
            Assert.Greater(row.min.z, 0.1f, "a tooth is inside or behind the skull");
            Color white = teeth.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.Greater(Mathf.Min(white.r, white.g, white.b), 0.85f, "the teeth are not white");
            Assert.Greater(WraithBuilder.ToothLength(0.0f), WraithBuilder.ToothLength(1.0f) * 1.5f, "the middle teeth are not the long ones");
        }

        /// <summary>
        /// Every triangle winds the way its normals point (Unity's front face: clockwise seen from outside), so a solid part is drawn, not
        /// culled as its own back: the mouth was wound inward and never showed.
        /// </summary>
        private static void AssertFacesOut(Mesh mesh, string what)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 wound = Vector3.Cross(b - a, c - a);
                Assert.Greater(wound.magnitude, 1e-9f, $"{what} has a triangle with no area");
                Vector3 outward = normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]];
                Assert.Greater(Vector3.Dot(wound, outward), 0.0f, $"{what} has a face wound inward: it is culled and never drawn");
            }
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
