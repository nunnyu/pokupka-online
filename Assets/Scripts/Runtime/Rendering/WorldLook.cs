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
        Color m_AmbientColor = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField]
        Color m_FogColor = new Color(0.04f, 0.035f, 0.06f);
        [SerializeField]
        float m_FogStart = 8f;
        [SerializeField]
        float m_FogEnd = 32f;
        [SerializeField, Tooltip("Warm light around the point the camera looks at")]
        Color m_LanternColor = new Color(1f, 0.62f, 0.32f);
        [SerializeField]
        float m_LanternIntensity = 1.4f;
        [SerializeField]
        float m_LanternRange = 9f;
        [SerializeField]
        float m_LanternHeight = 1.5f;
        [SerializeField, Range(0f, 1f)]
        float m_Desaturation = 0.3f;
        [SerializeField, Range(0f, 1f)]
        float m_Vignette = 0.6f;
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
        static readonly int k_LanternPosition = Shader.PropertyToID("_LanternPosition");
        static readonly int k_LanternColor = Shader.PropertyToID("_LanternColor");
        static readonly int k_Desaturation = Shader.PropertyToID("_Desaturation");
        static readonly int k_Vignette = Shader.PropertyToID("_Vignette");
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
            Vector3 lantern = GetLanternPosition();
            Shader.SetGlobalVector(k_LanternPosition, new Vector4(lantern.x, lantern.y, lantern.z, m_LanternRange));
            Shader.SetGlobalColor(k_LanternColor, m_LanternColor * m_LanternIntensity);
            Shader.SetGlobalFloat(k_Desaturation, m_Desaturation);
            Shader.SetGlobalFloat(k_Vignette, m_Vignette);
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

        // Where the camera's view meets the ground, which is the player for the top-down camera.
        Vector3 GetLanternPosition()
        {
            Vector3 forward = transform.forward;
            float distance = forward.y < -0.01f ? transform.position.y / -forward.y : 10f;
            Vector3 ground = transform.position + forward * distance;
            return ground + Vector3.up * m_LanternHeight;
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
