using System.Collections;
using System.Collections.Generic;
using CityX.Core;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The cel look rendered (owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines"): a white ball lit
    /// from the side against a green screen, on CityX/Lit. With the look on it is two flat tones with a black line round it; with it off it is
    /// URP's own smooth shading, pixel for pixel what URP's Lit draws, with no line. Needs a graphics device (-Graphics).
    /// </summary>
    public sealed class CelShadingPlayTests
    {
        private const int Size = 256;
        private const float Grey = 0.08f;          // a pixel is grey (the ball, a line) when its channels are this close; the screen is green
        private const float DarkLuminance = 0.08f; // a line's pixel is this dark (the ball's shade side is lit by the ambient, never this dark)
        private const int Erode = 4;               // the ball's inside: pixels at least this far from the screen (clear of the line)
        private const int ToneBins = 32;
        private const float ToneShare = 0.02f;     // a tone is a luminance bin holding this share of the inside

        private readonly List<Object> _made = new List<Object>();
        private bool _wasOn;
        private Camera _camera;
        private RenderTexture _target;
        private Renderer _ball;

        [SetUp]
        public void SetUp()
        {
            _wasOn = CelShading.Enabled;
        }

        [TearDown]
        public void TearDown()
        {
            CelShading.Set(_wasOn);
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

        private void Build(Shader shader)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("rendering needs a graphics device -- run with -Graphics");
            }
            Shader.SetGlobalVector("_CityXHazeParams", Vector4.zero);   // no air left over from a city test
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);

            var light = new GameObject("CelTestSun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(10.0f, 80.0f, 0.0f);   // from the left, a little above: half the ball lit
            _made.Add(light.gameObject);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "CelTestBall";
            ball.transform.position = new Vector3(0.0f, 0.0f, 5.0f);
            ball.transform.localScale = Vector3.one * 2.0f;
            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.0f);
            material.SetFloat("_Metallic", 0.0f);
            _ball = ball.GetComponent<Renderer>();
            _ball.sharedMaterial = material;
            _made.Add(ball);
            _made.Add(material);

            _camera = new GameObject("CelTestCamera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.green;
            _camera.fieldOfView = 30.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50.0f;
            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;
            _made.Add(_camera.gameObject);
            _made.Add(_target);
        }

        private Color[] Shoot()
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
            return pixels;
        }

        private static bool IsGrey(Color c) => Mathf.Abs(c.g - c.r) < Grey && Mathf.Abs(c.g - c.b) < Grey;

        private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>The ball's inside: grey pixels with no screen within <see cref="Erode"/> pixels.</summary>
        private static List<Color> Inside(Color[] pixels)
        {
            var inside = new List<Color>();
            for (int y = Erode; y < Size - Erode; y++)
            {
                for (int x = Erode; x < Size - Erode; x++)
                {
                    bool clear = true;
                    for (int dy = -Erode; dy <= Erode && clear; dy++)
                    {
                        for (int dx = -Erode; dx <= Erode && clear; dx++)
                        {
                            clear = IsGrey(pixels[(y + dy) * Size + x + dx]);
                        }
                    }
                    if (clear)
                    {
                        inside.Add(pixels[y * Size + x]);
                    }
                }
            }
            return inside;
        }

        /// <summary>How many luminance bins hold at least <see cref="ToneShare"/> of <paramref name="colours"/>: the tones the ball is shaded in.</summary>
        private static int Tones(List<Color> colours)
        {
            var bins = new int[ToneBins];
            foreach (Color c in colours)
            {
                bins[Mathf.Clamp((int)(Luminance(c) * ToneBins), 0, ToneBins - 1)]++;
            }
            int tones = 0;
            foreach (int count in bins)
            {
                tones += count >= colours.Count * ToneShare ? 1 : 0;
            }
            return tones;
        }

        private static int Dark(Color[] pixels)
        {
            int dark = 0;
            foreach (Color c in pixels)
            {
                dark += IsGrey(c) && Luminance(c) < DarkLuminance ? 1 : 0;
            }
            return dark;
        }

        [UnityTest]
        public IEnumerator WithTheLookOnTheBallIsTwoFlatTonesInABlackLine()
        {
            Build(LitShader.Find());
            yield return null;

            CelShading.Set(true);
            Color[] cel = Shoot();
            CelShading.Set(false);
            Color[] smooth = Shoot();

            List<Color> celInside = Inside(cel), smoothInside = Inside(smooth);
            int celTones = Tones(celInside), smoothTones = Tones(smoothInside);
            int celDark = Dark(cel), smoothDark = Dark(smooth);
            Debug.Log($"CityX cel test: {celInside.Count} pixels inside the ball; cel {celTones} tones, {celDark} line pixels; " +
                $"smooth {smoothTones} tones, {smoothDark} dark pixels");
            Assert.Greater(celInside.Count, 2000, "the ball was not drawn");
            Assert.LessOrEqual(celTones, 3, "the cel-shaded ball is not shaded in flat tones");
            Assert.GreaterOrEqual(smoothTones, 8, "with the cel look off the ball is not smoothly shaded");
            Assert.Greater(celDark, 300, "no black line round the ball");
            Assert.Less(smoothDark, 20, "a line was drawn with the cel look off");
        }

        [UnityTest]
        public IEnumerator WithTheLookOffCityXLitIsUrpsLit()
        {
            Build(LitShader.Find());
            yield return null;
            CelShading.Set(false);
            Color[] ours = Shoot();
            _ball.sharedMaterial.shader = Shader.Find("Universal Render Pipeline" + "/Lit");
            Color[] urp = Shoot();
            int differ = 0;
            for (int i = 0; i < ours.Length; i++)
            {
                Color a = ours[i], b = urp[i];
                differ += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 2.0f / 255.0f ? 1 : 0;
            }
            Assert.AreEqual(0, differ, $"{differ} pixels differ: with the cel look off CityX/Lit does not draw as URP's Lit");
        }
    }
}
