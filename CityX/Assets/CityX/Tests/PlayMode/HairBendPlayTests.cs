using System.Collections;
using System.IO;
using System.Text;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The kick hair bending at the nape on the real body (owner, 2026-11-04: "the hair pokes through the shirt in the back"): it sits exactly on the owner's fit until the head moves
    /// off the neck, and then its lowest locks stay on the back while its top goes with the head. Other styles stay rigid, and it is dyed and hidden as any other.
    /// </summary>
    public sealed class HairBendPlayTests
    {
        private GameObject _player;
        private int _capturedFramerate;

        [SetUp]
        public void SetUp()
        {
            _capturedFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = _capturedFramerate;
            if (_player != null) Object.Destroy(_player);
        }

#if UNITY_EDITOR
        /// <summary>The body in its rest pose with nothing animating it, wearing <paramref name="style"/>.</summary>
        private CharacterHair Wearing(string style)
        {
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            CharacterHair hair = PutHairOn(_player);
            hair.SetStyle(style);
            return hair;
        }

        /// <summary>Player.prefab wears none of its own (the scene's builder gives it hair): the real meshes on its head bone.</summary>
        internal static CharacterHair PutHairOn(GameObject body)
        {
            var hair = body.AddComponent<CharacterHair>();
            hair.Head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            hair.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
            meshes[System.Array.IndexOf(CharacterHair.ValidStyles, CharacterHair.KickHair) - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/KickHairMesh.asset");
            meshes[System.Array.IndexOf(CharacterHair.ValidStyles, CharacterHair.Hair1) - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset");
            hair.StyleMeshes = meshes;
            return hair;
        }

        private static Vector3[] Baked(SkinnedMeshRenderer skinned)
        {
            var mesh = new Mesh();
            skinned.BakeMesh(mesh, true);   // in the renderer's own space, with its scale
            Vector3[] points = mesh.vertices;
            Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = toWorld.MultiplyPoint3x4(points[i]);
            }
            Object.Destroy(mesh);
            return points;
        }
#endif

        [UnityTest]
        public IEnumerator AtRestItSitsExactlyOnTheOwnersFit()
        {
#if UNITY_EDITOR
            CharacterHair hair = Wearing(CharacterHair.KickHair);
            _player.GetComponentInChildren<Animator>().enabled = false;
            var skinned = hair.Worn.GetComponent<SkinnedMeshRenderer>();
            Assert.IsNotNull(skinned, "the kick hair is not baked to bend");
            // The anchor takes the head's rest as lined up with the neck's: the rig must really be so.
            Assert.Less(Quaternion.Angle(hair.Head.localRotation, Quaternion.identity), 0.5f, "the head bone has a rest turn of its own under the neck");
            yield return null;
            CharacterHair.Fit fit = CharacterHair.CurrentFit(CharacterHair.KickHair, skinned.sharedMesh.bounds);
            Assert.AreEqual(new Vector3(0.0f, -2.27f, 0.0f), hair.Worn.localPosition, "the owner's fit moved");
            Assert.AreEqual(fit.Position, hair.Worn.localPosition);

            Vector3[] drawn = Baked(skinned);
            Vector3[] modelled = skinned.sharedMesh.vertices;
            float worst = 0.0f;
            for (int i = 0; i < drawn.Length; i++)
            {
                worst = Mathf.Max(worst, Vector3.Distance(drawn[i], hair.Worn.TransformPoint(modelled[i])));
            }
            Assert.Less(worst, 0.002f, "at rest the bend moved the hair off its fit");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TippingTheHeadBackLeavesTheLowestLocksOnTheBackAndTurnsTheTop()
        {
#if UNITY_EDITOR
            CharacterHair hair = Wearing(CharacterHair.KickHair);
            _player.GetComponentInChildren<Animator>().enabled = false;
            var skinned = hair.Worn.GetComponent<SkinnedMeshRenderer>();
            hair.Head.localRotation = Quaternion.identity;
            hair.Head.parent.localRotation = Quaternion.identity;
            yield return null;
            Vector3[] before = Baked(skinned);

            hair.Head.localRotation = Quaternion.Euler(-25.0f, 0.0f, 0.0f);
            yield return null;
            Vector3[] after = Baked(skinned);

            Vector3[] modelled = skinned.sharedMesh.vertices;
            BoneWeight[] weights = skinned.sharedMesh.boneWeights;
            float lowestMoved = 0.0f, crownMoved = 0.0f;
            float crown = float.MinValue;
            for (int i = 0; i < after.Length; i++)
            {
                crown = Mathf.Max(crown, modelled[i].y);
            }
            for (int i = 0; i < after.Length; i++)
            {
                float moved = Vector3.Distance(before[i], after[i]);
                bool allNeck = (weights[i].boneIndex0 == 1 && weights[i].weight0 > 0.999f);
                if (allNeck)
                {
                    lowestMoved = Mathf.Max(lowestMoved, moved);
                }
                if (modelled[i].y > crown - 0.03f)
                {
                    crownMoved = Mathf.Max(crownMoved, moved);
                }
            }
            Assert.Less(lowestMoved, 0.005f, "the lowest locks moved with the head: they will go into the shirt");
            Assert.Greater(crownMoved, 0.1f, "the top of the hair did not go with the head");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ItIsDyedAndHiddenAsAnyOtherAndOtherStylesStayRigid()
        {
#if UNITY_EDITOR
            CharacterHair hair = Wearing(CharacterHair.KickHair);
            var renderer = hair.Worn.GetComponent<Renderer>();
            hair.SetColour(Color.red);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Assert.AreEqual(Color.red, block.GetColor("_BaseColor"), "the dye did not reach the bending hair");
            hair.Cover();
            Assert.IsTrue(renderer.forceRenderingOff, "a helmet did not hide the bending hair");
            hair.Uncover();
            Assert.IsFalse(renderer.forceRenderingOff);

            Transform neck = hair.Head.parent;
            hair.SetStyle(CharacterHair.Hair1);
            yield return null;
            Assert.IsNotNull(hair.Worn.GetComponent<MeshRenderer>(), "another style lost its plain renderer");
            Assert.IsNull(neck.Find("HairNeckAnchor"), "the neck anchor was left behind by a change of style");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyBendsToo()
        {
#if UNITY_EDITOR
            // Read off the prefab: the same component and the same baked mesh as the player's, so a copy bends by the same neck, moved by the same synced animation.
            var hair = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponentInChildren<CharacterHair>(true);
            Assert.IsNotNull(hair, "other players' copies wear no hair");
            Mesh kick = hair.StyleMeshes[System.Array.IndexOf(CharacterHair.ValidStyles, CharacterHair.KickHair) - 1];
            Assert.AreEqual(HairBend.BoneCount, kick.bindposes.Length, "other players' copies wear it rigid");
            Assert.IsNotNull(hair.Head.parent, "a copy's head has no neck to bend from");
            yield return null;
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }
    }

    /// <summary>
    /// Not a test: how deep the kick hair's lowest locks go into the body through the big movements, rigid against bending, and the back view of each. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.HairBendCapture. Output: Logs/CityShots/hair-bend-sheet.png, and the depths in the log.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HairBendCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Cell = 300;

        [UnityTest]
        public IEnumerator RenderTheBend()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 26.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.nearClipPlane = 0.05f;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var driver = player.GetComponent<PlayerAnimationDriver>();
            CharacterHair hair = HairBendPlayTests.PutHairOn(player);
            hair.SetStyle(CharacterHair.KickHair);
            GameObject shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab"), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            shirt.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
            var skinned = hair.Worn.GetComponent<SkinnedMeshRenderer>();
            Mesh hairMesh = skinned.sharedMesh;
            Vector3[] modelled = hairMesh.vertices;
            BoneWeight[] weights = hairMesh.boneWeights;

            var rows = new (string name, Vector2 move, bool sprint, string state)[]
            {
                ("idle", Vector2.zero, false, null),
                ("walk", Vector2.up, false, null),
                ("back", Vector2.down, false, null),
                ("sprint", Vector2.up, true, null),
                ("punch", Vector2.zero, false, FistStrikes.OverhandName),
            };
            const int columns = 8;
            var sheet = new Texture2D(Cell * columns, Cell * rows.Length, TextureFormat.RGB24, false);
            var log = new StringBuilder("CityX hair bend: how far the tips sit behind the back of the neck's line, m (less means further into the shirt):\n");
            for (int row = 0; row < rows.Length; row++)
            {
                var input = new LocomotionInput { Move = rows[row].move, Sprint = rows[row].sprint };
                for (int i = 0; i < 45; i++)
                {
                    locomotion.Tick(input, Dt);
                    yield return null;
                }
                if (rows[row].state != null)
                {
                    driver.PlayLocalState(rows[row].state);
                }
                float rigidWorst = float.MaxValue, bentWorst = float.MaxValue;
                for (int frame = 0; frame < columns * 6; frame++)
                {
                    locomotion.Tick(input, Dt);
                    yield return null;
                    Matrix4x4 rigid = hair.Worn.localToWorldMatrix;
                    Transform neck = hair.Head.parent;
                    Matrix4x4 onNeck = neck.localToWorldMatrix * Matrix4x4.TRS(hair.Head.localPosition + hair.Worn.localPosition, hair.Worn.localRotation, hair.Worn.localScale);
                    Transform spine = neck.parent;
                    for (int i = 0; i < modelled.Length; i += 4)
                    {
                        if (weights[i].boneIndex0 != 1 || weights[i].weight0 < 0.999f || modelled[i].z > -0.1f || Mathf.Abs(modelled[i].x) > 0.2f)
                        {
                            continue;   // only the lowest locks down the middle of the back
                        }
                        rigidWorst = Mathf.Min(rigidWorst, Clearance(spine, rigid.MultiplyPoint3x4(modelled[i])));
                        bentWorst = Mathf.Min(bentWorst, Clearance(spine, onNeck.MultiplyPoint3x4(modelled[i])));
                    }
                    if (frame % 6 == 0)
                    {
                        Vector3 at = player.transform.position + Vector3.up * 2.0f;
                        Vector3 from = at - player.transform.forward * 3.2f + player.transform.right * 0.5f + Vector3.up * 0.7f;
                        camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                        RenderTexture target = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), frame / 6 * Cell, (rows.Length - 1 - row) * Cell);
                        RenderTexture.active = null;
                        camera.targetTexture = null;
                        RenderTexture.ReleaseTemporary(target);
                    }
                }
                log.AppendLine($"{rows[row].name}: rigid {rigidWorst:0.000}, bending {bentWorst:0.000}");
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "hair-bend-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Debug.Log(log.ToString());
            foreach (GameObject thing in new[] { ground, sun.gameObject, camera.gameObject, player }) Object.Destroy(thing);
            Object.Destroy(sheet);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            yield return null;
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>How far behind the upper back a point down the middle of it is, metres (negative: inside the body), the back taken as 0.26 m behind the spine bone.</summary>
        private static float Clearance(Transform spine, Vector3 point)
        {
            const float torsoRadius = 0.26f;
            return -spine.InverseTransformPoint(point).z - torsoRadius;
        }
#endif
    }
}
