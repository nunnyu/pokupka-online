using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PkillGame.Rendering
{
    // Add to your Universal Renderer asset (Add Renderer Feature → PS1RendererFeature).
    // Assign Assets/Materials/PS1PostProcess.mat (shader: pkill9/PS1Post) to the Material field.
    public class PS1RendererFeature : ScriptableRendererFeature
    {
        [SerializeField] Material m_Material;

        [Header("PS1 Settings")]
        [Range(120f, 480f)] public float pixelRows   = 240f;
        [Range(1f,   8f)]   public float colorBits   = 5f;
        [Range(0f,   0.5f)] public float scanlineStr = 0.15f;
        [Range(0f,   1f)]   public float ditherStr   = 0.85f;

        PS1Pass m_Pass;

        public override void Create()
        {
            m_Pass = new PS1Pass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null) return;

            m_Material.SetFloat("_PixelRows",   pixelRows);
            m_Material.SetFloat("_ColorBits",   colorBits);
            m_Material.SetFloat("_ScanlineStr", scanlineStr);
            m_Material.SetFloat("_DitherStr",   ditherStr);

            m_Pass.Setup(m_Material);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing) => m_Pass?.Cleanup();

        // ── Render pass ───────────────────────────────────────────────────────────

        class PS1Pass : ScriptableRenderPass
        {
            Material m_Material;
            RTHandle m_TempHandle;

            public void Setup(Material mat) => m_Material = mat;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref m_TempHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PS1Temp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("PS1Post");
                RTHandle source   = renderingData.cameraData.renderer.cameraColorTargetHandle;

                using (new ProfilingScope(cmd, new ProfilingSampler("PS1 Post Process")))
                {
                    // src → temp with PS1 effect, then temp → src (copy back)
                    Blitter.BlitCameraTexture(cmd, source, m_TempHandle, m_Material, 0);
                    Blitter.BlitCameraTexture(cmd, m_TempHandle, source);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                m_TempHandle?.Release();
                m_TempHandle = null;
            }
        }
    }
}
