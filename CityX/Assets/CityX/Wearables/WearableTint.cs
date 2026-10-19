using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Per-renderer colour/smoothness override on a material SHARED across
    /// a whole wearable category -- see CharacterSkin.EnsureSharedCategory's
    /// own doc comment for why a category (every helmet, say) references one
    /// Material asset rather than one per item. Lives here, outside
    /// Character/Editor/, because it has to be callable at RUNTIME (a
    /// wearable's own Awake, every time it is instantiated) as well as from
    /// an editor builder -- CharacterSkin itself needs AssetDatabase and
    /// only compiles in the editor.
    ///
    /// APPLIED AT RUNTIME, NOT BAKED AT BUILD TIME, on purpose --
    /// MaterialPropertyBlock is documented as a per-instance runtime
    /// override, not something a saved prefab is guaranteed to carry
    /// through PrefabUtility.SaveAsPrefabAsset the way a real asset
    /// reference does (the same caution CharacterSkin.EnsurePainted's own
    /// doc comment already applies to a loose Texture2D). Re-applying every
    /// time a wearable actually loads -- see TopHat.Awake --
    /// sidesteps that question entirely instead of trusting it either way.
    /// </summary>
    public static class WearableTint
    {
        public static void Apply(Renderer renderer, Color colour, float smoothness = 0.2f, int materialIndex = 0)
        {
            if (renderer == null)
            {
                return;
            }
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, materialIndex);
            block.SetColor("_BaseColor", colour);
            block.SetFloat("_Smoothness", smoothness);
            renderer.SetPropertyBlock(block, materialIndex);
        }
    }
}
