using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The property blocks a renderer has on each of its materials. A copy of a renderer (a cut-off head, a burst's chunk, the HUD's portrait)
    /// needs them as well as the whole renderer's: the hair's dye is on its material's own block (CharacterHair), and a material's own block
    /// wins over the whole renderer's, so a copy without it came out in the hair material's gold (owner, 2026-11-08: "when my player got
    /// decapitated the hair color changed on the decapitated head").
    /// </summary>
    public static class MaterialBlocks
    {
        /// <summary>The blocks on each of <paramref name="source"/>'s first <paramref name="count"/> materials; null for one that has none.</summary>
        public static MaterialPropertyBlock[] Of(Renderer source, int count)
        {
            var blocks = new MaterialPropertyBlock[count];
            for (int i = 0; i < count; i++)
            {
                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block, i);
                blocks[i] = block.isEmpty ? null : block;
            }
            return blocks;
        }

        /// <summary>Gives <paramref name="target"/>'s first <paramref name="count"/> materials the blocks <paramref name="source"/>'s have.</summary>
        public static void Copy(Renderer source, Renderer target, int count)
        {
            MaterialPropertyBlock[] blocks = Of(source, count);
            for (int i = 0; i < count; i++)
            {
                if (blocks[i] != null)
                {
                    target.SetPropertyBlock(blocks[i], i);
                }
            }
        }
    }
}
