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
    /// </summary>
    public sealed class CharacterHair : MonoBehaviour
    {
        public const string None = "none";
        public const string Embermane = "embermane";

        public static readonly string[] ValidStyles = { None, Embermane };

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

        /// <summary>
        /// How far the hair's roots sit BELOW the crown of the head, in the
        /// mesh's own units.
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

        private void Awake()
        {
            Rebuild();
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

            // The turn that puts the spikes up and out of the scalp rather than
            // down off the back of the head -- see RootsBelowCrown.
            _worn.transform.localRotation = Quaternion.Euler(180.0f, 0.0f, 0.0f);
            _worn.transform.localPosition = new Vector3(
                0.0f,
                FusedBodyShape.HeadRadius - RootsBelowCrown + mesh.bounds.max.y,
                0.0f);

            _worn.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _worn.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HairMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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
            return index < StyleMeshes.Length ? StyleMeshes[index] : null;
        }
    }
}
