using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// SNOW (docs/SNOW.md; owner, 2026-11-05: "add snowy weather mechanics, research how video games do snow weather and make it a weather option"). Eases the snowfall in and
    /// out, lays and melts the cover (<see cref="SnowRules"/>), and tells the shaders both: CityX/Snowfall draws the flakes, CityXSnow.hlsl whitens what faces up where the
    /// sky can reach. It keeps its own finer map of what is overhead, for both of them and for the walking. It also brings the grey sky and closing fog, and the wind.
    ///
    /// It is the weather's way in (<see cref="IWeatherControl"/>), rain's too, and on the scene's root rather than the player as rain is, so that it runs on every machine.
    /// A friend who joins does not play the baked player, whose child the rain is, so the rain moves to the body they claim.
    /// </summary>
    public sealed class SnowEffect : MonoBehaviour, IWeatherControl
    {
        public const int FlakeCount = 14000;

        public static readonly Vector3 FlakeBox = new Vector3(40.0f, 24.0f, 40.0f);

        /// <summary>How long a change of snowfall takes to arrive, seconds.</summary>
        public const float IntensityRampSeconds = 6.0f;

        /// <summary>How long the sky takes to close in or clear, seconds.</summary>
        public const float MoodRampSeconds = 8.0f;

        /// <summary>The map of what is overhead: finer than rain's, since a band along each wall that stays bare shows on a white street where nothing shows on a wet one, and slower to re-check, since snow lies on what does not move.</summary>
        public const float MapCellMetres = 1.5f;

        public const int MapCells = 48;

        public const float MapMaxAgeSeconds = 6.0f;

        public const int MapAgingBudget = 12;

        /// <summary>A cell with nothing under it reads this high, far under anything.</summary>
        public const float NothingOverhead = -10000.0f;

        /// <summary>How often the shaders' copy of the map is brought up to date, seconds, besides whenever the map's window has moved: what stands overhead hardly changes.</summary>
        public const float MapUploadSeconds = 0.5f;

        /// <summary>Under cover the wind is this much of its strength, and this dull, Hz.</summary>
        public const float ShelteredWind = 0.35f;

        public const float ShelteredWindCutoffHz = 700.0f;

        private const float OpenWindCutoffHz = 3000.0f;

        /// <summary>How fast the sound follows walking in and out of cover, per second.</summary>
        private const float ShelterEasePerSecond = 2.5f;

        private static readonly int SnowId = Shader.PropertyToID("_CityXSnow");
        private static readonly int MapId = Shader.PropertyToID("_CityXSnowMap");
        private static readonly int HeightsId = Shader.PropertyToID("_CityXSnowHeights");
        private static readonly int BoxId = Shader.PropertyToID("_Box");
        private static readonly int ShowId = Shader.PropertyToID("_Show");
        private static readonly int WindId = Shader.PropertyToID("_Wind");

        [Tooltip("The flakes' material (CityX/Snowfall). Set by the scene builder.")]
        public Material FlakeMaterial;

        [Tooltip("The city's rain, whose orders pass through here and whose sky this leaves alone while it is storming.")]
        public RainEffect Rain;

        public DayNightCycle Cycle;

        public Light Sun;

        [Tooltip("The clear day's fog. Set by the scene builder from the same numbers as the scene's.")]
        public float ClearFogStart = 150.0f;

        public float ClearFogEnd = 380.0f;

        private float _target;
        private float _intensity;
        private float _cover;
        private float _mood;
        private float _shelter;
        private float _lastRain;
        private bool _moodWasOn;
        private RainCover _map;
        private Texture2D _heights;
        private float[] _heightData;
        private float[] _rawHeights;
        private float _uploadIn;
        private MeshRenderer _flakes;
        private Material _flakeMaterial;
        private AudioSource _wind;
        private AudioLowPassFilter _windFilter;

        public float TargetIntensity => _target;

        public float Intensity => _intensity;

        public float Cover => _cover;

        public float Mood => _mood;

        public RainCover Map => _map;

        public bool FlakesShowing => _flakes != null && _flakes.enabled;

        public WeatherOrder Now => new WeatherOrder
        {
            Rain = Rain != null ? Rain.TargetIntensity : _lastRain,
            Snow = _target,
            Cover = _cover,
        };

        private void Awake()
        {
            _map = new RainCover(RainCover.CastFromSky, MapCellMetres, MapCells, MapMaxAgeSeconds, MapAgingBudget);
            _heights = new Texture2D(MapCells, MapCells, TextureFormat.RFloat, false, true)
            {
                name = "SnowHeights",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave,
            };
            _heightData = new float[MapCells * MapCells];
            _rawHeights = new float[MapCells * MapCells];
            UploadHeights();
            BuildFlakes();
            _wind = gameObject.AddComponent<AudioSource>();
            _wind.clip = SnowSounds.Wind;
            _wind.loop = true;
            _wind.playOnAwake = false;
            _wind.spatialBlend = 0.0f;
            _wind.volume = 0.0f;
            _windFilter = gameObject.AddComponent<AudioLowPassFilter>();
            _windFilter.cutoffFrequency = OpenWindCutoffHz;
        }

        private void OnEnable()
        {
            CurrentWeather.Control = this;
            LocalPlayerBody.Changed += OnBodyClaimed;
            if (LocalPlayerBody.Current != null)
            {
                OnBodyClaimed(LocalPlayerBody.Current);
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(CurrentWeather.Control, this))
            {
                CurrentWeather.Control = null;
            }
            LocalPlayerBody.Changed -= OnBodyClaimed;
            CurrentWeather.ClearSnow();
            Shader.SetGlobalVector(SnowId, Vector4.zero);
        }

        private void OnDestroy()
        {
            if (_flakes != null)
            {
                Destroy(_flakes.GetComponent<MeshFilter>().sharedMesh);
            }
            if (_flakeMaterial != null)
            {
                Destroy(_flakeMaterial);
            }
            if (_heights != null)
            {
                Destroy(_heights);
            }
        }

        private void OnBodyClaimed(GameObject body)
        {
            if (Rain != null && body != null)
            {
                Rain.transform.SetParent(body.transform, false);
            }
        }

        /// <summary>The weather is now this: what falls, and if told, how much lies.</summary>
        public void Apply(in WeatherOrder order)
        {
            _lastRain = order.Rain;
            if (Rain != null)
            {
                Rain.SetRain(order.Rain);
                if (order.Dry)
                {
                    Rain.Dry();
                }
            }
            SetSnow(order.Snow);
            if (order.Cover >= 0.0f)
            {
                _cover = Mathf.Clamp01(order.Cover);
            }
        }

        /// <summary>Snow at an intensity: 0 none, up to 1 a blizzard. Eases in and out.</summary>
        public void SetSnow(float intensity)
        {
            _target = Mathf.Clamp01(intensity);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _intensity = Mathf.MoveTowards(_intensity, _target, deltaTime / IntensityRampSeconds);
            _cover = SnowRules.StepCover(_cover, _intensity, CurrentSky.Sunshine, CurrentWeather.Intensity, deltaTime);
            _mood = Mathf.MoveTowards(_mood, SnowRules.Overcast(_target), deltaTime / MoodRampSeconds);

            CurrentWeather.SnowFalling = _intensity;
            CurrentWeather.SnowCover = _cover;
            CurrentWeather.SnowMap = _map;

            bool snowy = _intensity > 0.0f || _cover > 0.0f;
            Vector3 centre = Eye();
            if (snowy)
            {
                _map.Refresh(centre, deltaTime);
                _uploadIn -= deltaTime;
                // More than a re-check's few new cells: the window has moved, and the edge the player is walking toward must be known now.
                if (_uploadIn <= 0.0f || _map.LastRefreshRays > MapAgingBudget)
                {
                    _uploadIn = MapUploadSeconds;
                    UploadHeights();
                }
            }
            Shader.SetGlobalVector(SnowId, new Vector4(_cover, 0.0f, 0.0f, 0.0f));
            float half = MapCellMetres * (MapCells * 0.5f - 1.0f);
            Shader.SetGlobalVector(MapId, new Vector4(centre.x, centre.z, half, 1.0f / (MapCellMetres * MapCells)));

            if (_flakes != null)
            {
                _flakes.enabled = _intensity > 0.0f;
                if (_flakes.enabled)
                {
                    _flakeMaterial.SetFloat(ShowId, _intensity);
                    float wind = SnowRules.WindSpeed(_intensity);
                    _flakeMaterial.SetVector(WindId, new Vector4(wind * 0.8f, 0.0f, wind * 0.6f, 0.0f));
                }
            }
            TickWind(centre, deltaTime);
        }

        /// <summary>The sky, the sun and the fog, after <see cref="DayNightCycle"/> has set them; left to the rain while it is storming, as it does the same.</summary>
        private void LateUpdate()
        {
            bool on = _mood > 0.0f && (Rain == null || Rain.StormStrength <= 0.0f);
            if (!on && !_moodWasOn)
            {
                return;
            }
            _moodWasOn = on;
            if (Cycle != null)
            {
                Cycle.Overcast = _mood;
                Cycle.Apply(SkyCycle.At(Cycle.TimeOfDay));
            }
            if (Sun != null)
            {
                Sun.intensity *= SnowRules.SunThrough(_mood);
            }
            float share = Mathf.InverseLerp(0.0f, SnowRules.Overcast(1.0f), _mood);
            RenderSettings.fogStartDistance = SnowRules.FogStart(ClearFogStart, share);
            RenderSettings.fogEndDistance = SnowRules.FogEnd(ClearFogEnd, share);
        }

        private void TickWind(Vector3 centre, float deltaTime)
        {
            float volume = SnowRules.WindVolume(_intensity);
            if (volume <= 0.0f)
            {
                if (_wind.isPlaying)
                {
                    _wind.Stop();
                }
                return;
            }
            if (!_wind.isPlaying)
            {
                _wind.Play();
            }
            _shelter = Mathf.MoveTowards(_shelter, _map.Shelter(centre), ShelterEasePerSecond * deltaTime);
            _wind.volume = volume * Mathf.Lerp(1.0f, ShelteredWind, _shelter);
            _windFilter.cutoffFrequency = Mathf.Lerp(OpenWindCutoffHz, ShelteredWindCutoffHz, _shelter);
        }

        private Vector3 Eye()
        {
            Camera camera = Camera.main;
            return camera != null ? camera.transform.position : transform.position;
        }

        private void UploadHeights()
        {
            _map.CopyHeights(_rawHeights, NothingOverhead);
            RainCover.WearEdges(_rawHeights, _heightData, MapCells);
            _heights.SetPixelData(_heightData, 0);
            _heights.Apply(false, false);
            Shader.SetGlobalTexture(HeightsId, _heights);
        }

        private void BuildFlakes()
        {
            if (FlakeMaterial == null)
            {
                return;
            }
            var host = new GameObject("Flakes");
            host.transform.SetParent(transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = FlakeField.Build(FlakeCount, new System.Random(4021));
            _flakes = host.AddComponent<MeshRenderer>();
            _flakeMaterial = new Material(FlakeMaterial) { hideFlags = HideFlags.DontSave };
            _flakeMaterial.SetVector(BoxId, FlakeBox);
            _flakes.sharedMaterial = _flakeMaterial;
            _flakes.shadowCastingMode = ShadowCastingMode.Off;
            _flakes.receiveShadows = false;
            _flakes.enabled = false;
        }
    }
}
