using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Worn hair kept out of its body's shirt (HairPush; owner, 2026-11-12: "is there a better fix for the shirt spikes stabbing through the shirt than them bending?"): a body
    /// wearing hair has its upper body's pieces sent to the shader, posed as drawn, until the hair comes off; the kick hair is rigid on the head again; other players' copies too.
    /// </summary>
    public sealed class HairPushPlayTests
    {
        private static readonly int PiecesA = Shader.PropertyToID("_HairPushA");
        private static readonly int PieceCount = Shader.PropertyToID("_HairPushCount");
        private static readonly int Heads = Shader.PropertyToID("_HairPushHeads");
        private static readonly int HeadCount = Shader.PropertyToID("_HairPushHeadCount");
        private static readonly int TuckRims = Shader.PropertyToID("_HairTuckRim");
        private static readonly int TuckUps = Shader.PropertyToID("_HairTuckUp");
        private static readonly int TuckForwards = Shader.PropertyToID("_HairTuckForward");

        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
        }

#if UNITY_EDITOR
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

        private CharacterHair Wearing(string style, Vector3 at)
        {
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            CharacterHair hair = PutHairOn(_player);
            hair.SetStyle(style);
            return hair;
        }
#endif

        [UnityTest]
        public IEnumerator WearingHairKeepsItOutOfThatBodyUntilTheBodyGoes()
        {
#if UNITY_EDITOR
            int before = HairPush.Count;
            Wearing(CharacterHair.KickHair, Vector3.zero);
            Assert.AreEqual(before + 1, HairPush.Count, "the body wearing hair is not one the hair is kept out of");
            Object.Destroy(_player);
            yield return null;
            Assert.AreEqual(before, HairPush.Count, "a body gone is still pushed against");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheBodysPiecesGoToTheShaderWhereItStandsWithRoomForTheShirt()
        {
#if UNITY_EDITOR
            Wearing(CharacterHair.KickHair, new Vector3(40.0f, 0.05f, -30.0f));
            yield return null;
            HairPush.Upload(null);
            int sent = (int)Shader.GetGlobalFloat(PieceCount);
            Assert.AreEqual(Mathf.Min(HairPush.Count, HairPush.MaxBodies) * HairPush.PiecesPerBody, sent, "not every body near enough had its pieces sent");
            Vector3 hips = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
            Vector4[] starts = Shader.GetGlobalVectorArray(PiecesA);
            bool torso = false;
            for (int i = 0; i < sent; i++)
            {
                if (Vector3.Distance(starts[i], hips) < 1e-3f)
                {
                    torso = true;
                    Assert.AreEqual(FusedBodyShape.TorsoRadius + HairPush.ShirtRoom, starts[i].w, 1e-4f, "the torso's piece is not the torso and the shirt's room");
                }
            }
            Assert.IsTrue(torso, "the torso was not sent where the body stands");

            // Its head too, which hair pressed into a board on the back may not go into.
            Assert.AreEqual(Mathf.Min(HairPush.Count, HairPush.MaxBodies), (int)Shader.GetGlobalFloat(HeadCount), "not every body's head was sent");
            Vector3 head = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
            Vector4 headSent = System.Array.Find(Shader.GetGlobalVectorArray(Heads), h => Vector3.Distance(h, head) < 1e-3f);
            Assert.AreEqual(FusedBodyShape.HeadRadius + HairPush.ScalpRoom, headSent.w, 1e-4f, "the head was not sent where it is, as big as it is with room off the scalp");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheKickHairSitsRigidOnTheOwnersFit()
        {
#if UNITY_EDITOR
            CharacterHair hair = Wearing(CharacterHair.KickHair, Vector3.zero);
            yield return null;
            Assert.IsNull(hair.Worn.GetComponent<SkinnedMeshRenderer>(), "the kick hair still bends at the nape");
            Assert.IsNotNull(hair.Worn.GetComponent<MeshRenderer>(), "the kick hair is not drawn");
            Assert.AreEqual(hair.Head, hair.Worn.transform.parent, "the kick hair is not on the head");
            Assert.AreEqual(new Vector3(0.0f, -2.27f, 0.0f), hair.Worn.transform.localPosition, "the owner's fit moved");
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// The Midnight Coat's fur collar has the hair tucked under it (owner, 2026-11-15: "make it so the kick hair doesnt clip through the midnight coat"): its rim
        /// goes to the shader round the lower head, behind the neck, facing up and forward as the body does; until the coat comes off. A T-shirt tucks nothing in
        /// (the hair lies over it as it did). On another player's copy wearing it too.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCoatsCollarTucksTheHairInOnTheBodyAndOnACopyAndTheShirtsDoesNot()
        {
#if UNITY_EDITOR
            Wearing(CharacterHair.KickHair, new Vector3(-40.0f, 0.05f, 30.0f));
            Animator body = _player.GetComponentInChildren<Animator>();
            GameObject Top(string path) => Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3(60.0f, 0.5f, 60.0f), Quaternion.identity);

            var shirt = Top("Assets/CityX/Wearables/TShirt.prefab").GetComponent<CityX.Items.IEquippableItem>();
            shirt.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            yield return null;
            Assert.IsFalse(HairPush.Tucks(body), "the T-shirt tucks the hair in");
            shirt.OnUnequip(_player);

            var coat = Top("Assets/CityX/Wearables/MidnightCoat.prefab").GetComponent<CityX.Items.IEquippableItem>();
            coat.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            yield return null;
            Assert.IsTrue(HairPush.Tucks(body), "the coat's collar does not tuck the hair in");
            HairPush.Upload(null);
            Transform neck = body.GetBoneTransform(HumanBodyBones.Neck), head = body.GetBoneTransform(HumanBodyBones.Head);
            Vector4[] rims = Shader.GetGlobalVectorArray(TuckRims), ups = Shader.GetGlobalVectorArray(TuckUps), forwards = Shader.GetGlobalVectorArray(TuckForwards);
            int at = System.Array.FindIndex(rims, r => r.w > 0.5f && Vector3.Distance(r, head.position) < 1.0f);
            Assert.GreaterOrEqual(at, 0, "the coat's collar was not sent where the body stands");
            Vector3 rim = rims[at];
            Assert.Greater(rim.y, neck.position.y, "the collar's rim is under the neck");
            Assert.Less(rim.y, head.position.y, "the collar's rim is over the middle of the head");
            Assert.Greater(Vector3.Dot(((Vector3)ups[at]).normalized, Vector3.up), 0.8f, "up out of the collar is not up");
            Assert.Greater(Vector3.Dot(((Vector3)forwards[at]).normalized, _player.transform.forward), 0.8f, "the collar's front is not the body's");

            coat.OnUnequip(_player);
            yield return null;
            Assert.IsFalse(HairPush.Tucks(body), "the hair is still tucked in with the coat off");

            // Another player's copy wears it the same way (put on as the network puts it on a copy).
            var copy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(-50.0f, 0.05f, 30.0f), Quaternion.identity);
            copy.GetComponent<PlayerInputReader>().enabled = false;
            try
            {
                var copyCoat = Top("Assets/CityX/Wearables/MidnightCoat.prefab").GetComponent<CityX.Items.ICosmeticMountable>();
                copyCoat.MountCosmetic(copy);
                yield return null;
                Assert.IsTrue(HairPush.Tucks(copy.GetComponentInChildren<Animator>()), "another player's copy wearing the coat does not tuck its hair in");
                copyCoat.UnmountCosmetic(copy);
                Object.Destroy(((Component)copyCoat).gameObject);
            }
            finally
            {
                Object.Destroy(copy);
            }
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyIsKeptOutOfItsShirtToo()
        {
#if UNITY_EDITOR
            // Every screen pushes the hair out of the bodies it draws: a copy wears CharacterHair, on a human body, with the pushed material.
            var copy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            var hair = copy.GetComponentInChildren<CharacterHair>(true);
            Assert.IsNotNull(hair, "other players' copies wear no hair");
            Assert.IsTrue(hair.HairMaterial.IsKeywordEnabled(HairPush.Keyword), "other players' copies wear hair that is not pushed");
            Assert.IsTrue(copy.GetComponentInChildren<Animator>(true).isHuman, "a copy's body has no bones to push against");
            yield return null;
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }
    }

    /// <summary>
    /// Not a test: the kick hair over a T-shirt from behind through the big movements and with the head tipped back, each frame without the push and with it side by side.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.HairPushCapture. Output: Logs/CityShots/hair-push-sheet.png.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HairPushCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Cell = 300;
        private const int Shots = 4;
        private const float HeadBack = -30.0f;

        [UnityTest]
        public IEnumerator RenderThePush() => RenderOver("Assets/CityX/Wearables/TShirt.prefab", "hair-push-sheet.png");

        /// <summary>The same over the Midnight Coat, its fur collar and back standing well off the body (owner, 2026-11-15: "the back spikes are clipping through"): hair-push-coat-sheet.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePushOverTheCoat() => RenderOver("Assets/CityX/Wearables/MidnightCoat.prefab", "hair-push-coat-sheet.png");

        private static IEnumerator RenderOver(string topPath, string sheetName)
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
            CharacterHair hair = HairPushPlayTests.PutHairOn(player);
            hair.SetStyle(CharacterHair.KickHair);
            GameObject shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(topPath), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            shirt.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
            var worn = hair.Worn.GetComponent<Renderer>();
            Material pushed = hair.HairMaterial;
            var through = new Material(pushed);
            through.DisableKeyword(HairPush.Keyword);

            var rows = new (string name, Vector2 move, bool sprint, string state, bool headBack)[]
            {
                ("idle", Vector2.zero, false, null, false),
                ("head back", Vector2.zero, false, null, true),
                ("walk", Vector2.up, false, null, false),
                ("back", Vector2.down, false, null, false),
                ("sprint", Vector2.up, true, null, false),
                ("punch", Vector2.zero, false, FistStrikes.OverhandName, false),
            };
            var sheet = new Texture2D(Cell * Shots * 2, Cell * rows.Length, TextureFormat.RGB24, false);
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
                for (int frame = 0; frame < Shots * 6; frame++)
                {
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (frame % 6 != 0)
                    {
                        continue;
                    }
                    if (rows[row].headBack)
                    {
                        hair.Head.localRotation *= Quaternion.Euler(HeadBack, 0.0f, 0.0f);   // after the animation, until it poses the head again next frame
                    }
                    Vector3 at = player.transform.position + Vector3.up * 2.0f;
                    Vector3 from = at - player.transform.forward * 3.2f + player.transform.right * 0.5f + Vector3.up * 0.7f;
                    camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                    int y = (rows.Length - 1 - row) * Cell;
                    worn.sharedMaterial = through;
                    Shoot(camera, sheet, frame / 6 * 2 * Cell, y);
                    worn.sharedMaterial = pushed;
                    Shoot(camera, sheet, (frame / 6 * 2 + 1) * Cell, y);
                }
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, sheetName), (byte[])encode.Invoke(null, new object[] { sheet }));

            // Standing, the head from either side and in front, close: what the push leaves of the hair round the face and over the ears.
            for (int i = 0; i < 45; i++)
            {
                locomotion.Tick(new LocomotionInput(), Dt);
                yield return null;
            }
            Vector3 head = hair.Head.position;
            string stem = Path.GetFileNameWithoutExtension(sheetName);
            foreach ((string side, Vector3 from) in new[] { ("left", Vector3.left), ("right", Vector3.right), ("front", Vector3.forward), ("back-high", new Vector3(0.3f, 0.6f, -1.0f)) })
            {
                Vector3 eye = head + player.transform.rotation * from.normalized * 2.4f;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(head - eye));
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{stem}-{side}", eye, head + Vector3.down * 0.15f);
                worn.sharedMaterial = through;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{stem}-{side}-unpushed", eye, head + Vector3.down * 0.15f);
                worn.sharedMaterial = pushed;
            }
            foreach (GameObject thing in new[] { ground, sun.gameObject, camera.gameObject, player }) Object.Destroy(thing);
            Object.Destroy(sheet);
            Object.Destroy(through);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            yield return null;
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static void Shoot(Camera camera, Texture2D sheet, int x, int y)
        {
            RenderTexture target = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, Cell, Cell), x, y);
            RenderTexture.active = null;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
        }
#endif
    }
}
