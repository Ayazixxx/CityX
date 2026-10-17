using System.Collections.Generic;
using CityX.Core;
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
    /// this gets one flat colour, ANY colour (see <see cref="Colour"/>), which the
    /// creator sets and the character save keeps.
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
        public const string Hair1 = "hair1";
        public const string Danny = "danny";
        public const string AnimeHair = "animehair";
        public const string ChadHair = "chadhair";
        public const string OldManHair = "oldmanhair";
        public const string SpikeX = "spikex";
        public const string SwipeCut = "swipecut";
        public const string IdkNeedsWork = "idkneedswork";
        public const string ChadHair2 = "chadhair2";
        public const string Hyde = "hyde";

        /// <summary>
        /// Every style there is, "none" first. The creator's buttons, the mesh list and the
        /// tests all walk this, so a style added here shows up everywhere.
        ///
        /// A save file stores the name, so a style that is REMOVED (Embermane and the OLD
        /// AnimeHair0 were, as outdated -- this AnimeHair, added 2026-09-27 from a different,
        /// user-supplied model, is unrelated to that removed one despite the similar name) leaves
        /// characters carrying a name nothing recognises. That is safe: <see cref="SetStyle"/>
        /// ignores a name that is not here, and a head starts out bald, so such a character is
        /// simply bald until they pick again.
        /// </summary>
        public static readonly string[] ValidStyles = { None, Hair1, Danny, AnimeHair, ChadHair, OldManHair, SpikeX, SwipeCut, IdkNeedsWork, ChadHair2, Hyde };

        /// <summary>The short name a button or a console line uses for a style.</summary>
        public static string LabelFor(string style)
        {
            switch (style)
            {
                case None: return "BALD";
                case Hair1: return "BOB";
                case Danny: return "DAN";
                case AnimeHair: return "ANI";
                case ChadHair: return "CHAD";
                case OldManHair: return "OLD";
                case SpikeX: return "SPIKE";
                case SwipeCut: return "SWIPE";
                case IdkNeedsWork: return "IDK";
                case ChadHair2: return "CHAD2";
                case Hyde: return "HYD";
                default: return (style ?? string.Empty).ToUpperInvariant();
            }
        }

        /// <summary>character.gd's own HAIR_COLOR: the colour hair has until somebody picks another.</summary>
        public static readonly Color HairColour = CharacterSave.DefaultHairColour;

        [Tooltip("The head bone the hair rides. Found by name when left empty.")]
        public Transform Head;

        [Tooltip("One mesh per style, in ValidStyles order after \"none\".")]
        public Mesh[] StyleMeshes = new Mesh[0];

        public Material HairMaterial;

        [SerializeField]
        private string _style = None;

        [SerializeField]
        private Color _colour = CharacterSave.DefaultHairColour;

        private GameObject _worn;

        private static MaterialPropertyBlock _block;
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColourId = Shader.PropertyToID("_Color");

        public string Style => _style;

        /// <summary>The colour of the hair, worn or not: it is kept while bald and put back on the next style.</summary>
        public Color Colour => _colour;

        /// <summary>
        /// Dyes the hair. Per renderer, through a property block, so the one shared hair material
        /// stays as it is and every head can have its own colour. Alpha is always opaque.
        /// </summary>
        public void SetColour(Color colour)
        {
            _colour = new Color(colour.r, colour.g, colour.b, 1.0f);
            ApplyColour();
        }

        private void ApplyColour()
        {
            if (_worn == null)
            {
                return;
            }
            var renderer = _worn.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }
            // MATERIAL 0's BLOCK, not the whole renderer's. Garment tints and PlayerWetness both use
            // the per-material block, so that is the one place they read a colour back from: the dye
            // used to go in the whole-renderer block, PlayerWetness never saw it, took the material's
            // default gold as the hair's "dry" colour, and put that back the moment it rained.
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block, 0);
            _block.SetColor(BaseColourId, _colour);
            _block.SetColor(ColourId, _colour);
            renderer.SetPropertyBlock(_block, 0);

            // A body already wet should darken the new colour now, not up to two seconds from now.
            GetComponentInParent<PlayerWetness>()?.Invalidate();
        }

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

        /// <summary>The turn a style's model needs to face the way the body does. Hair1 faces forward as modelled.</summary>
        public static float YawFor(string style)
        {
            return 0.0f;
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

            ApplyColour();
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
