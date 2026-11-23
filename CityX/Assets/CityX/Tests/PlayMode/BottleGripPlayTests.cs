using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The hand round a bottle's neck (owner, 2026-11-23: "the thumb is clipping into the soda bottle instead of gripping around it"): carried,
    /// no part of the thumb or the fingers goes into the glass, measured on the body's own skin as it is drawn (baked from the skinned mesh)
    /// against the bottle's outline, for every kind of bottle.
    /// </summary>
    public sealed class BottleGripPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>How far the skin may sink into the glass and not be seen to: the black line covers a centimetre at the game camera.</summary>
        private const float MostSunk = 0.012f;

        private static IEnumerable<string> Kinds()
        {
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                yield return kind.ItemId;
            }
        }

        /// <summary>The bottle's radius at a height above its base (a real bottle's metres): its profile, or its body, shoulder and neck for one with a model of its own.</summary>
        private static float RadiusAt(BottleShape shape, float height)
        {
            if (height < 0.0f || height > shape.Height)
            {
                return 0.0f;
            }
            if (shape.Profile != null)
            {
                return BottleMesh.RadiusAt(shape.Profile, height, 0.006f);
            }
            if (height <= shape.BodyTop)
            {
                return shape.BodyRadius;
            }
            return height >= shape.NeckBase ? shape.NeckRadius
                : Mathf.Lerp(shape.BodyRadius, shape.NeckRadius, Mathf.SmoothStep(0.0f, 1.0f, (height - shape.BodyTop) / (shape.NeckBase - shape.BodyTop)));
        }

        [UnityTest]
        public IEnumerator CarriedTheHandClosesRoundTheNeckWithoutGoingIntoTheGlass([ValueSource(nameof(Kinds))] string itemId)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                var bottle = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GlassBottleKinds.PrefabPath(itemId)),
                    new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<BottleWeapon>();
                bottle.OnEquip(body, ItemInstanceData.None);
                var locomotion = body.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 50; i++)
                {
                    body.GetComponent<SoulsSwordCombat>()?.Tick(Dt);
                    locomotion.Tick(new LocomotionInput(), Dt);
                    yield return null;
                }
                Assert.Greater(locomotion.RightGrip, 0.9f, "the fingers are not closed on the neck yet");

                var skin = TransformSearch.FindDeep(body.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
                var posed = new Mesh();
                skin.BakeMesh(posed, true);
                Vector3[] points = posed.vertices;
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                Transform[] bones = skin.bones;
                BottleShape shape = BottleShape.For(itemId);
                var deepest = new Dictionary<string, float>();
                var where = new Dictionary<string, float>();
                for (int i = 0; i < points.Length; i++)
                {
                    BoneWeight w = weights[i];
                    string bone = bones[w.boneIndex0] != null ? bones[w.boneIndex0].name : string.Empty;
                    if (!bone.EndsWith("_r") || !(bone.StartsWith("thumb") || bone.StartsWith("index") || bone.StartsWith("middle")
                        || bone.StartsWith("ring") || bone.StartsWith("little") || bone == "hand_r"))
                    {
                        continue;
                    }
                    // Into the bottle's own frame: the fist on the neck at the origin, the base out along +Y, a real bottle's metres.
                    Vector3 held = bottle.transform.InverseTransformPoint(skin.transform.TransformPoint(points[i])) / shape.Scale;
                    float height = shape.GripHeight - held.y;
                    float sunk = RadiusAt(shape, height) - new Vector2(held.x, held.z).magnitude;
                    string digit = bone.StartsWith("thumb") ? "thumb" : bone == "hand_r" ? "palm" : "fingers";
                    if (!deepest.TryGetValue(digit, out float was) || sunk > was)
                    {
                        deepest[digit] = sunk;
                        where[digit] = height;
                    }
                }
                Object.Destroy(posed);
                string report = string.Empty;
                foreach (KeyValuePair<string, float> pair in deepest)
                {
                    report += $"{pair.Key}: {pair.Value * shape.Scale * 100.0f:F1} cm into the glass at {where[pair.Key] * 100.0f:F1} cm up (neck from {shape.NeckBase * 100.0f:F1}); ";
                }
                Debug.Log($"BOTTLE GRIP {itemId}: {report}");
                foreach (KeyValuePair<string, float> pair in deepest)
                {
                    Assert.Less(pair.Value * shape.Scale, MostSunk, $"{itemId}: the {pair.Key} goes into the glass. {report}");
                }
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }
    }
}
