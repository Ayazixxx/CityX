using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// What the garments worn on one body hide of it, kept on its skin: the body is drawn from its whole mesh without every worn garment's
    /// hidden triangles (BodyCover), so a shirt and jeans worn together both cut theirs, and taking them off in any order gives back exactly
    /// what the rest still cover. With none worn the whole body is back, and the next garment put on takes the body as it then is.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WornBodyCover : MonoBehaviour
    {
        private SkinnedMeshRenderer _skin;
        private Mesh _whole;
        private readonly Dictionary<SkinnedGarment, (byte[] mask, int madeForTriangles)> _worn =
            new Dictionary<SkinnedGarment, (byte[], int)>();

        /// <summary><paramref name="garment"/>'s hidden triangles no longer drawn on <paramref name="skin"/>.</summary>
        public static void Hide(SkinnedMeshRenderer skin, SkinnedGarment garment, byte[] mask, int madeForTriangles)
        {
            WornBodyCover cover = skin.GetComponent<WornBodyCover>();
            if (cover == null)
            {
                cover = skin.gameObject.AddComponent<WornBodyCover>();
                cover._skin = skin;
            }
            if (cover._worn.Count == 0)
            {
                cover._whole = skin.sharedMesh;
            }
            cover._worn[garment] = (mask, madeForTriangles);
            cover.Draw();
        }

        /// <summary><paramref name="garment"/> taken off <paramref name="skin"/>: what it alone hid drawn again.</summary>
        public static void Show(SkinnedMeshRenderer skin, SkinnedGarment garment)
        {
            WornBodyCover cover = skin != null ? skin.GetComponent<WornBodyCover>() : null;
            if (cover == null || !cover._worn.Remove(garment))
            {
                return;
            }
            cover.Draw();
        }

        private void Draw()
        {
            _skin.sharedMesh = BodyCover.Cut(_whole, new List<(byte[], int)>(_worn.Values));
        }
    }
}
