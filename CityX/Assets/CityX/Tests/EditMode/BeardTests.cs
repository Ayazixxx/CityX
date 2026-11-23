using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The beards (docs/BEARDS.md): every one has its mesh and stays close on the head, so no collar meets it; the two full beards are one smooth
    /// skin with no hard edge the game's lines would draw; a body puts one on and takes it off; a save keeps it.
    /// </summary>
    public sealed class BeardTests
    {
        /// <summary>The furthest a beard stands off the head: "not much farther out then the head" (owner, 2026-11-22).</summary>
        private const float FurthestOffTheHead = 0.03f;

        private static IEnumerable<string> Beards()
        {
            foreach (string beard in CharacterHair.ValidBeards)
            {
                if (beard != CharacterHair.None)
                {
                    yield return beard;
                }
            }
        }

        [Test]
        public void CleanShavenComesFirstAndHasNoMesh()
        {
            Assert.AreEqual(CharacterHair.None, CharacterHair.ValidBeards[0]);
            Assert.IsNull(CharacterHair.BeardMesh(CharacterHair.None));
            Assert.IsNull(CharacterHair.BeardMesh("not a beard"));
        }

        [TestCaseSource(nameof(Beards))]
        public void ABeardHasItsMeshAndStaysCloseOnTheHead(string beard)
        {
            Mesh mesh = CharacterHair.BeardMesh(beard);
            Assert.IsNotNull(mesh, $"{beard} has no mesh in Resources/{CharacterHair.BeardResourceFolder}: run CityX/Bake Beards");
            Assert.Greater(mesh.triangles.Length / 3, 100, "hardly a beard");
            Assert.Less(mesh.triangles.Length / 3, 3000, "a beard is no more than a hairstyle's triangles");
            float furthest = 0.0f, lowest = 0.0f;
            foreach (Vector3 vertex in mesh.vertices)
            {
                furthest = Mathf.Max(furthest, vertex.magnitude);
                lowest = Mathf.Min(lowest, vertex.y);
            }
            Assert.Less(furthest, FusedBodyShape.HeadRadius + FurthestOffTheHead, "it stands too far off the head");
            Assert.Greater(lowest, -FusedBodyShape.HeadRadius - 0.01f, "it hangs below the head, into a collar");
            Assert.Greater(mesh.bounds.center.z, 0.0f, "a beard is on the face's side of the head");
            Assert.Less(Mathf.Abs(mesh.bounds.center.x), 0.01f, "it is not the same on both sides");
        }

        [TestCase(CharacterHair.FullBeard)]
        [TestCase(CharacterHair.SpikyBeard)]
        public void AFullBeardIsOneSmoothSkin(string beard)
        {
            // The lines are drawn where the surface turns sharply. The skin faces out from the head everywhere, so nothing is drawn on it (owner:
            // "have no visible creases or lines"); only its rim's skirt lies flat along the head, a quarter turn from the skin, so ONE line is
            // drawn round the beard as round the hair ("how can we get the black line on the edge of the beard like the hair above it").
            Mesh mesh = CharacterHair.BeardMesh(beard);
            Assert.IsNotNull(mesh);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            var rim = new HashSet<Vector3Int>();
            int skin = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                float outward = Vector3.Dot(normals[i], vertices[i].normalized);
                // The skin slopes down to its low rim, gently: well under the 45 degrees a line is drawn at.
                if (outward > 0.75f)
                {
                    skin++;
                }
                else
                {
                    Assert.Less(Mathf.Abs(outward), 0.05f, $"{beard} has a crease at {vertices[i]}: neither the skin nor its rim");
                    rim.Add(Vector3Int.RoundToInt(vertices[i] * 20000.0f));
                }
            }
            Assert.Greater(rim.Count, 100, "the beard has no hard rim for its line");
            Assert.Greater(skin, rim.Count / 2, "hardly any skin");
        }

        [Test]
        public void TheSpikyBeardsEdgeIsLongerThanTheCrispOnes()
        {
            // The same beard but for its edge: the spikes are in the outline, nowhere else.
            Bounds crisp = CharacterHair.BeardMesh(CharacterHair.FullBeard).bounds, spiky = CharacterHair.BeardMesh(CharacterHair.SpikyBeard).bounds;
            Assert.Less(Mathf.Abs(crisp.size.x - spiky.size.x), 0.03f, "the two full beards are not the same width");
            Assert.Greater(CharacterHair.BeardMesh(CharacterHair.SpikyBeard).vertexCount, CharacterHair.BeardMesh(CharacterHair.FullBeard).vertexCount);
        }

        [Test]
        public void ABodyPutsABeardOnInItsHairsColourAndShavesItOff()
        {
            var body = new GameObject("Body");
            var head = new GameObject("Head");
            head.transform.SetParent(body.transform, false);
            try
            {
                var hair = body.AddComponent<CharacterHair>();
                hair.Head = head.transform;
                Assert.AreEqual(CharacterHair.None, hair.Beard);
                Assert.IsNull(hair.BeardWorn);

                hair.SetBeard(CharacterHair.SpikyBeard);
                Assert.AreEqual(CharacterHair.SpikyBeard, hair.Beard);
                Assert.IsNotNull(hair.BeardWorn);
                Assert.AreSame(head.transform, hair.BeardWorn.parent, "the beard is not on the head");
                Assert.AreEqual(Vector3.zero, hair.BeardWorn.localPosition, "a beard is made in the head's own frame: it goes on as it is");
                Assert.AreSame(CharacterHair.BeardMesh(CharacterHair.SpikyBeard), hair.BeardWorn.GetComponent<MeshFilter>().sharedMesh);

                var red = new Color(0.8f, 0.1f, 0.1f);
                hair.SetColour(red);
                var block = new MaterialPropertyBlock();
                hair.BeardWorn.GetComponent<Renderer>().GetPropertyBlock(block, 0);
                Color worn = block.GetColor("_BaseColor");
                Assert.Less(Mathf.Abs(worn.r - red.r) + Mathf.Abs(worn.g - red.g) + Mathf.Abs(worn.b - red.b), 0.01f, "the beard is not the hair's colour");

                hair.SetBeard("not a beard");
                Assert.AreEqual(CharacterHair.SpikyBeard, hair.Beard, "a beard the game does not have was put on");

                hair.SetBeard(CharacterHair.FullBeard);
                Assert.AreEqual(1, head.transform.childCount, "the beard before was left on");
                hair.SetBeard(CharacterHair.None);
                Assert.IsNull(hair.BeardWorn);
                Assert.AreEqual(0, head.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ASaveKeepsTheBeardAndAnOldOneIsCleanShaven()
        {
            Assert.AreEqual(CharacterHair.None, new CharacterSave().Beard);
            var old = new CharacterSave { Beard = null };
            old.Repair();
            Assert.AreEqual(CharacterHair.None, old.Beard, "a save from before beards");
            var kept = JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(new CharacterSave { Beard = CharacterHair.FullBeard }));
            Assert.AreEqual(CharacterHair.FullBeard, kept.Beard);
        }

        [Test]
        public void TheFaceTabOffersEveryBeard()
        {
            var page = new GameObject("FacePage", typeof(RectTransform));
            try
            {
                FacePartChips chips = FacePage.BuildFrom(page.transform, FacePartCatalog.Current);
                Assert.AreEqual(FacePage.BeardChipLabel, chips.ChipLabels[chips.ChipLabels.Length - 1].text, "the last chip is the beards'");
                Assert.AreEqual(chips.Chips.Length, chips.Panels.Length);
                BeardTiles tiles = chips.Panels[chips.Panels.Length - 1].GetComponentInChildren<BeardTiles>(true);
                Assert.IsNotNull(tiles, "the beards' page has no tiles");
                Assert.AreEqual(CharacterHair.ValidBeards.Length, tiles.Frames.Length);
                for (int i = 0; i < tiles.Frames.Length; i++)
                {
                    string beard = CharacterHair.ValidBeards[i];
                    Assert.IsNotNull(tiles.Frames[i].GetComponent<Button>(), $"{beard} cannot be clicked");
                    var wiring = tiles.Frames[i].GetComponent<ButtonAction>();
                    Assert.AreEqual(ButtonAction.Kind.CreatorBeard, wiring.Action);
                    Assert.AreEqual(i, wiring.Value, $"{beard}'s tile puts another beard on");
                    Assert.AreEqual(CharacterHair.BeardLabelFor(beard), tiles.Frames[i].GetComponentInChildren<Text>(true).text);
                }
            }
            finally
            {
                Object.DestroyImmediate(page);
            }
        }

        [Test]
        public void EveryBeardHasAShortNameForItsTile()
        {
            var seen = new HashSet<string>();
            foreach (string beard in CharacterHair.ValidBeards)
            {
                string label = CharacterHair.BeardLabelFor(beard);
                Assert.IsNotEmpty(label);
                Assert.LessOrEqual(label.Length, 6, $"{label} does not fit a tile");
                Assert.IsTrue(seen.Add(label), $"two beards are called {label}");
            }
        }
    }
}
