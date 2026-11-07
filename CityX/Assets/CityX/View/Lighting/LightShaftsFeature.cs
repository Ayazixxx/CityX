using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// The light shafts (Shaders/CityXLightShafts.shader, docs/LIGHTING.md) as a renderer feature of their own, before post-processing: the
    /// air traced at half the screen's width and height, then laid over the whole picture. Traced at full size, 24 steps for each of a
    /// 2560x1600 screen's 4 million pixels cost 2.5 ms a frame on the owner's laptop; the beams are soft, so a quarter of the pixels look the
    /// same. Put on the renderers by LightShaftsSetup.
    /// </summary>
    public sealed class LightShaftsFeature : ScriptableRendererFeature
    {
        public Material Material;

        private ShaftsPass _pass;

        public override void Create()
        {
            _pass = new ShaftsPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if (Material == null || type == CameraType.Preview || type == CameraType.Reflection)
            {
                return;
            }
            // With the sun under the horizon there are no shafts; the passes alone (copying the picture) cost 0.4 ms a frame.
            if (CityX.World.CurrentSky.TowardSun.y <= 0.0f)
            {
                return;
            }
            _pass.Material = Material;
            renderer.EnqueuePass(_pass);
        }

        private sealed class ShaftsPass : ScriptableRenderPass
        {
            private const int TracePass = 0;
            private const int LayOverPass = 1;
            private static readonly int ShaftsTextureId = Shader.PropertyToID("_CityXShaftsTexture");
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly Vector4 WholeScreen = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            public Material Material;

            private sealed class TraceData
            {
                public Material Material;
            }

            private sealed class LayOverData
            {
                public Material Material;
                public TextureHandle Picture;
                public TextureHandle Shafts;
            }

            public ShaftsPass()
            {
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                RenderTextureDescriptor half = camera.cameraTargetDescriptor;
                half.width = Mathf.Max(1, half.width / 2);
                half.height = Mathf.Max(1, half.height / 2);
                half.msaaSamples = 1;
                half.depthStencilFormat = GraphicsFormat.None;
                half.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;   // signed: the shadowed air takes light away
                TextureHandle shafts = UniversalRenderer.CreateRenderGraphTexture(renderGraph, half, "_CityXShaftsTexture", false, FilterMode.Bilinear);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("CityX light shafts: trace at half size", out TraceData trace))
                {
                    trace.Material = Material;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseAllGlobalTextures(true);   // the sun's shadow map and the scene's depth, bound by URP as globals
                    builder.SetRenderAttachment(shafts, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (TraceData data, RasterGraphContext context) =>
                    {
                        Block.Clear();
                        Block.SetVector(BlitScaleBiasId, WholeScreen);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, TracePass, MeshTopology.Triangles, 3, 1, Block);
                    });
                }

                // The picture copied (as URP's own full-screen pass does), then drawn back with the light laid over it.
                TextureDesc copy = renderGraph.GetTextureDesc(resources.cameraColor);
                copy.name = "_CityXShaftsPicture";
                copy.clearBuffer = false;
                TextureHandle picture = renderGraph.CreateTexture(copy);
                renderGraph.AddBlitPass(resources.activeColorTexture, picture, Vector2.one, Vector2.zero, passName: "CityX light shafts: copy the picture");

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("CityX light shafts: lay over", out LayOverData layOver))
                {
                    layOver.Material = Material;
                    layOver.Picture = picture;
                    layOver.Shafts = shafts;
                    builder.UseTexture(picture);
                    builder.UseTexture(shafts);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (LayOverData data, RasterGraphContext context) =>
                    {
                        Block.Clear();
                        Block.SetTexture(BlitTextureId, (RTHandle)data.Picture);
                        Block.SetTexture(ShaftsTextureId, (RTHandle)data.Shafts);
                        Block.SetVector(BlitScaleBiasId, WholeScreen);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, LayOverPass, MeshTopology.Triangles, 3, 1, Block);
                    });
                }
            }
        }
    }
}
