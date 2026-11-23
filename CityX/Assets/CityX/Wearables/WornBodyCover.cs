using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// What the garments worn on one body hide of it, kept on its skin: the body is drawn from its whole mesh without every worn garment's
    /// hidden triangles (BodyCover), so a shirt and jeans worn together both cut theirs, and taking them off in any order gives back exactly
    /// what the rest still cover. With none worn the whole body is back, and the next garment put on takes the body as it then is. A garment
    /// here is anything worn that hides skin: the skinned clothes, and the shoes (rigid, on the feet: Shoes).
    ///
    /// With shoes on, a garment hides what it hides then instead (the jeans: the ankles under their hems as well, which they leave drawn for
    /// bare feet; in shoes that skin poked through the shoes when a jump pointed the feet, owner 2026-10-26).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WornBodyCover : MonoBehaviour
    {
        private SkinnedMeshRenderer _skin;
        private Mesh _whole;
        private readonly Dictionary<Object, (byte[] mask, byte[] withShoes, int madeForTriangles, bool shoes)> _worn =
            new Dictionary<Object, (byte[], byte[], int, bool)>();

        /// <summary>
        /// <paramref name="garment"/>'s hidden triangles no longer drawn on <paramref name="skin"/>; <paramref name="withShoes"/> instead while
        /// shoes are worn too (none: the same).
        /// </summary>
        public static void Hide(SkinnedMeshRenderer skin, Object garment, byte[] mask, int madeForTriangles, byte[] withShoes = null)
        {
            Wear(skin, garment, (mask, withShoes, madeForTriangles, false));
        }

        /// <summary>
        /// Shoes' hidden triangles (the feet) no longer drawn on <paramref name="skin"/>, and the garments' with-shoes ones instead of theirs.
        /// </summary>
        public static void HideUnderShoes(SkinnedMeshRenderer skin, Object shoes, byte[] mask, int madeForTriangles)
        {
            Wear(skin, shoes, (mask, null, madeForTriangles, true));
        }

        private static void Wear(SkinnedMeshRenderer skin, Object garment, (byte[], byte[], int, bool) worn)
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
            cover._worn[garment] = worn;
            cover.Draw();
        }

        /// <summary>The body's whole mesh under <paramref name="skin"/>: what it draws while nothing hides any of it (what is drawn may be a cut copy).</summary>
        public static Mesh Whole(SkinnedMeshRenderer skin)
        {
            WornBodyCover cover = skin.GetComponent<WornBodyCover>();
            return cover != null && cover._worn.Count > 0 && cover._whole != null ? cover._whole : skin.sharedMesh;
        }

        /// <summary><paramref name="garment"/> taken off <paramref name="skin"/>: what it alone hid drawn again.</summary>
        public static void Show(SkinnedMeshRenderer skin, Object garment)
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
            bool shod = false;
            foreach (var (_, _, _, shoes) in _worn.Values)
            {
                shod |= shoes;
            }
            var masks = new List<(byte[], int)>(_worn.Count);
            foreach (var (mask, withShoes, madeForTriangles, _) in _worn.Values)
            {
                masks.Add((shod && withShoes != null && withShoes.Length > 0 ? withShoes : mask, madeForTriangles));
            }
            _skin.sharedMesh = BodyCover.Cut(_whole, masks);
        }
    }
}
