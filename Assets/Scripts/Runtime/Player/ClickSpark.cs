using UnityEngine;
using UnityEngine.Rendering;

namespace Pokupka
{
    // Small sparkle on the ground where the local player clicked to move.
    public class ClickSpark : MonoBehaviour
    {
        [SerializeField]
        Material m_Material;
        [SerializeField]
        float m_Size = 0.9f;
        [SerializeField]
        float m_Lifetime = 0.35f;

        Transform m_Spark;
        float m_Age = -1f;

        public void Play(Vector3 position)
        {
            if (m_Material == null)
            {
                return;
            }
            if (m_Spark == null)
            {
                m_Spark = CreateSpark();
            }
            m_Spark.position = position + Vector3.up * 0.02f;
            m_Spark.gameObject.SetActive(true);
            m_Age = 0f;
            Update();
        }

        void Update()
        {
            if (m_Age < 0f || m_Spark == null)
            {
                return;
            }

            float t = m_Age / m_Lifetime;
            m_Age += Time.deltaTime;
            if (t >= 1f)
            {
                m_Spark.gameObject.SetActive(false);
                m_Age = -1f;
                return;
            }
            m_Spark.localScale = Vector3.one * (m_Size * Mathf.Sqrt(Mathf.Sin(t * Mathf.PI)));
            m_Spark.rotation = Quaternion.Euler(0f, t * 90f, 0f);
        }

        void OnDestroy()
        {
            if (m_Spark != null)
            {
                Destroy(m_Spark.gameObject);
            }
        }

        Transform CreateSpark()
        {
            var spark = new GameObject("ClickSpark");
            spark.AddComponent<MeshFilter>().sharedMesh = CreateQuad();
            var renderer = spark.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = m_Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return spark.transform;
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "ClickSpark" };
            mesh.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            return mesh;
        }
    }
}
