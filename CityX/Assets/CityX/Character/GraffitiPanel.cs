using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// One piece of graffiti on a wall: a square quad laid flat on it, a centimetre proud, drawn with a LIT material (owner, 2026-10-15: "make
    /// the light bounce off properly ingame as if it was apart of the wall texture"), so the sun, the street and the room lights, and the
    /// shadows, fall on the paint as they fall on the wall under it. The paint is the piece's GraffitiCanvas in a texture of its own.
    /// </summary>
    public sealed class GraffitiPanel : MonoBehaviour
    {
        public GraffitiPiece Piece { get; private set; }

        /// <summary>One canvas every panel decodes its paint into on its way to the texture (the main thread only): a piece keeps no pixels of its own.</summary>
        private static GraffitiCanvas _scratch;

        private Texture2D _texture;
        private Material _material;
        private MeshRenderer _renderer;
        private static Mesh _quad;

        /// <summary>The paint on the GPU: compressed (DXT5, a quarter of the memory) where the graphics card can, and not kept readable.</summary>
        public Texture2D Texture => _texture;

        public static GraffitiPanel Create(int id)
        {
            var go = new GameObject("Graffiti" + id);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var panel = go.AddComponent<GraffitiPanel>();
            panel._renderer = go.AddComponent<MeshRenderer>();
            panel._renderer.shadowCastingMode = ShadowCastingMode.Off;
            panel._renderer.receiveShadows = true;
            panel._material = MakeMaterial(null);
            panel._renderer.sharedMaterial = panel._material;
            return panel;
        }

        /// <summary>Shows a piece: lays the quad on its wall and uploads its paint. False when its paint will not decode.</summary>
        public bool Show(GraffitiPiece piece)
        {
            _scratch ??= new GraffitiCanvas();
            if (!_scratch.Decode(piece.Paint))
            {
                return false;
            }
            Piece = piece;
            Vector3 normal = piece.Normal.normalized;
            GraffitiWall.AcrossOf(normal, out Vector3 up);
            // The quad faces -z (Quad()): its back to the wall, its face out along the normal; its +x is the canvas's across (AcrossOf).
            transform.SetPositionAndRotation(piece.Centre + normal * GraffitiWall.Proud, Quaternion.LookRotation(-normal, up));
            transform.localScale = new Vector3(piece.Size, piece.Size, 1.0f);
            Upload(_scratch.Pixels, "Graffiti" + piece.Id);
            return true;
        }

        /// <summary>
        /// The paint into a texture of its own: a new one each time (a compressed texture cannot be painted into again), mipmapped so a piece
        /// seen from down the street does not shimmer, compressed to DXT5 (87 KB instead of 349 KB with its mips) and its CPU copy let go of.
        /// </summary>
        private void Upload(Color32[] pixels, string name)
        {
            var texture = new Texture2D(GraffitiCanvas.Size, GraffitiCanvas.Size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            texture.Compress(false);
            texture.Apply(false, true);
            _material.SetTexture("_BaseMap", texture);
            _material.mainTexture = texture;
            if (_texture != null)
            {
                Destroy(_texture);
            }
            _texture = texture;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        /// <summary>A unit square in x and y facing -z, its UVs the canvas's (0,0 bottom left as the wall is seen).</summary>
        private static Mesh Quad()
        {
            if (_quad != null)
            {
                return _quad;
            }
            _quad = new Mesh { name = "GraffitiQuad", hideFlags = HideFlags.DontSave };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.5f, -0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.0f), new Vector3(-0.5f, 0.5f, 0.0f),
            };
            // Seen from the front (from the street, looking along +z into the wall) x runs to the right and y up: the canvas as it was drawn.

            _quad.uv = new[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            _quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
            _quad.RecalculateBounds();
            return _quad;
        }

        private static Material MakeMaterial(Texture2D texture)
        {
            Material material;
            if (GraffitiWall.PaintMaterial != null)
            {
                material = new Material(GraffitiWall.PaintMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                material.SetFloat("_Surface", 1.0f);
                material.SetFloat("_Blend", 0.0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0.0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetFloat("_Smoothness", 0.25f);
            }
            material.name = "GraffitiPaint";
            material.hideFlags = HideFlags.DontSave;
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
            }
            material.SetColor("_BaseColor", Color.white);
            return material;
        }
    }
}
