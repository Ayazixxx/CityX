using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BEARD, worn on the same head as the hair and in the hair's colour (owner, 2026-11-22: "try making a few basic beard styles and put them
    /// into the editor ... make them not super big so they clip into clothing"; then "get rid of all of them except moustache, circle beard, and
    /// full beard"; then the full beard in two, "the combination of spikey cheek line + spikey bottom be a style and then the crisp lines on top
    /// and bottom be another style"; docs/BEARDS.md). Each is a mesh made in the head's own frame, its origin the ball's middle, so it goes on the head bone as it
    /// is, with no fit. The meshes are in Resources (Beards/&lt;name&gt;, baked by CityX/Bake Beards from SourceArt/Beards/beard_build.py's
    /// models), so every body that has hair can wear one with nothing handed to it.
    /// </summary>
    public sealed partial class CharacterHair
    {
        public const string BeardWornName = "Beard";

        public const string Moustache = "moustache";
        public const string CircleBeard = "circle_beard";
        public const string FullBeard = "full_beard";
        public const string SpikyBeard = "spiky_beard";

        /// <summary>Every beard there is, "none" (clean-shaven) first. A save stores the name, so a name never changes once shipped.</summary>
        public static readonly string[] ValidBeards = { None, Moustache, CircleBeard, FullBeard, SpikyBeard };

        /// <summary>Where a beard's mesh is in Resources.</summary>
        public const string BeardResourceFolder = "Beards/";

        /// <summary>The short name a tile or a console line uses for a beard.</summary>
        public static string BeardLabelFor(string beard)
        {
            switch (beard)
            {
                case None: return "NONE";
                case Moustache: return "STACHE";
                case CircleBeard: return "CIRCLE";
                case FullBeard: return "FULL";
                case SpikyBeard: return "SPIKY";
                default: return (beard ?? string.Empty).ToUpperInvariant();
            }
        }

        [SerializeField]
        private string _beard = None;

        private GameObject _beardWorn;

        public string Beard => _beard;

        public Transform BeardWorn => _beardWorn != null ? _beardWorn.transform : null;

        /// <summary>The mesh of <paramref name="beard"/>, or null for "none" and for a name the game does not have.</summary>
        public static Mesh BeardMesh(string beard)
        {
            return beard == None || System.Array.IndexOf(ValidBeards, beard) < 0 ? null : Resources.Load<Mesh>(BeardResourceFolder + beard);
        }

        /// <summary>Puts <paramref name="beard"/> on (one of <see cref="ValidBeards"/>; "none" shaves); a name the game does not have is ignored.</summary>
        public void SetBeard(string beard)
        {
            if (beard == _beard || System.Array.IndexOf(ValidBeards, beard) < 0)
            {
                return;
            }
            _beard = beard;
            RebuildBeard();
        }

        private void RebuildBeard()
        {
            if (_beardWorn != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_beardWorn);
                }
                else
                {
                    DestroyImmediate(_beardWorn);
                }
                _beardWorn = null;
            }
            Mesh mesh = BeardMesh(_beard);
            if (mesh == null || Head == null)
            {
                return;
            }
            _beardWorn = new GameObject(BeardWornName);
            _beardWorn.transform.SetParent(Head, false);
            _beardWorn.layer = Head.gameObject.layer;
            _beardWorn.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _beardWorn.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HairMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            DyeBeard();
        }

        /// <summary>The beard in the hair's colour, through its own block as the hair's is (the shared material is never touched).</summary>
        private void DyeBeard()
        {
            var renderer = _beardWorn != null ? _beardWorn.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block, 0);
            _block.SetColor(BaseColourId, _colour);
            _block.SetColor(ColourId, _colour);
            renderer.SetPropertyBlock(_block, 0);
        }
    }
}
