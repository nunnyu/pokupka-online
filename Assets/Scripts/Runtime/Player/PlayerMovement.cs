using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pokupka
{
    // Owner sends the clicked point, server validates it and moves the player.
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField]
        float m_MoveSpeed = 6f;
        [SerializeField, Tooltip("Max distance from the origin on X or Z")]
        float m_WorldHalfSize = 60f;
        [SerializeField, Tooltip("Resend interval while right-click is held")]
        float m_HeldResendInterval = 0.1f;

        static readonly Plane k_Ground = new Plane(Vector3.up, Vector3.zero);
        static readonly Color k_LocalPlayerColor = new Color(0.3f, 0.75f, 0.95f); // temp glup 

        bool m_HasDestination;
        Vector3 m_Destination;
        float m_NextResendTime;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                transform.position = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            }
            if (IsOwner)
            {
                GetComponentInChildren<Renderer>().material.color = k_LocalPlayerColor;
            }
        }

        void Update()
        {
            if (IsOwner)
            {
                ReadInput();
            }
            if (IsServer)
            {
                MoveTowardsDestination();
            }
        }

        void ReadInput()
        {
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (mouse == null || camera == null)
            {
                return;
            }

            bool clicked = mouse.rightButton.wasPressedThisFrame;
            bool heldAndDue = mouse.rightButton.isPressed && Time.time >= m_NextResendTime;
            if (!clicked && !heldAndDue)
            {
                return;
            }

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (k_Ground.Raycast(ray, out float distance))
            {
                m_NextResendTime = Time.time + m_HeldResendInterval;
                RequestMoveServerRpc(ray.GetPoint(distance));
            }
        }

        [ServerRpc]
        void RequestMoveServerRpc(Vector3 destination)
        {
            if (!IsValidDestination(destination))
            {
                return;
            }
            destination.y = transform.position.y;
            m_Destination = destination;
            m_HasDestination = true;
        }

        bool IsValidDestination(Vector3 destination)
        {
            bool isFinite = float.IsFinite(destination.x) && float.IsFinite(destination.y) && float.IsFinite(destination.z);
            return isFinite && Mathf.Abs(destination.x) <= m_WorldHalfSize && Mathf.Abs(destination.z) <= m_WorldHalfSize;
        }

        void MoveTowardsDestination()
        {
            if (!m_HasDestination)
            {
                return;
            }

            Vector3 toDestination = m_Destination - transform.position;
            if (toDestination.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toDestination);
            }

            transform.position = Vector3.MoveTowards(transform.position, m_Destination, m_MoveSpeed * Time.deltaTime);
            if (transform.position == m_Destination)
            {
                m_HasDestination = false;
            }
        }
    }
}
