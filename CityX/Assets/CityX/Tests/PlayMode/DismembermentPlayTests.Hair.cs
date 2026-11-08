using System.Collections;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The hair on a cut-off or burst head keeps the colour the player dyed it (owner, 2026-11-08: "when my player got decapitated the hair
    /// color changed on the decapitated head"): the dye is on the hair material's own block, which the copies used to leave behind, so they
    /// came out in the hair material's gold. Hair a hat covers is not copied onto the flying head through the hat.
    /// </summary>
    public sealed partial class DismembermentPlayTests
    {
        private static readonly Color Dye = new Color(0.12f, 0.3f, 0.75f);
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        [UnityTest]
        public IEnumerator ACutOffHeadKeepsItsHairDye()
        {
            GameObject body = Body(Vector3.zero);
            Dyed(body);
            yield return Stand(body);
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: true);
            GameObject head = body.GetComponent<Dismemberer>().LastPiece;
            Assert.IsNotNull(head, "the head was not cut off");
            Renderer hair = Named(head, CharacterHair.WornName);
            Assert.IsNotNull(hair, "the cut-off head has no hair");
            AssertDyed(hair);
        }

        [UnityTest]
        public IEnumerator ABurstHeadsHairKeepsItsDye()
        {
            GameObject body = Body(Vector3.zero);
            Dyed(body);
            yield return Stand(body);
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: false);
            int pieces = 0;
            foreach (GoreChunk chunk in Object.FindObjectsByType<GoreChunk>(FindObjectsSortMode.None))
            {
                Renderer hair = Named(chunk.gameObject, CharacterHair.WornName);
                if (hair != null)
                {
                    AssertDyed(hair);
                    pieces++;
                }
            }
            Assert.Greater(pieces, 0, "the head's hair did not break with it");
        }

        [UnityTest]
        public IEnumerator HairUnderAHatIsNotCopiedOntoTheCutOffHead()
        {
            GameObject body = Body(Vector3.zero);
            Dyed(body).Cover();
            yield return Stand(body);
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: true);
            GameObject head = body.GetComponent<Dismemberer>().LastPiece;
            Assert.IsNotNull(head, "the head was not cut off");
            Assert.IsNull(Named(head, CharacterHair.WornName), "hair the hat covered pokes out of the cut-off head");
        }

        /// <summary>Gives <paramref name="body"/> the baked hairstyle (the scene builder adds hair, the prefab has none), dyed <see cref="Dye"/>.</summary>
        private CharacterHair Dyed(GameObject body)
        {
#if UNITY_EDITOR
            Transform head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var hair = body.GetComponent<CharacterHair>() ?? body.AddComponent<CharacterHair>();
            hair.Head = head;
            hair.StyleMeshes = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset") };
            hair.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var save = CharacterSave.Create("Dyed", new Color(0.8f, 0.6f, 0.5f));
            save.HairStyle = CharacterHair.Hair1;
            save.HairColour = Dye;
            body.GetComponent<CharacterIdentity>().Apply(save);
            Assert.IsNotNull(hair.Worn, "setup: no hair on the head");
            AssertDyed(hair.Worn.GetComponent<Renderer>());
            return hair;
#else
            Assert.Ignore("needs the editor to load the hair.");
            return null;
#endif
        }

        private static void AssertDyed(Renderer hair)
        {
            var block = new MaterialPropertyBlock();
            hair.GetPropertyBlock(block, 0);
            Color drawn = block.isEmpty ? hair.sharedMaterials[0].GetColor(BaseColourId) : block.GetColor(BaseColourId);
            Assert.AreEqual(Dye.r, drawn.r, 0.01f, $"{hair.name} is not the colour the hair was dyed ({drawn})");
            Assert.AreEqual(Dye.g, drawn.g, 0.01f, $"{hair.name} is not the colour the hair was dyed ({drawn})");
            Assert.AreEqual(Dye.b, drawn.b, 0.01f, $"{hair.name} is not the colour the hair was dyed ({drawn})");
        }

        private static Renderer Named(GameObject root, string name)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == name)
                {
                    return renderer;
                }
            }
            return null;
        }
    }
}
