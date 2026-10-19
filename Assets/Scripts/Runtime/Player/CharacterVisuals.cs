using UnityEngine;

namespace Pokupka
{
    // Puts the character's renderers on the full resolution layer and gives them the character material.
    public class CharacterVisuals : MonoBehaviour
    {
        [SerializeField]
        Transform m_Visuals;
        [SerializeField]
        Material m_Material;
        [SerializeField]
        string m_Layer = "Characters";

        void Awake()
        {
            Transform root = m_Visuals != null ? m_Visuals : transform;
            int layer = LayerMask.NameToLayer(m_Layer);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (layer >= 0)
                {
                    child.gameObject.layer = layer;
                }
            }
            if (m_Material == null)
            {
                return;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = m_Material;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
