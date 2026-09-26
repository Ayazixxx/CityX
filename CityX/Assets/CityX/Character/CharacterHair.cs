using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The hairstyle sitting on the head bone. Ported from character.gd's own
    /// _build_hair / set_hair_style.
    ///
    /// One mesh per style, swapped by name, with "none" always valid and never
    /// a real asset -- character.gd's own VALID_HAIR_STYLES convention, kept
    /// because a save file stores this as a plain string.
    ///
    /// Hair has no paintable UVs of its own. The body's atlas covers the body;
    /// this gets one flat colour, exactly as the Godot build left it, so a
    /// later customization pass has one place to change.
    ///
    /// EVERY STYLE HAS A FIT: where it sits on the head, how it is turned and
    /// how big it is (see <see cref="Fit"/>). A style gets a sensible default
    /// worked out from its own mesh and the size of the head, and the fit can
    /// be changed LIVE (drag the worn hair in the Scene view while the game
    /// runs, then Save in this component's Inspector -- see HairFitSettings), so
    /// a style is tuned by looking at it, not by typing numbers. The fit is
    /// shared by every head wearing the style --
    /// the creator, the character list and the player in the world -- so one
    /// adjustment is seen everywhere.
    /// </summary>
    public sealed class CharacterHair : MonoBehaviour
    {
        public const string None = "none";
        public const string Embermane = "embermane";
        public const string AnimeHair0 = "animehair0";
        public const string Hair1 = "hair1";

        public static readonly string[] ValidStyles = { None, Embermane, AnimeHair0, Hair1 };

        /// <summary>The short name a button or a console line uses for a style.</summary>
        public static string LabelFor(string style)
        {
            switch (style)
            {
                case None: return "BALD";
                case Embermane: return "MANE";
                case AnimeHair0: return "ANIME";
                case Hair1: return "BOB";
                default: return (style ?? string.Empty).ToUpperInvariant();
            }
        }

        /// <summary>character.gd's own HAIR_COLOR.</summary>
        public static readonly Color HairColour = new Color(0.75f, 0.55f, 0.15f);

        [Tooltip("The head bone the hair rides. Found by name when left empty.")]
        public Transform Head;

        [Tooltip("One mesh per style, in ValidStyles order after \"none\".")]
        public Mesh[] StyleMeshes = new Mesh[0];

        public Material HairMaterial;

        [SerializeField]
        private string _style = None;

        private GameObject _worn;

        public string Style => _style;

        /// <summary>The worn hair object, or null when bald -- for tests and tuning.</summary>
        public Transform Worn => _worn != null ? _worn.transform : null;

        // --- where a style sits ---

        /// <summary>Where a style sits on the head, in the head bone's own space (its origin is the CENTRE of the head).</summary>
        [System.Serializable]
        public struct Fit
        {
            public Vector3 Position;
            public Vector3 Euler;
            public Vector3 Scale;

            public Fit(Vector3 position, Vector3 euler, Vector3 scale)
            {
                Position = position;
                Euler = euler;
                Scale = scale;
            }

            public override string ToString()
            {
                return $"pos ({Position.x:0.###}, {Position.y:0.###}, {Position.z:0.###}) " +
                       $"rot ({Euler.x:0.#}, {Euler.y:0.#}, {Euler.z:0.#}) " +
                       $"scale ({Scale.x:0.###}, {Scale.y:0.###}, {Scale.z:0.###})";
            }
        }

        /// <summary>Hair this much wider than the head itself, so it reads as hair on a head and not a skin-tight cap.</summary>
        public const float WidthOverHead = 1.12f;

        /// <summary>How far above the crown of the skull the top of the hair stands, as a fraction of the head's radius.</summary>
        public const float CrownLift = 0.04f;

        /// <summary>
        /// How far the hair's roots sit BELOW the crown of the head, in the
        /// mesh's own units -- embermane's own placement.
        ///
        /// The mesh's baked-in origin is an accident of how it was exported --
        /// character.gd records its raw vertical range as -0.7196 to 0.1191,
        /// i.e. almost all of it below its own origin, with the spikes pointing
        /// DOWN. Godot corrected that with a 180 degree turn about X and by
        /// parking the origin on the head box's top face.
        ///
        /// The same mesh reached Unity through Blender, which baked the glTF
        /// node's own transform into the vertices and moved that origin. So the
        /// placement here is written as the SPAN the hair has to occupy, which
        /// is identical in both engines and does not care where the origin
        /// ended up: 0.1191 of roots tucked into the skull, and the rest of it
        /// standing up off the crown.
        /// </summary>
        private const float RootsBelowCrown = 0.1191f;

        /// <summary>Which way round a modelled-facing-forward style has to be turned to face the way the body does. Set by looking at it.</summary>
        public const float AnimeHair0YawDegrees = 0.0f;

        /// <summary>The same for Hair1. Set by looking at it.</summary>
        public const float Hair1YawDegrees = 0.0f;

        /// <summary>The turn a style's model needs to face the way the body does.</summary>
        public static float YawFor(string style)
        {
            switch (style)
            {
                case AnimeHair0: return AnimeHair0YawDegrees;
                case Hair1: return Hair1YawDegrees;
                default: return 0.0f;
            }
        }

        private static readonly List<CharacterHair> Live = new List<CharacterHair>();

        /// <summary>Raised whenever a style's fit is changed or reset.</summary>
        public static event System.Action FitChanged;

        /// <summary>
        /// The fit a style gets when nobody has touched it, worked out from its
        /// own mesh and the head. Pure, so it can be tested without a scene.
        /// </summary>
        public static Fit DefaultFit(string style, Bounds meshBounds)
        {
            if (style == Embermane)
            {
                // Its own long-standing placement, unchanged.
                return new Fit(
                    new Vector3(0.0f, FusedBodyShape.HeadRadius - RootsBelowCrown + meshBounds.max.y, 0.0f),
                    new Vector3(180.0f, 0.0f, 0.0f),
                    Vector3.one);
            }
            return FitToHead(meshBounds, FusedBodyShape.HeadRadius, YawFor(style));
        }

        /// <summary>
        /// Sizes a hair mesh to a head and stands it on the crown.
        ///
        /// Scale makes the hair's WIDTH the head's diameter times
        /// <see cref="WidthOverHead"/>; the hair's own top is then set just
        /// above the crown, and its middle is centred over the head from side to
        /// side and front to back. The mesh is assumed upright (up is +Y), which is
        /// how a hairstyle is modelled; how it is turned to face front is the
        /// yaw, not something this can tell.
        /// </summary>
        public static Fit FitToHead(Bounds meshBounds, float headRadius, float yawDegrees)
        {
            float width = Mathf.Max(meshBounds.size.x, 0.0001f);
            float scale = headRadius * 2.0f * WidthOverHead / width;

            // The mesh's own middle, turned by the yaw the way the object will be.
            Vector3 centre = Quaternion.Euler(0.0f, yawDegrees, 0.0f) * meshBounds.center;
            float top = meshBounds.max.y;

            var position = new Vector3(
                -scale * centre.x,
                headRadius * (1.0f + CrownLift) - scale * top,
                -scale * centre.z);
            return new Fit(position, new Vector3(0.0f, yawDegrees, 0.0f), Vector3.one * scale);
        }

        /// <summary>The fit in force for a style: what has been saved from the editor, else its automatic default.</summary>
        public static Fit CurrentFit(string style, Bounds meshBounds)
        {
            HairFitSettings settings = HairFitSettings.Current;
            if (settings != null && settings.TryGet(style, out Fit saved))
            {
                return saved;
            }
            return DefaultFit(style, meshBounds);
        }

        /// <summary>Whether a hand-tuned fit has been saved for this style.</summary>
        public static bool IsTuned(string style)
        {
            HairFitSettings settings = HairFitSettings.Current;
            return settings != null && settings.TryGet(style, out _);
        }

        /// <summary>
        /// Puts a style's fit in the settings and re-places every head wearing it. The
        /// Inspector's Save button calls this; persisting the asset to disk is the
        /// editor's job, not the runtime's.
        /// </summary>
        public static void SaveFit(string style, Fit fit)
        {
            HairFitSettings settings = HairFitSettings.Current;
            if (settings == null)
            {
                Debug.LogWarning("CityX: no HairFits settings asset to save into.");
                return;
            }
            settings.Set(style, fit);
            FitChanged?.Invoke();
        }

        /// <summary>Back to the automatic default.</summary>
        public static void ResetFit(string style)
        {
            HairFitSettings settings = HairFitSettings.Current;
            if (settings != null && settings.Remove(style))
            {
                FitChanged?.Invoke();
            }
        }

        /// <summary>Every head currently wearing hair, for finding one to tune.</summary>
        public static IReadOnlyList<CharacterHair> Wearers => Live;

        public Mesh CurrentMesh => MeshFor(_style);

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            Live.Add(this);
            FitChanged += ApplyFit;
        }

        private void OnDisable()
        {
            Live.Remove(this);
            FitChanged -= ApplyFit;
        }

        public void SetStyle(string style)
        {
            if (style == _style || System.Array.IndexOf(ValidStyles, style) < 0)
            {
                return;
            }
            _style = style;
            Rebuild();
        }

        /// <summary>The next style along (or the previous one), wrapping round: the selector's whole job.</summary>
        public void CycleStyle(int direction)
        {
            int index = System.Array.IndexOf(ValidStyles, _style);
            int count = ValidStyles.Length;
            int next = ((index + direction) % count + count) % count;
            SetStyle(ValidStyles[next]);
        }

        /// <summary>
        /// Rebuilds the worn mesh from scratch.
        ///
        /// Public because the head can be rebuilt underneath it -- in Godot the
        /// head pivot is freed and remade, taking the hair with it, and
        /// _build_hair is called again from there rather than being left to
        /// whoever remembers.
        /// </summary>
        public void Rebuild()
        {
            if (_worn != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_worn);
                }
                else
                {
                    DestroyImmediate(_worn);
                }
                _worn = null;
            }

            Mesh mesh = MeshFor(_style);
            if (mesh == null || Head == null)
            {
                return;
            }

            _worn = new GameObject("Hair");
            _worn.transform.SetParent(Head, false);
            _worn.layer = Head.gameObject.layer;

            _worn.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _worn.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HairMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            ApplyFit();
        }

        /// <summary>Puts the worn hair where the style's fit says. Runs on every fit change.</summary>
        public void ApplyFit()
        {
            if (_worn == null)
            {
                return;
            }
            Mesh mesh = MeshFor(_style);
            if (mesh == null)
            {
                return;
            }

            Fit fit = CurrentFit(_style, mesh.bounds);
            _worn.transform.localPosition = fit.Position;
            _worn.transform.localRotation = Quaternion.Euler(fit.Euler);
            _worn.transform.localScale = fit.Scale;
        }

        private Mesh MeshFor(string style)
        {
            int index = System.Array.IndexOf(ValidStyles, style);
            // Index 0 is "none", which has no mesh and never will.
            if (index <= 0)
            {
                return null;
            }
            index -= 1;
            return StyleMeshes != null && index < StyleMeshes.Length ? StyleMeshes[index] : null;
        }
    }
}
