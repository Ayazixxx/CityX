using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CityX.Core;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a light's marks do, rendered (CityLightLayers; docs/LIGHTING.md, docs/FLASHLIGHT.md, docs/SEWERS.md). URP drops a mark that is
    /// on a rendering layer the project does not name and says nothing, and a mark the shaders never get looks the same in every test that
    /// reads it back from the light: only a picture shows it. So a spot light at the eye shines square on a wall here, bare and then
    /// marked, and the wall says what reached it: a room's light never lights the street, the city's lamps never light a tunnel, and
    /// under the ground a carried beam (owner, 2026-11-09: "the flashlight/ other light sources need to light up the darkness") is as
    /// bright on a far wall as on a near one, where a bare light falls off with the square of the distance; under the sky it falls off
    /// as a bare light does. And with a pane of glass for the wall: a lamp shines on a window as an area, not as a disc (owner,
    /// 2026-11-09: "it keeps reflecting as circles instead of just an area of light"). Needs a graphics device (-Graphics).
    /// </summary>
    public sealed class LightMarksPlayTests
    {
        private const int Size = 64;
        private const int Middle = 8;            // the measure: this many pixels square in the middle of the picture
        private const float BareLight = 0.08f;   // what the bare light puts on the wall at every distance: clear of black and of white
        private const float Range = 45.0f;       // a torch's
        private const float Tolerance = 0.12f;   // of a ratio: eight-bit pixels
        private const float Lit = 0.004f;        // a wall with this much more light on it is lit
        private const float Unlit = 0.05f;       // a light that adds less than this share of a bare light's does not reach the wall
        private const float SideMetres = 12.0f;  // far enough that a room light's soft fitting (CITYX_FITTING_RADIUS_SQ) is no part of the measure

        // How far under the ground the eye is, as DayNightCycle tells the shaders: a body knows nothing else of where it is.
        private static readonly int EyeUnderground = Shader.PropertyToID("_CityXUnderground");

        private readonly List<Object> _made = new List<Object>();
        private bool _celWas, _fogWas;
        private AmbientMode _ambientModeWas;
        private Color _ambientWas;
        private Vector3 _sunWas;
        private Camera _camera;
        private RenderTexture _target;

        [SetUp]
        public void SetUp()
        {
            _celWas = CelShading.Enabled;
            _fogWas = RenderSettings.fog;
            _ambientModeWas = RenderSettings.ambientMode;
            _ambientWas = RenderSettings.ambientLight;
            _sunWas = CurrentSky.TowardSun;
            Shader.SetGlobalFloat(EyeUnderground, 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalFloat(EyeUnderground, 0.0f);
            CelShading.Set(_celWas);
            RenderSettings.fog = _fogWas;
            RenderSettings.ambientMode = _ambientModeWas;
            RenderSettings.ambientLight = _ambientWas;
            CurrentSky.TowardSun = _sunWas;
            foreach (Object o in _made)
            {
                if (o != null)
                {
                    Object.Destroy(o);
                }
            }
            _made.Clear();
            if (_target != null)
            {
                _target.Release();
            }
        }

        /// <summary>
        /// A wall across the whole picture, its face at z = 0, and a camera looking square at it with nothing else to light it. The wall
        /// is the city's with <paramref name="look"/> in its vertex colours (SurfaceLook), or a plain mesh with none.
        /// </summary>
        private void Build(Material material, Color32? look = null)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("rendering needs a graphics device -- run with -Graphics");
            }
            Shader.SetGlobalVector("_CityXHazeParams", Vector4.zero);   // no air left over from a city test
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            CelShading.Set(true);   // as the game is played

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "LightMarksWall";
            wall.transform.position = new Vector3(0.0f, 0.0f, 0.5f);
            wall.transform.localScale = new Vector3(40.0f, 40.0f, 1.0f);
            wall.GetComponent<Renderer>().sharedMaterial = material;
            _made.Add(wall);
            if (look.HasValue)
            {
                MeshFilter filter = wall.GetComponent<MeshFilter>();
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

            _camera = new GameObject("LightMarksCamera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.fieldOfView = 10.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100.0f;
            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;
            _made.Add(_camera.gameObject);
            _made.Add(_target);
        }

        /// <summary>
        /// The middle of the picture in linear light, and its brightest channel as it is stored; or the same square
        /// <paramref name="right"/> pixels to the right of the middle.
        /// </summary>
        private float Shoot(out float brightest, int right = 0)
        {
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Color[] pixels = image.GetPixels();
            Object.Destroy(image);

            float sum = 0.0f;
            brightest = 0.0f;
            int first = (Size - Middle) / 2;
            for (int y = first; y < first + Middle; y++)
            {
                for (int x = first + right; x < first + right + Middle; x++)
                {
                    Color c = pixels[y * Size + x];
                    brightest = Mathf.Max(brightest, c.maxColorComponent);
                    sum += Mathf.GammaToLinearSpace(c.g);
                }
            }
            return sum / (Middle * Middle);
        }

        /// <summary>
        /// What a spot light at the eye, <paramref name="metres"/> from the wall, adds to the middle of the picture, in linear light. The
        /// light is as strong as puts <see cref="BareLight"/> on the wall by the square of the distance, and is marked by
        /// <paramref name="mark"/> (none: a bare light, as a muzzle flash is).
        /// </summary>
        private float LightOnTheWall(float metres, System.Action<Light> mark)
        {
            return LightOnTheWall(metres, BareLight * metres * metres, mark);
        }

        /// <summary>The same of a light of a given <paramref name="intensity"/>, measured <paramref name="right"/> pixels right of the middle.</summary>
        private float LightOnTheWall(float metres, float intensity, System.Action<Light> mark, int right = 0)
        {
            _camera.transform.position = new Vector3(0.0f, 0.0f, -metres);
            var light = new GameObject("LightMarksLight").AddComponent<Light>();
            _made.Add(light.gameObject);
            light.transform.position = _camera.transform.position;
            light.type = LightType.Spot;
            light.color = Color.white;
            light.intensity = intensity;
            light.range = Range;
            light.spotAngle = 60.0f;
            light.innerSpotAngle = 55.0f;
            light.shadows = LightShadows.None;
            mark?.Invoke(light);
            float lit = Shoot(out float brightest, right);
            Assert.Less(brightest, 0.9f, $"the wall {metres} m off is burnt white: no measure of the light on it");
            light.enabled = false;
            return lit - Shoot(out _, right);
        }

        private static float MatchMetres()
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "CityX/View/Shaders/CityXToonLighting.hlsl"));
            Match define = Regex.Match(text, @"#define CITYX_BEAM_MATCH_METRES ([0-9.]+)");
            Assert.IsTrue(define.Success, "CityXToonLighting.hlsl no longer says where a beam and a bare light agree");
            return float.Parse(define.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>One of the city's own materials: its walls' and streets', or by <paramref name="name"/> another (its window glass).</summary>
        private static Material CityMaterial(string name = "CityConcrete")
        {
#if UNITY_EDITOR
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/CityX/View/Materials/{name}.mat");
            Assert.IsNotNull(material, $"no {name}.mat");
            return material;
#else
            Assert.Ignore("needs the editor to load the city's material.");
            return null;
#endif
        }

        /// <summary>A body's material: the toon shader's, matt and white.</summary>
        private Material BodyMaterial()
        {
            var material = new Material(LitShader.Find());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.0f);
            material.SetFloat("_Metallic", 0.0f);
            _made.Add(material);
            return material;
        }

        /// <summary>
        /// One carried beam under the ground, near the wall and far from it: it puts on the wall at every distance what a bare light of
        /// its strength puts there from where the two agree (CITYX_BEAM_MATCH_METRES). The farthest is a third of the way down its range,
        /// where URP's fade at the end of a light's range has taken a thirtieth.
        /// </summary>
        private void AssertTheBeamIsAsBrightFarOffAsNear()
        {
            float match = MatchMetres();
            float strength = BareLight * match * match;
            float bare = LightOnTheWall(match, strength, null);
            Assert.Greater(bare, Lit, $"a bare light {match} m off does not light the wall");
            foreach (float metres in new[] { match * 0.5f, match, match * 4.0f })
            {
                float beam = LightOnTheWall(metres, strength, CityLightLayers.MarkBeam);
                Assert.AreEqual(bare, beam, bare * Tolerance,
                    $"{metres} m off a carried beam puts {beam:0.0000} on the wall, not the {bare:0.0000} a bare light of its strength puts there {match} m off");
            }
        }

        /// <summary>
        /// One carried beam under the sky, near the wall and far from it: a light like any other. Thrown as it is down a tunnel, a torch
        /// pointed down a street floods every house front thirty metres off.
        /// </summary>
        private void AssertTheBeamFallsOffAsABareLightDoes()
        {
            float match = MatchMetres();
            foreach (float metres in new[] { match * 0.5f, match * 4.0f })
            {
                float bare = LightOnTheWall(metres, null);
                float beam = LightOnTheWall(metres, CityLightLayers.MarkBeam);
                Assert.Greater(bare, Lit, $"a bare light {metres} m off does not light the wall");
                Assert.AreEqual(bare, beam, bare * Tolerance,
                    $"under the sky a carried beam {metres} m off puts {beam:0.0000} on the wall, not the {bare:0.0000} a bare light does");
            }
        }

        [UnityTest]
        public IEnumerator UnderTheGroundABeamIsAsBrightOnABodyFarOffAsNear()
        {
            Build(BodyMaterial());
            Shader.SetGlobalFloat(EyeUnderground, 1.0f);
            yield return null;
            AssertTheBeamIsAsBrightFarOffAsNear();
        }

        [UnityTest]
        public IEnumerator UnderTheGroundABeamIsAsBrightOnATunnelsWallFarOffAsNear()
        {
            Build(CityMaterial(), SurfaceLook.PackUnderground(Color.white, 0, 0.0f));
            yield return null;
            AssertTheBeamIsAsBrightFarOffAsNear();
        }

        [UnityTest]
        public IEnumerator UnderTheSkyABeamFallsOffOnABodyAsAnyLightDoes()
        {
            Build(BodyMaterial());
            yield return null;
            AssertTheBeamFallsOffAsABareLightDoes();
        }

        [UnityTest]
        public IEnumerator UnderTheSkyABeamFallsOffOnTheCityAsAnyLightDoes()
        {
            // A plain mesh's white vertex colour is the city's first surface, outdoors and clean (SurfaceLook).
            Build(CityMaterial());
            yield return null;
            AssertTheBeamFallsOffAsABareLightDoes();
        }

        [UnityTest]
        public IEnumerator ARoomsLightDoesNotLightTheStreetAndAStreetLightDoes()
        {
            Build(CityMaterial());
            yield return null;
            float bare = LightOnTheWall(SideMetres, null);
            float inside = LightOnTheWall(SideMetres, CityLightLayers.MarkInside);
            float outside = LightOnTheWall(SideMetres, CityLightLayers.MarkOutside);
            Assert.Greater(bare, Lit, "a bare light does not light an outside wall");
            Assert.Less(inside, bare * Unlit, $"a room's light shines through its wall onto the street ({inside:0.0000} of a bare light's {bare:0.0000})");
            Assert.AreEqual(bare, outside, bare * Tolerance, "a light outside does not light an outside wall as a bare light does");
        }

        [UnityTest]
        public IEnumerator ARoomLampWithAShadowReachesTheStreetThroughAWindowButNotThroughAWall()
        {
            // The window-shaped light on the pavement (owner, 2026-11-10: "id like that effect to still be in the game"): nothing in the
            // way is the window; a wall that casts a shadow and is not itself in the picture is the wall.
            Build(CityMaterial());
            yield return null;
            System.Action<Light> casting = l =>
            {
                CityLightLayers.MarkInside(l);
                l.shadows = LightShadows.Soft;
                l.shadowStrength = RoomLampShadows.FullStrength;
            };
            System.Action<Light> halfFaded = l =>
            {
                casting(l);
                l.shadowStrength = RoomLampShadows.FullStrength * 0.5f;
            };
            float bare = LightOnTheWall(SideMetres, null);
            float throughTheWindow = LightOnTheWall(SideMetres, casting);
            float fading = LightOnTheWall(SideMetres, halfFaded);
            Assert.Greater(bare, Lit, "a bare light does not light an outside wall");
            Assert.AreEqual(bare, throughTheWindow, bare * Tolerance, "a lit room's lamp with a shadow does not reach the street through its window");
            Assert.AreEqual(bare * 0.5f, fading, bare * Tolerance, "a lamp fading in or out does not follow its shadow's strength");

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.position = new Vector3(0.0f, 0.0f, -SideMetres * 0.5f);
            wall.transform.localScale = new Vector3(30.0f, 30.0f, 0.5f);
            wall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            yield return null;
            float throughTheWall = LightOnTheWall(SideMetres, casting);
            Assert.Less(throughTheWall, bare * Unlit, $"a lit room's lamp shines through a solid wall onto the street ({throughTheWall:0.0000} of a bare light's {bare:0.0000})");
        }

        [UnityTest]
        public IEnumerator AStreetLightDoesNotLightARoomAndARoomsLightDoes()
        {
            Build(CityMaterial(), SurfaceLook.Pack(Color.white, 0, 0.0f, true));
            yield return null;
            float bare = LightOnTheWall(SideMetres, null);
            float inside = LightOnTheWall(SideMetres, CityLightLayers.MarkInside);
            float outside = LightOnTheWall(SideMetres, CityLightLayers.MarkOutside);
            Assert.Greater(bare, Lit, "a bare light does not light a room's wall");
            Assert.Less(outside, bare * Unlit, $"a light outside shines through the wall into the room ({outside:0.0000} of a bare light's {bare:0.0000})");
            Assert.AreEqual(bare, inside, bare * Tolerance, "a room's light does not light its own wall as a bare light does");
        }

        [UnityTest]
        public IEnumerator UnderTheGroundACarriedLightLightsTheWallAndTheCitysLampsDoNot()
        {
            Build(CityMaterial(), SurfaceLook.PackUnderground(Color.white, 0, 0.0f));
            yield return null;
            float match = MatchMetres();
            float bare = LightOnTheWall(match, null);
            float beam = LightOnTheWall(match, CityLightLayers.MarkBeam);
            float inside = LightOnTheWall(match, CityLightLayers.MarkInside);
            float outside = LightOnTheWall(match, CityLightLayers.MarkOutside);
            Assert.Greater(bare, Lit, "a bare light (a fire, a muzzle flash) does not light a tunnel's wall");
            Assert.AreEqual(bare, beam, bare * Tolerance, "a torch does not light a tunnel's wall as a bare light does where the two agree");
            Assert.Less(outside, bare * Unlit, $"a street light lights the tunnel under its road ({outside:0.0000} of a bare light's {bare:0.0000})");
            Assert.Less(inside, bare * Unlit, $"a room's light lights the tunnel under its building ({inside:0.0000} of a bare light's {bare:0.0000})");
        }

        [UnityTest]
        public IEnumerator ALampShinesOnAWindowAsAnAreaNotADisc()
        {
            // A lamp at the eye, four metres from a pane that fills the picture: its mirror image is the middle of the picture. Glass as
            // smooth as a window's showed it there as a white disc a few degrees across, and nothing of it ten degrees off.
            const float metres = 4.0f;
            const int tenDegrees = Size / 6;   // the picture is sixty degrees across
            Build(CityMaterial("CityGlassDark"));
            _camera.fieldOfView = 60.0f;
            yield return null;
            float lamp = metres * metres;      // a lamp that puts one unit of light on the pane
            float middle = LightOnTheWall(metres, lamp, null);
            float beside = LightOnTheWall(metres, lamp, null, tenDegrees);
            Assert.Greater(middle, Lit, "a lamp does not shine on a window at all");
            Assert.Greater(beside, middle * 0.35f,
                $"a lamp's shine on a window is a disc: ten degrees off its middle the pane has {beside:0.0000} of the middle's {middle:0.0000}");
        }

        [Test]
        public void AStreetLampsRealLightIsMarkedOutside()
        {
            var head = new Vector3(12.0f, 9.0f, 34.0f);
            try
            {
                CurrentSky.TowardSun = Vector3.down;   // night: the lamps are on
                StreetLampSites.Clear();
                StreetLampSites.Report(PlanWorld.ChunkOf(head.x), PlanWorld.ChunkOf(head.z), new List<Vector3> { head });
                StreetLampDirector.Direct(head);
                Assert.AreEqual(1, StreetLampDirector.Lit, "the lamp at the eye casts no real light at night");

                int found = 0;
                foreach (Light light in Resources.FindObjectsOfTypeAll<Light>())
                {
                    if (light.name != "StreetLamp")
                    {
                        continue;
                    }
                    found++;
                    Assert.AreEqual(CityLightLayers.Default | CityLightLayers.Exterior, CityLightLayers.Of(light),
                        "a street lamp's light is not marked outside: it casts no shadows, so it would light the rooms beside the road and the tunnel under it");
                }
                Assert.Greater(found, 0, "no street lamp's light was found");
            }
            finally
            {
                StreetLampSites.Clear();
                StreetLampDirector.Direct(null);
            }
        }
    }
}
