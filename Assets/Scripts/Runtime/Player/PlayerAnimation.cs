using UnityEngine;

namespace Pokupka
{
    // Drives the character animator from how fast the player is actually moving.
    public class PlayerAnimation : MonoBehaviour
    {
        [SerializeField, Tooltip("Movement speed at which the walk animation plays at normal speed")]
        float m_WalkAnimationSpeed = 1.8f;
        [SerializeField]
        float m_Smoothing = 12f;

        static readonly int k_Speed = Animator.StringToHash("Speed");
        static readonly int k_WalkAnimationSpeed = Animator.StringToHash("WalkAnimSpeed");

        Animator m_Animator;
        Vector3 m_LastPosition;
        float m_Speed;

        void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            m_LastPosition = transform.position;
        }

        void Update()
        {
            if (m_Animator == null || Time.deltaTime <= 0f)
            {
                return;
            }

            Vector3 delta = transform.position - m_LastPosition;
            delta.y = 0f;
            m_LastPosition = transform.position;

            float t = 1f - Mathf.Exp(-m_Smoothing * Time.deltaTime);
            m_Speed = Mathf.Lerp(m_Speed, delta.magnitude / Time.deltaTime, t);
            m_Animator.SetFloat(k_Speed, m_Speed);
            m_Animator.SetFloat(k_WalkAnimationSpeed, Mathf.Clamp(m_Speed / m_WalkAnimationSpeed, 0.5f, 3f));
        }
    }
}
