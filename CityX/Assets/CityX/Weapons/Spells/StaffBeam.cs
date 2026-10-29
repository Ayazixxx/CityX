using CityX.Character;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// A staff's beam of light, drawn in code (docs/STAFFS.md): a straight beam from the staff's head to where it ends, a white-hot core in a
    /// wide soft glow with bands of light running down it and a quick pulse in its width, a flare at the staff's head, and where it lands a
    /// flare, motes thrown off and a light; the hum of it. Its colours and pace are its <see cref="Style"/>: the Arcane Beam Staff's magenta
    /// beam that burns (owner: "similar to how the water staff looked at the beginning"), the Staff of Healing's gold one that mends. The same on
    /// another player's copy of the staff.
    /// </summary>
    public sealed class StaffBeam : MonoBehaviour
    {
        /// <summary>A beam's look. Its materials are made the first time a beam of it is.</summary>
        public sealed class Style
        {
            public string Name;
            public Color Glow;
            public Color Core;
            public Color Light;
            public float GlowWidth;
            public float CoreWidth;

            /// <summary>Bands of light down the beam a second, and how much its width pulses.</summary>
            public float BandsPerSecond;

            public float PulseShare;

            /// <summary>Motes a second thrown off where it lands.</summary>
            public float MotesPerSecond;

            /// <summary>The arcane barrier's hum is played at this pitch: higher is a beam's whine, lower a soft drone.</summary>
            public float HumPitch;

            internal Material GlowMaterial;
            internal Material CoreMaterial;
            internal Material FlareMaterial;
        }

        /// <summary>The Arcane Beam Staff's: magenta, white-hot in the middle, quick and whining.</summary>
        public static readonly Style Arcane = new Style
        {
            Name = "ArcaneBeam", Glow = ArcaneVisuals.Magenta, Core = ArcaneVisuals.Hot, Light = ArcaneVisuals.Magenta,
            GlowWidth = 0.55f, CoreWidth = 0.13f, BandsPerSecond = 7.0f, PulseShare = 0.15f, MotesPerSecond = 45.0f, HumPitch = 1.4f,
        };

        /// <summary>The Staff of Healing's: warm gold, narrower, slow and soft (Mercy's beam is a gold one).</summary>
        public static readonly Style Healing = new Style
        {
            Name = "HealingBeam", Glow = new Color(1.0f, 0.74f, 0.22f), Core = new Color(1.0f, 0.97f, 0.82f), Light = new Color(1.0f, 0.82f, 0.45f),
            GlowWidth = 0.38f, CoreWidth = 0.09f, BandsPerSecond = 2.5f, PulseShare = 0.06f, MotesPerSecond = 22.0f, HumPitch = 0.85f,
        };

        /// <summary>How many bands of light a metre of beam shows, and how often a second its width pulses.</summary>
        public const float BandsPerMetre = 0.7f;

        public const float PulseHz = 9.0f;

        /// <summary>The flares' sizes, metres, and the light where the beam lands.</summary>
        public const float HeadFlareSize = 0.45f;

        public const float EndFlareSize = 1.0f;
        public const float LightIntensity = 3.0f;
        public const float LightRange = 5.0f;
        public const float HumVolume = 0.45f;

        private Style _style;
        private LineRenderer _glow;
        private LineRenderer _core;
        private MeshRenderer _headFlare;
        private Transform _end;
        private MeshRenderer _endFlare;
        private ParticleSystem _motes;
        private Light _light;
        private AudioSource _hum;
        private MaterialPropertyBlock _block;

        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();
        private float _scroll;
        private float _age;
        private static Texture2D _texture;

        public bool IsRunning { get; private set; }

        /// <summary>Where the beam ends this frame, and whether it ends on something (else it runs out at its reach).</summary>
        public Vector3 End { get; private set; }

        public bool Lands { get; private set; }

        /// <summary>A beam of <paramref name="style"/> on <paramref name="tip"/> (the staff's head), stopped until <see cref="Run"/>.</summary>
        public static StaffBeam Make(Transform tip, Style style)
        {
            var go = new GameObject(style.Name);
            go.transform.SetParent(tip, false);
            var beam = go.AddComponent<StaffBeam>();
            beam.Build(style);
            return beam;
        }

        private void Build(Style style)
        {
            _style = style;
            Materials(style);
            _glow = Line("Glow", style.GlowMaterial, style.GlowWidth);
            _core = Line("Core", style.CoreMaterial, style.CoreWidth);
            _headFlare = ArcaneVisuals.Sprite(transform, style.FlareMaterial, HeadFlareSize, "HeadFlare");
            _end = new GameObject("End").transform;
            _end.SetParent(transform, false);
            _endFlare = ArcaneVisuals.Sprite(_end, style.FlareMaterial, EndFlareSize, "EndFlare");
            _motes = ArcaneVisuals.Motes(_end, 0.0f, 0.08f, 4.0f, 0.09f, 0.35f);
            ParticleSystem.MainModule main = _motes.main;
            main.startColor = new ParticleSystem.MinMaxGradient(style.Glow, style.Core);
            _motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = style.FlareMaterial;
            _light = ArcaneVisuals.Light(_end, LightIntensity, LightRange);
            _light.color = style.Light;
            if (Application.isPlaying)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.clip = ArcaneBarrierSounds.Hum;
                _hum.loop = true;
                _hum.pitch = style.HumPitch;
                _hum.volume = HumVolume;
                _hum.spatialBlend = 1.0f;
                _hum.playOnAwake = false;
            }
            Show(false, false);
        }

        /// <summary>The style's materials: the glow and the core painted with the beam's bands, the flares and motes a soft spot.</summary>
        private static void Materials(Style style)
        {
            if (style.GlowMaterial != null)
            {
                return;
            }
            style.GlowMaterial = Painted(style.Name + "Glow", style.Glow);
            style.CoreMaterial = Painted(style.Name + "Core", style.Core);
            style.FlareMaterial = SoftGlow.Make(style.Name + "Flare", style.Glow, true);
        }

        private static Material Painted(string name, Color colour) => GlowShapes.Painted(name, colour, true, BandTexture());

        /// <summary>Its alpha the soft spot's fall-off across the beam and bands of light along it, repeating so it tiles and scrolls.</summary>
        private static Texture2D BandTexture()
        {
            if (_texture == null)
            {
                _texture = GlowShapes.Paint("StaffBeamBands", 64, TextureWrapMode.Repeat, (u, v) =>
                {
                    float d = v * 2.0f - 1.0f;
                    float fall = 1.0f - d * d;
                    return fall * fall * fall * (0.7f + 0.3f * Mathf.Sin(2.0f * Mathf.PI * u));
                });
            }
            return _texture;
        }

        private LineRenderer Line(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void Run()
        {
            if (IsRunning)
            {
                return;
            }
            IsRunning = true;
            _age = 0.0f;
            Show(true, Lands);
            if (_hum != null)
            {
                _hum.Play();
            }
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }
            IsRunning = false;
            Show(false, false);
            if (_hum != null)
            {
                _hum.Stop();
            }
        }

        /// <summary>The beam from <paramref name="from"/> to <paramref name="to"/>, landing on something there or not, <paramref name="deltaTime"/> on.</summary>
        public void Aim(Vector3 from, Vector3 to, bool lands, float deltaTime)
        {
            End = to;
            Lands = lands;
            if (!IsRunning)
            {
                return;
            }
            _age += deltaTime;
            _scroll -= _style.BandsPerSecond * deltaTime;
            float pulse = 1.0f + _style.PulseShare * Mathf.Sin(2.0f * Mathf.PI * PulseHz * _age);
            float length = Vector3.Distance(from, to);
            Lay(_glow, from, to, _style.GlowWidth * pulse, length);
            Lay(_core, from, to, _style.CoreWidth * pulse, length);
            _headFlare.transform.position = from;
            _end.position = to;
            Show(true, lands);
            if (lands)
            {
                _motes.Emit(Mathf.Max(0, Mathf.RoundToInt(_style.MotesPerSecond * deltaTime + Random.value - 0.5f)));
            }
        }

        private void Lay(LineRenderer line, Vector3 from, Vector3 to, float width, float length)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.GetPropertyBlock(Block);
            Block.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(0.01f, length * BandsPerMetre), 1.0f, _scroll, 0.0f));
            line.SetPropertyBlock(Block);
        }

        private void Show(bool running, bool lands)
        {
            _glow.enabled = running;
            _core.enabled = running;
            _headFlare.enabled = running;
            _endFlare.enabled = running && lands;
            _light.enabled = running && lands;
        }
    }
}
