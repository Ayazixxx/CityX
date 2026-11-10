using CityX.World;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// What the wet streets mirror (Shaders/CityXWetMirror.hlsl, docs/WET_STREETS.md). While the city is wet, each frame's picture is kept
    /// before post-processing, at half size, with how far from the eye each of its pixels is and the matrix it was drawn with; the ground looks
    /// its reflections up in them while the next frame is drawn. Only the player's own camera is remembered, and on a dry day nothing is kept
    /// and nothing mirrored. Put on the renderers by WetMirrorSetup.
    /// </summary>
    public sealed class WetMirrorFeature : ScriptableRendererFeature
    {
        [Tooltip("CityX/MirrorMemory: writes each pixel's distance from the eye.")]
        public Material Material;

        /// <summary>How many of its smaller sizes down the film of water between the puddles reads the picture: wet asphalt's blur. A puddle reads it sharp.</summary>
        public const float FilmBlur = 2.5f;

        private static readonly int MirrorId = Shader.PropertyToID("_CityXMirror");
        private static readonly int MatrixId = Shader.PropertyToID("_CityXMirrorMatrix");
        private static readonly int PictureId = Shader.PropertyToID("_CityXMirrorPicture");
        private static readonly int DepthId = Shader.PropertyToID("_CityXMirrorDepth");

        private RememberPass _pass;
        private RTHandle _picture;
        private RTHandle _depth;
        private Matrix4x4 _drawnWith;
        private int _rememberedFrame = NeverRemembered;

        private const int NeverRemembered = -1000;

        /// <summary>Nothing mirrored from now on (the effect switched off: a feature that is off is not asked each frame, so it cannot say so itself).</summary>
        public static void Clear()
        {
            Shader.SetGlobalVector(MirrorId, Vector4.zero);
        }

        public override void Create()
        {
            _pass = new RememberPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (Material == null || renderingData.cameraData.cameraType != CameraType.Game || !camera.CompareTag("MainCamera"))
            {
                return;
            }
            if (CurrentWeather.Wetness <= 0.0f)
            {
                Forget();
                return;
            }

            RenderTextureDescriptor half = renderingData.cameraData.cameraTargetDescriptor;
            half.width = Mathf.Max(1, half.width / 2);
            half.height = Mathf.Max(1, half.height / 2);
            half.msaaSamples = 1;
            half.depthStencilFormat = GraphicsFormat.None;
            half.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            half.useMipMap = true;
            half.autoGenerateMips = false;
            bool made = RenderingUtils.ReAllocateHandleIfNeeded(ref _picture, half, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_CityXMirrorPicture");
            half.graphicsFormat = GraphicsFormat.R32_SFloat;
            half.useMipMap = false;
            made |= RenderingUtils.ReAllocateHandleIfNeeded(ref _depth, half, FilterMode.Point, TextureWrapMode.Clamp, name: "_CityXMirrorDepth");

            // What the frame before left, for this frame's ground: nothing in the first wet frame, after the window changed size, or when
            // the last picture kept is an old one (the effect was off meanwhile).
            bool usable = !made && Time.frameCount - _rememberedFrame <= 1;
            Shader.SetGlobalVector(MirrorId, new Vector4(usable ? 1.0f : 0.0f, FilmBlur, 0.0f, 0.0f));
            if (usable)
            {
                Shader.SetGlobalMatrix(MatrixId, _drawnWith);
                Shader.SetGlobalTexture(PictureId, _picture);
                Shader.SetGlobalTexture(DepthId, _depth);
            }

            _drawnWith = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
            _rememberedFrame = Time.frameCount;
            _pass.Material = Material;
            _pass.Picture = _picture;
            _pass.Depth = _depth;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            Forget();
            _picture?.Release();
            _depth?.Release();
            _picture = null;
            _depth = null;
        }

        private void Forget()
        {
            if (_rememberedFrame != NeverRemembered)
            {
                _rememberedFrame = NeverRemembered;
                Clear();
            }
        }

        private sealed class RememberPass : ScriptableRenderPass
        {
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly Vector4 WholeScreen = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            public Material Material;
            public RTHandle Picture;
            public RTHandle Depth;

            private sealed class DistanceData
            {
                public Material Material;
            }

            private sealed class SizesData
            {
                public TextureHandle Picture;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }
                TextureHandle picture = renderGraph.ImportTexture(Picture);
                TextureHandle depth = renderGraph.ImportTexture(Depth);

                renderGraph.AddBlitPass(resources.activeColorTexture, picture, Vector2.one, Vector2.zero, passName: "CityX wet mirror: remember the picture");

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("CityX wet mirror: the picture's smaller sizes", out SizesData sizes))
                {
                    sizes.Picture = picture;
                    builder.UseTexture(picture, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (SizesData data, UnsafeGraphContext context) =>
                    {
                        context.cmd.GenerateMips(((RTHandle)data.Picture).rt);
                    });
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("CityX wet mirror: remember the distances", out DistanceData distance))
                {
                    distance.Material = Material;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseAllGlobalTextures(true);   // the scene's depth, bound by URP as a global
                    builder.SetRenderAttachment(depth, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (DistanceData data, RasterGraphContext context) =>
                    {
                        Block.Clear();
                        Block.SetVector(BlitScaleBiasId, WholeScreen);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, Block);
                    });
                }
            }
        }
    }
}
