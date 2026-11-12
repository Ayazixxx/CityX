using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Items
{
    /// <summary>
    /// HAIR TUCKED UNDER WHAT IS SLUNG ON THE BACK (owner, 2026-11-12: "the hairstyles clip through the boomerang when its worn on the back ... is there a way
    /// to make any amount of hair that passes through it invisible on the other side"). A big weapon carried on the back (<see cref="BackCarry"/>) shows its
    /// board here while it is there; before each frame is drawn the boards go to the shader as globals, and a material with the _BACKCLIP keyword (the
    /// hair's) leaves out what of it is past a board's inner face inside its outline (View/Shaders/CityXBackClip.hlsl). Every screen draws its own, so every
    /// player sees it, and other players' copies too.
    /// </summary>
    public static class BackClip
    {
        /// <summary>The most boards at once (CITYX_BACK_CLIP_MAX in the shader): the nearest bodies' carries.</summary>
        public const int Max = 8;

        /// <summary>The shader keyword a material turns on to be tucked under the boards.</summary>
        public const string Keyword = "_BACKCLIP";

        private static readonly int ToBoardId = Shader.PropertyToID("_BackClipToBoard");
        private static readonly int CountId = Shader.PropertyToID("_BackClipCount");
        private static readonly int VeeAId = Shader.PropertyToID("_BackClipVeeA");
        private static readonly int VeeBId = Shader.PropertyToID("_BackClipVeeB");

        private struct Board
        {
            public Transform Frame;
            public Vector4 VeeA, VeeB;
        }

        private static readonly List<Board> Boards = new List<Board>();
        private static readonly Matrix4x4[] ToBoard = new Matrix4x4[Max];
        private static readonly Vector4[] VeeA = new Vector4[Max];
        private static readonly Vector4[] VeeB = new Vector4[Max];
        private static bool _hooked;

        /// <summary>The boards shown now (for tests).</summary>
        public static int Count => Boards.Count;

        /// <summary>
        /// Shows a board slung on a back: its frame (x across, y out of its face away from the back, z along it) and its outline in that frame, a V from one
        /// tip through the elbow to the other tip (<paramref name="tipAndElbow"/>: tip x, z, elbow x, z; <paramref name="tipWidthDepth"/>: other tip x, z,
        /// each arm's half width, how far behind its middle its inner face is). Shown again, it is updated.
        /// </summary>
        public static void Show(Transform frame, Vector4 tipAndElbow, Vector4 tipWidthDepth)
        {
            Hook();
            int i = Boards.FindIndex(b => b.Frame == frame);
            var board = new Board { Frame = frame, VeeA = tipAndElbow, VeeB = tipWidthDepth };
            if (i >= 0) Boards[i] = board;
            else Boards.Add(board);
        }

        public static void Hide(Transform frame)
        {
            Boards.RemoveAll(b => b.Frame == frame || b.Frame == null);
        }

        private static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            _hooked = true;
            RenderPipelineManager.beginContextRendering += (context, cameras) => Upload();
        }

        /// <summary>Sends the boards to the shader (done before each frame is drawn).</summary>
        public static void Upload()
        {
            Boards.RemoveAll(b => b.Frame == null);
            int count = Mathf.Min(Boards.Count, Max);
            for (int i = 0; i < Max; i++)
            {
                if (i < count)
                {
                    ToBoard[i] = Boards[i].Frame.worldToLocalMatrix;
                    VeeA[i] = Boards[i].VeeA;
                    VeeB[i] = Boards[i].VeeB;
                }
                else
                {
                    ToBoard[i] = Matrix4x4.identity;
                    VeeA[i] = VeeB[i] = Vector4.zero;
                }
            }
            Shader.SetGlobalMatrixArray(ToBoardId, ToBoard);
            Shader.SetGlobalVectorArray(VeeAId, VeeA);
            Shader.SetGlobalVectorArray(VeeBId, VeeB);
            Shader.SetGlobalFloat(CountId, count);
        }
    }
}
