using Unity.Netcode;
using UnityEngine;

namespace Pokupka
{
    // Follows the local player from a fixed angle.
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField]
        Vector3 m_Offset = new Vector3(0f, 14f, -10f);
        [SerializeField]
        float m_FollowSharpness = 10f;

        Transform m_Target;

        void Start()
        {
            transform.rotation = Quaternion.LookRotation(-m_Offset);
        }

        void LateUpdate()
        {
            if (m_Target == null)
            {
                m_Target = FindLocalPlayer();
                if (m_Target == null)
                {
                    return;
                }
            }

            Vector3 desired = m_Target.position + m_Offset;
            float t = 1f - Mathf.Exp(-m_FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }

        static Transform FindLocalPlayer()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsClient || networkManager.LocalClient == null)
            {
                return null;
            }
            var playerObject = networkManager.LocalClient.PlayerObject;
            return playerObject != null ? playerObject.transform : null;
        }
    }
}
