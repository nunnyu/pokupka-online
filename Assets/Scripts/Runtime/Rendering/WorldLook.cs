using UnityEngine;
using UnityEngine.Rendering;

namespace Pokupka
{
    // Drives the global settings of the shader. In play mode the world is rendered by a hidden
    // low resolution camera and shown behind the characters, which this camera draws at full resolution.
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
        [SerializeField, Tooltip("Vertical resolution the world renders at (play mode only)")]
        int m_RenderHeight = 240;
        [SerializeField, Tooltip("Vertical resolution vertices snap to, lower = more wobble")]
        int m_SnapHeight = 240;
        [SerializeField, Range(2, 256), Tooltip("Color levels per channel")]
        int m_ColorLevels = 32;
        [SerializeField, Range(0f, 1f)]
        float m_AffineStrength = 1f;
        [SerializeField]
        Shader m_DisplayShader;
        [SerializeField, Tooltip("Objects on this layer are drawn at full resolution on top of the world")]
        string m_CharacterLayer = "Characters";

        static readonly int k_SnapResolution = Shader.PropertyToID("_VertexSnapResolution");
        static readonly int k_LightDir = Shader.PropertyToID("_SunDirection");
        static readonly int k_LightColor = Shader.PropertyToID("_SunColor");
        static readonly int k_AmbientColor = Shader.PropertyToID("_AmbientColor");
        static readonly int k_FogColor = Shader.PropertyToID("_FogColor");
        static readonly int k_FogRange = Shader.PropertyToID("_FogRange");
        static readonly int k_ColorLevels = Shader.PropertyToID("_ColorLevels");
        static readonly int k_AffineStrength = Shader.PropertyToID("_AffineStrength");
        static readonly int k_WorldTexture = Shader.PropertyToID("_WorldTexture");

        Camera m_Camera;
        Camera m_WorldCamera;
        RenderTexture m_WorldTexture;
        Material m_DisplayMaterial;
        GameObject m_Display;
        int m_OriginalCullingMask;

        void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
            ApplyGlobals();
        }

        void Update()
        {
            ApplyGlobals();
            if (!Application.isPlaying)
            {
                return;
            }
            if (m_WorldCamera == null)
            {
                StartSplitRendering();
            }
            if (m_WorldCamera != null)
            {
                SyncWorldCamera();
            }
        }

        void OnDisable()
        {
            StopSplitRendering();
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

        void StartSplitRendering()
        {
            int characterLayer = LayerMask.NameToLayer(m_CharacterLayer);
            if (m_DisplayShader == null || characterLayer < 0)
            {
                Debug.LogWarning("[WorldLook] Needs a display shader and a character layer to render the world at low resolution.");
                enabled = false;
                return;
            }
            int uiLayer = LayerMask.NameToLayer("UI");
            int characterMask = 1 << characterLayer;
            int uiMask = uiLayer >= 0 ? 1 << uiLayer : 0;

            m_OriginalCullingMask = m_Camera.cullingMask;
            var worldObject = new GameObject("World Camera");
            worldObject.transform.SetParent(transform, false);
            m_WorldCamera = worldObject.AddComponent<Camera>();
            m_WorldCamera.CopyFrom(m_Camera);
            m_WorldCamera.cullingMask = m_OriginalCullingMask & ~characterMask & ~uiMask;
            m_WorldCamera.depth = m_Camera.depth - 1f;
            m_Camera.cullingMask = characterMask | uiMask;

            m_DisplayMaterial = new Material(m_DisplayShader);
            m_Display = new GameObject("World Display") { layer = uiLayer >= 0 ? uiLayer : characterLayer };
            m_Display.transform.SetParent(transform, false);
            m_Display.transform.localPosition = Vector3.forward * (m_Camera.nearClipPlane + 1f);
            m_Display.AddComponent<MeshFilter>().sharedMesh = CreateDisplayMesh();
            var displayRenderer = m_Display.AddComponent<MeshRenderer>();
            displayRenderer.sharedMaterial = m_DisplayMaterial;
            displayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void SyncWorldCamera()
        {
            m_WorldCamera.fieldOfView = m_Camera.fieldOfView;
            m_WorldCamera.orthographic = m_Camera.orthographic;
            m_WorldCamera.orthographicSize = m_Camera.orthographicSize;
            m_WorldCamera.nearClipPlane = m_Camera.nearClipPlane;
            m_WorldCamera.farClipPlane = m_Camera.farClipPlane;
            m_WorldCamera.backgroundColor = m_FogColor;

            int height = Mathf.Max(16, m_RenderHeight);
            int width = Mathf.Max(16, Mathf.RoundToInt(height * m_Camera.aspect));
            if (m_WorldTexture != null && m_WorldTexture.width == width && m_WorldTexture.height == height)
            {
                return;
            }
            ReleaseWorldTexture();
            m_WorldTexture = new RenderTexture(width, height, 24) { name = "World", filterMode = FilterMode.Point };
            m_WorldTexture.Create();
            m_WorldCamera.targetTexture = m_WorldTexture;
            m_DisplayMaterial.SetTexture(k_WorldTexture, m_WorldTexture);
        }

        void StopSplitRendering()
        {
            if (m_WorldCamera == null)
            {
                return;
            }
            m_Camera.cullingMask = m_OriginalCullingMask;
            ReleaseWorldTexture();
            Destroy(m_WorldCamera.gameObject);
            Destroy(m_Display);
            Destroy(m_DisplayMaterial);
            m_WorldCamera = null;
        }

        void ReleaseWorldTexture()
        {
            if (m_WorldTexture == null)
            {
                return;
            }
            m_WorldCamera.targetTexture = null;
            m_WorldTexture.Release();
            Destroy(m_WorldTexture);
            m_WorldTexture = null;
        }

        static Mesh CreateDisplayMesh()
        {
            var mesh = new Mesh { name = "World Display" };
            mesh.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.up });
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }
    }
}
