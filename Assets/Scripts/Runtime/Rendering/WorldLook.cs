using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pokupka
{
    // Drives the global settings of the shader and the low render resolution.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class WorldLook : MonoBehaviour
    {
        [SerializeField]
        Light m_Sun;
        [SerializeField]
        Color m_AmbientColor = new Color(0.32f, 0.3f, 0.4f);
        [SerializeField]
        Color m_FogColor = new Color(0.16f, 0.14f, 0.22f);
        [SerializeField]
        float m_FogStart = 18f;
        [SerializeField]
        float m_FogEnd = 55f;
        [SerializeField, Tooltip("Vertical resolution the game renders at (play mode only)")]
        int m_RenderHeight = 240;
        [SerializeField, Tooltip("Vertical resolution vertices snap to, lower = more wobble")]
        int m_SnapHeight = 240;
        [SerializeField, Range(2, 256), Tooltip("Color levels per channel")]
        int m_ColorLevels = 32;
        [SerializeField, Range(0f, 1f)]
        float m_AffineStrength = 1f;

        static readonly int k_SnapResolution = Shader.PropertyToID("_VertexSnapResolution");
        static readonly int k_LightDir = Shader.PropertyToID("_SunDirection");
        static readonly int k_LightColor = Shader.PropertyToID("_SunColor");
        static readonly int k_AmbientColor = Shader.PropertyToID("_AmbientColor");
        static readonly int k_FogColor = Shader.PropertyToID("_FogColor");
        static readonly int k_FogRange = Shader.PropertyToID("_FogRange");
        static readonly int k_ColorLevels = Shader.PropertyToID("_ColorLevels");
        static readonly int k_AffineStrength = Shader.PropertyToID("_AffineStrength");

        Camera m_Camera;
        float m_OriginalRenderScale = -1f;

        void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
            ApplyGlobals();
        }

        void Update()
        {
            ApplyGlobals();
            if (Application.isPlaying)
            {
                ApplyRenderScale();
            }
        }

        void OnDisable()
        {
            RestoreRenderScale();
        }

        void OnValidate()
        {
            if (m_Camera != null)
            {
                ApplyGlobals();
            }
        }

        void ApplyGlobals()
        {
            Vector3 toLight = m_Sun != null ? -m_Sun.transform.forward : new Vector3(0.4f, 0.8f, -0.4f).normalized;
            Color lightColor = m_Sun != null ? m_Sun.color * m_Sun.intensity : Color.white;

            Shader.SetGlobalVector(k_SnapResolution, new Vector4(m_SnapHeight * m_Camera.aspect, m_SnapHeight, 0f, 0f));
            Shader.SetGlobalVector(k_LightDir, toLight);
            Shader.SetGlobalColor(k_LightColor, lightColor);
            Shader.SetGlobalColor(k_AmbientColor, m_AmbientColor);
            Shader.SetGlobalColor(k_FogColor, m_FogColor);
            Shader.SetGlobalVector(k_FogRange, new Vector4(m_FogStart, m_FogEnd, 0f, 0f));
            Shader.SetGlobalFloat(k_ColorLevels, m_ColorLevels);
            Shader.SetGlobalFloat(k_AffineStrength, m_AffineStrength);

            if (m_Camera.clearFlags != CameraClearFlags.SolidColor)
            {
                m_Camera.clearFlags = CameraClearFlags.SolidColor;
            }
            if (m_Camera.backgroundColor != m_FogColor)
            {
                m_Camera.backgroundColor = m_FogColor;
            }
        }

        void ApplyRenderScale()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline)
            {
                return;
            }
            if (m_OriginalRenderScale < 0f)
            {
                m_OriginalRenderScale = pipeline.renderScale;
            }
            pipeline.renderScale = Mathf.Clamp((float)m_RenderHeight / Screen.height, 0.1f, 1f);
        }

        void RestoreRenderScale()
        {
            if (m_OriginalRenderScale >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.renderScale = m_OriginalRenderScale;
            }
            m_OriginalRenderScale = -1f;
        }
    }
}
