using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The wet streets' mirror, rendered (docs/WET_STREETS.md; WetMirrorFeature, CityXWetMirror.hlsl): a white block standing on the city's own
    /// ground under a dark sky, looked at along the ground. Wet, the block shows in the ground in front of it; dry, or indoors, or on bare earth,
    /// or with the effect switched off, it does not. Needs a graphics device (-Graphics).
    /// </summary>
    public sealed class WetMirrorPlayTests
    {
        private const int Width = 320;
        private const int Height = 200;
        private const float Shows = 0.05f;      // this much more light in the ground (linear) is the block mirrored in it
        private const float Same = 0.01f;       // this much is nothing
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        private static readonly int FacadeWetnessId = Shader.PropertyToID("_CityXWetness");
        private static readonly int HazeId = Shader.PropertyToID("_CityXHazeParams");

        private readonly List<Object> _made = new List<Object>();
        private Camera _camera;
        private RenderTexture _target;
        private ScriptableRendererFeature _mirror;
        private bool _mirrorWas, _fogWas;
        private AmbientMode _ambientModeWas;
        private Color _ambientWas;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("rendering needs a graphics device -- run with -Graphics");
            }
#if UNITY_EDITOR
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            _mirror = data.rendererFeatures.Find(f => f != null && f.name == GraphicsQuality.ReflectionsFeature);
            Assert.IsNotNull(_mirror, $"no {GraphicsQuality.ReflectionsFeature} on {RendererPath}: rebuild the scene");
            _mirrorWas = _mirror.isActive;
#else
            Assert.Ignore("needs the editor to load the renderer.");
#endif
            _fogWas = RenderSettings.fog;
            _ambientModeWas = RenderSettings.ambientMode;
            _ambientWas = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            Shader.SetGlobalVector(HazeId, Vector4.zero);
        }

        [TearDown]
        public void TearDown()
        {
            CurrentWeather.Wetness = 0.0f;
            Shader.SetGlobalFloat(FacadeWetnessId, 0.0f);
            WetMirrorFeature.Clear();
            if (_mirror != null)
            {
                _mirror.SetActive(_mirrorWas);
            }
            RenderSettings.fog = _fogWas;
            RenderSettings.ambientMode = _ambientModeWas;
            RenderSettings.ambientLight = _ambientWas;
            foreach (Object made in _made)
            {
                if (made != null)
                {
                    Object.Destroy(made);
                }
            }
            _made.Clear();
        }

        private static Material CityMaterial()
        {
#if UNITY_EDITOR
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityConcrete.mat");
            Assert.IsNotNull(material, "no CityConcrete.mat");
            return material;
#else
            return null;
#endif
        }

        /// <summary>The ground, its surface <paramref name="look"/> (SurfaceLook), and the camera looking along it at where the block stands.</summary>
        private void BuildGround(Color32? look, Color sky)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(ground);
            ground.transform.position = new Vector3(0.0f, -0.5f, 40.0f);
            ground.transform.localScale = new Vector3(240.0f, 1.0f, 240.0f);
            ground.GetComponent<Renderer>().sharedMaterial = CityMaterial();
            if (look.HasValue)
            {
                MeshFilter filter = ground.GetComponent<MeshFilter>();
                Mesh painted = Object.Instantiate(filter.sharedMesh);
                var colours = new Color32[painted.vertexCount];
                for (int i = 0; i < colours.Length; i++)
                {
                    colours[i] = look.Value;
                }
                painted.colors32 = colours;
                filter.sharedMesh = painted;
                _made.Add(painted);
            }

            _camera = new GameObject("WetMirrorCamera").AddComponent<Camera>();
            _made.Add(_camera.gameObject);
            _camera.tag = "MainCamera";
            // Drawn as the game's camera is, through its post-processing: a camera without it stores its picture the other way up.
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = sky;
            _camera.fieldOfView = 40.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 500.0f;
            _camera.transform.position = new Vector3(0.0f, 2.3f, 0.0f);
            _camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, -1.0f, 30.0f));
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;
            _made.Add(_target);
        }

        private void BuildBlock()
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(block);
            block.transform.position = new Vector3(0.0f, 5.0f, 30.0f);
            block.transform.localScale = new Vector3(14.0f, 10.0f, 2.0f);
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            _made.Add(white);
            block.GetComponent<Renderer>().sharedMaterial = white;
        }

        private static void Wet(float wetness)
        {
            CurrentWeather.Wetness = wetness;
            Shader.SetGlobalFloat(FacadeWetnessId, wetness);
        }

        /// <summary>The picture, in linear light, a row at a time from the bottom. Drawn twice: the mirror shows the frame before.</summary>
        private float[] Rows(out float middleOfTheGround)
        {
            _camera.Render();
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Color[] pixels = image.GetPixels();
            Object.Destroy(image);

            var rows = new float[Height];
            for (int y = 0; y < Height; y++)
            {
                float sum = 0.0f;
                for (int x = Width / 2 - 20; x < Width / 2 + 20; x++)
                {
                    sum += Mathf.GammaToLinearSpace(pixels[y * Width + x].g);
                }
                rows[y] = sum / 40.0f;
            }
            // The ground just in front of the block's foot, where its mirror image stands.
            middleOfTheGround = 0.0f;
            for (int y = Height / 4; y < Height / 2 - 18; y++)
            {
                middleOfTheGround = Mathf.Max(middleOfTheGround, rows[y]);
            }
            return rows;
        }

        private float GroundWith(bool mirror, float wetness)
        {
            _mirror.SetActive(mirror);
            if (!mirror)
            {
                WetMirrorFeature.Clear();
            }
            Wet(wetness);
            Rows(out float ground);
            return ground;
        }

        [UnityTest]
        public IEnumerator AWetStreetShowsWhatStandsOnIt()
        {
            BuildGround(null, Color.black);
            BuildBlock();
            yield return null;
            float off = GroundWith(false, 1.0f);
            float on = GroundWith(true, 1.0f);
            Assert.Greater(on, off + Shows, $"the white block does not show in the wet ground ({on:0.000} with the mirror, {off:0.000} without)");
        }

        [UnityTest]
        public IEnumerator ADryStreetShowsNothingAndKeepsNoPicture()
        {
            BuildGround(null, Color.black);
            BuildBlock();
            yield return null;
            float off = GroundWith(false, 0.0f);
            float on = GroundWith(true, 0.0f);
            Assert.AreEqual(off, on, Same, "a dry street mirrors the city");
            Assert.AreEqual(0.0f, Shader.GetGlobalVector("_CityXMirror").x, 1e-4f, "a picture is being kept on a dry day");
        }

        [UnityTest]
        public IEnumerator AFloorIndoorsStaysDryInTheRain()
        {
            BuildGround(SurfaceLook.Pack(Color.white, 0, 0.0f, true), Color.black);
            BuildBlock();
            yield return null;
            float dry = GroundWith(true, 0.0f);
            float wet = GroundWith(true, 1.0f);
            Assert.AreEqual(dry, wet, Same, "it rains on the floor of a room");
        }

        [UnityTest]
        public IEnumerator BareEarthDoesNotMirror()
        {
            const int earth = 10;   // the wall materials' slice (kTileMetres, CITYX_EARTH_SLICE)
            BuildGround(SurfaceLook.Pack(Color.white, earth, 0.0f), Color.black);
            BuildBlock();
            yield return null;
            float off = GroundWith(false, 1.0f);
            float on = GroundWith(true, 1.0f);
            Assert.AreEqual(off, on, Same, "mud mirrors the city like a puddle");
        }
    }
}
