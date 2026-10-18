using UnityEngine;

namespace Pokupka
{
    // Smoothly turns the model toward its parent's facing.
    public class ModelFacing : MonoBehaviour
    {
        [SerializeField]
        float m_TurnSharpness = 25f;

        Quaternion m_Rotation;

        void OnEnable()
        {
            m_Rotation = transform.parent.rotation;
        }

        void LateUpdate()
        {
            float t = 1f - Mathf.Exp(-m_TurnSharpness * Time.deltaTime);
            m_Rotation = Quaternion.Slerp(m_Rotation, transform.parent.rotation, t);
            transform.rotation = m_Rotation;
        }
    }
}
