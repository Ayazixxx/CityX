using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// One cast of Glacier Spikes as it is seen (docs/GLACIER_SPIKES.md): the crystals tearing out of the ground behind the front, standing,
    /// then sinking, drawn instanced (one draw per crystal shape, three in all) with the ice shader; what flies and glows round them is
    /// <see cref="GlacierSpikesEffects"/>. Where each crystal stands is <see cref="GlacierSpikesLayout"/>'s, from the cast alone, so the
    /// caster's field and every other player's copy of it are the same. It hurts nothing: the caster's ability strikes.
    /// </summary>
    public sealed class GlacierSpikesField : MonoBehaviour
    {
        /// <summary>The ice's material in Resources (IceMaterialBuilder), so a build keeps its shader.</summary>
        public const string MaterialName = "GlacierIce";

        public const string ShaderName = "CityX/Ice";

        /// <summary>A crystal that has come this far out of the ground has breached it: its chips fly.</summary>
        private const float BreachEmergence = 0.25f;

        /// <summary>How far a crystal stays off a wall beside the line, metres.</summary>
        private const float WallClearance = 0.25f;

        private static readonly int StartId = Shader.PropertyToID("_IceStart");
        private static readonly int DirectionId = Shader.PropertyToID("_IceDirection");
        private static readonly int ClockId = Shader.PropertyToID("_IceClock");

        private static Mesh[] _crystals;
        private static Material _ice;

        private readonly GlacierSpike[] _spikes = new GlacierSpike[GlacierSpikesRules.SpikeCount];
        private readonly float[] _ground = new float[GlacierSpikesRules.SpikeCount];
        private readonly float[] _reach = new float[GlacierSpikesRules.SpikeCount];
        private readonly bool[] _breached = new bool[GlacierSpikesRules.SpikeCount];
        private readonly Matrix4x4[][] _matrices = new Matrix4x4[GlacierSpikesRules.Variants][];
        private readonly int[] _counts = new int[GlacierSpikesRules.Variants];
        private MaterialPropertyBlock _block;
        private Bounds _bounds;
        private GlacierSpikesCast _cast;
        private GlacierSpikesEffects _effects;
        private bool _impacted;

        /// <summary>The line the field grows along, with the ground under it.</summary>
        public GlacierSpikesPath Path { get; } = new GlacierSpikesPath();

        public GlacierSpikesCast Cast => _cast;

        /// <summary>Seconds since the cast.</summary>
        public float Age { get; private set; }

        /// <summary>How many crystals were drawn on the last frame.</summary>
        public int Drawn { get; private set; }

        public GlacierSpikesEffects Effects => _effects;

        /// <summary>The crystal shapes, made once (three seeds of the same cut).</summary>
        public static Mesh[] Crystals
        {
            get
            {
                if (_crystals != null && _crystals[0] != null)
                {
                    return _crystals;
                }
                _crystals = new Mesh[GlacierSpikesRules.Variants];
                for (int v = 0; v < _crystals.Length; v++)
                {
                    IceCrystalMesh.Shape shape = IceCrystalMesh.Build(7.3f + v * 21.7f, GlacierSpikesRules.Facets, GlacierSpikesRules.Taper,
                        GlacierSpikesRules.Roughness, GlacierSpikesRules.Bend);
                    _crystals[v] = IceCrystalMesh.ToMesh(shape, "GlacierCrystal" + v);
                }
                return _crystals;
            }
        }

        /// <summary>The ice's material (CityX/Ice), instanced.</summary>
        public static Material Ice
        {
            get
            {
                if (_ice != null)
                {
                    return _ice;
                }
                Material asset = Resources.Load<Material>(MaterialName);
                Shader shader = Shader.Find(ShaderName);
                _ice = asset != null ? new Material(asset) : shader != null ? new Material(shader) : new Material(LitFallback());
                _ice.name = MaterialName;
                _ice.hideFlags = HideFlags.DontSave;
                _ice.enableInstancing = true;
                return _ice;
            }
        }

        private static Shader LitFallback() => CityX.Core.LitShader.Find();

        /// <summary>
        /// The field for <paramref name="cast"/>, growing from now: its crystals laid out from the seed, each set on the ground under it and kept
        /// off any wall beside the line.
        /// </summary>
        public static GlacierSpikesField Grow(in GlacierSpikesCast cast, IGlacierGround ground, Material template = null)
        {
            var go = new GameObject("GlacierSpikes");
            var field = go.AddComponent<GlacierSpikesField>();
            field.Lay(cast, ground ?? GlacierSpikesGround.World, template);
            return field;
        }

        private void Lay(in GlacierSpikesCast cast, IGlacierGround ground, Material template)
        {
            Path.Follow(cast.Start, cast.Direction, cast.Length, ground);
            _cast = new GlacierSpikesCast(cast.Start, cast.Direction, Path.Length, cast.Seed);
            GlacierSpikesLayout.Build(_cast.Seed, _spikes);
            for (int i = 0; i < _spikes.Length; i++)
            {
                Vector3 anchor = GlacierSpikesLayout.Anchor(_spikes[i], _cast);
                anchor.y = Path.HeightAt(_spikes[i].Along * _cast.Length);
                Vector3 across = GlacierSpikesLayout.Across(_spikes[i], _cast);
                float reach = float.MaxValue;
                Vector3 knee = Vector3.up * 0.5f;
                if (across.sqrMagnitude > 1e-4f)
                {
                    float clear = ground.ClearDistance(anchor + knee, anchor + across + knee);
                    if (clear < across.magnitude - 1e-3f)
                    {
                        reach = Mathf.Max(0.0f, clear - WallClearance);
                        across *= reach / across.magnitude;
                    }
                }
                _reach[i] = reach;
                Vector3 foot = anchor + across + Vector3.up * GlacierSpikesRules.StepUp;
                _ground[i] = ground.TryGround(foot, GlacierSpikesRules.StepUp + GlacierSpikesRules.StepDown, out float y) ? y : anchor.y;
            }
            for (int v = 0; v < _matrices.Length; v++)
            {
                _matrices[v] = new Matrix4x4[_spikes.Length];
            }
            _bounds = new Bounds(_cast.Start, Vector3.zero);
            _bounds.Encapsulate(_cast.End);
            _bounds.Expand(new Vector3(1.0f, 0.0f, 1.0f) * (GlacierSpikesRules.ImpactRadius + GlacierSpikesRules.Radius * 3.0f) * 2.0f);
            _bounds.Expand(Vector3.up * (GlacierSpikesRules.Height * GlacierSpikesRules.Peak * 2.0f + 4.0f) * 2.0f);
            _effects = gameObject.AddComponent<GlacierSpikesEffects>();
            _effects.Begin(this, ground, template);
        }

        /// <summary>Where crystal <paramref name="index"/> stands <paramref name="age"/> seconds after the cast (the same on every machine).</summary>
        public GlacierSpikePose PoseOf(int index, float age) =>
            GlacierSpikesLayout.Pose(_spikes[index], _cast, age, _ground[index], _reach[index]);

        public int Count => _spikes.Length;

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>One frame of the field: its crystals placed and drawn, its effects told; public so a test can step it.</summary>
        public void Step(float deltaTime)
        {
            Age += deltaTime;
            if (Age >= GlacierSpikesRules.EndSeconds)
            {
                Destroy(gameObject);
                return;
            }
            for (int v = 0; v < _counts.Length; v++)
            {
                _counts[v] = 0;
            }
            for (int i = 0; i < _spikes.Length; i++)
            {
                GlacierSpikePose pose = PoseOf(i, Age);
                if (!pose.Visible)
                {
                    continue;
                }
                if (!_breached[i] && pose.Emergence >= BreachEmergence)
                {
                    _breached[i] = true;
                    _effects.Breach(new Vector3(pose.Position.x, _ground[i], pose.Position.z), pose.Size.x, _spikes[i].Rubble);
                }
                int variant = _spikes[i].Variant;
                _matrices[variant][_counts[variant]++] = Matrix4x4.TRS(pose.Position, pose.Rotation, pose.Size);
            }
            float front = GlacierSpikesRules.FrontDistance(Age);
            if (!_impacted && front >= _cast.Length)
            {
                _impacted = true;
                _effects.Impact();
            }
            _effects.Step(Mathf.Min(front, _cast.Length), GlacierSpikesRules.Sunk(Age), deltaTime);
            Draw();
        }

        private void Draw()
        {
            int drawn = 0;
            for (int v = 0; v < _counts.Length; v++)
            {
                drawn += _counts[v];
            }
            Drawn = drawn;
            if (!SystemInfo.supportsInstancing)
            {
                return;   // no graphics device (a batch test run): nothing to draw on
            }
            _block ??= new MaterialPropertyBlock();
            _block.SetVector(StartId, _cast.Start);
            _block.SetVector(DirectionId, _cast.Direction);
            _block.SetVector(ClockId, new Vector4(Age, GlacierSpikesRules.FrontSpeed, _cast.Length, GlacierSpikesRules.BirthGlowSeconds));
            var render = new RenderParams(Ice)
            {
                worldBounds = _bounds,
                matProps = _block,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            Mesh[] crystals = Crystals;
            for (int v = 0; v < _counts.Length; v++)
            {
                if (_counts[v] > 0)
                {
                    Graphics.RenderMeshInstanced(render, crystals[v], 0, _matrices[v], _counts[v]);
                }
            }
        }
    }
}
