using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pokupka
{
    // Owner sends the clicked point, server validates it, finds a path with A* and walks it with a CharacterController.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField]
        float m_MoveSpeed = 6f;
        [SerializeField, Tooltip("Max distance from the origin on X or Z")]
        float m_WorldHalfSize = 60f;
        [SerializeField, Tooltip("Resend interval while right-click is held")]
        float m_HeldResendInterval = 0.1f;
        [SerializeField]
        float m_Gravity = 25f;
        [SerializeField, Tooltip("How fast knockback slows down")]
        float m_KnockbackDrag = 8f;
        [SerializeField]
        string m_GroundLayer = "Ground";
        [SerializeField]
        string m_CharacterLayer = "Characters";

        const float k_StuckTimeout = 0.4f;
        const int k_MaxReplans = 2;
        const float k_ArriveDistance = 0.05f;
        const float k_WaypointDistance = 0.35f;

        CharacterController m_Controller;
        ClickSpark m_ClickSpark;
        int m_GroundMask;
        int m_ClickMask;

        readonly List<Vector3> m_Path = new List<Vector3>();
        int m_PathIndex;
        float m_NextResendTime;
        float m_VerticalSpeed;
        float m_StuckTime;
        Vector3 m_Goal;
        int m_Replans;
        Vector3 m_Knockback;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_ClickSpark = GetComponent<ClickSpark>();
            m_GroundMask = LayerMask.GetMask(m_GroundLayer);
            m_ClickMask = ~LayerMask.GetMask(m_CharacterLayer, "UI", "Ignore Raycast");
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Teleport(new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f)));
                NavGrid.Instance.EnsureBuilt();
            }
        }

        // Server only. Adds a push that fades out, e.g. from getting hit.
        public void AddKnockback(Vector3 velocity)
        {
            if (!IsServer)
            {
                return;
            }
            velocity.y = 0f;
            m_Knockback += velocity;
        }

        public void Teleport(Vector3 position)
        {
            m_Controller.enabled = false;
            transform.position = position;
            m_Controller.enabled = true;
        }

        void Update()
        {
            if (IsOwner)
            {
                ReadInput();
            }
            if (IsServer)
            {
                Simulate();
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

            // Only ground is hit, so clicks pass through walls and props to the floor behind them.
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, m_GroundMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            m_NextResendTime = Time.time + m_HeldResendInterval;
            RequestMoveServerRpc(hit.point);
            if (clicked && m_ClickSpark != null)
            {
                m_ClickSpark.Play(hit.point);
            }
        }

        [ServerRpc]
        void RequestMoveServerRpc(Vector3 destination)
        {
            if (!IsValidDestination(destination))
            {
                return;
            }
            m_Goal = destination;
            m_Replans = 0;
            PlanPath();
        }

        void PlanPath()
        {
            Vector3 feet = transform.position + Vector3.down * (m_Controller.height * 0.5f);
            m_PathIndex = 0;
            m_StuckTime = 0f;
            if (!NavGrid.Instance.FindPath(feet, m_Goal, m_Path))
            {
                m_Path.Clear();
            }
        }

        bool IsValidDestination(Vector3 destination)
        {
            bool isFinite = float.IsFinite(destination.x) && float.IsFinite(destination.y) && float.IsFinite(destination.z);
            if (!isFinite || Mathf.Abs(destination.x) > m_WorldHalfSize || Mathf.Abs(destination.z) > m_WorldHalfSize)
            {
                return false;
            }
            // The point has to be on walkable ground, not on top of a wall or prop.
            var from = new Vector3(destination.x, destination.y + 0.5f, destination.z);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 1f, m_ClickMask, QueryTriggerInteraction.Ignore)
                && ((1 << hit.collider.gameObject.layer) & m_GroundMask) != 0;
        }

        void Simulate()
        {
            float deltaTime = Time.deltaTime;
            Vector3 walk = Vector3.zero;
            while (m_PathIndex < m_Path.Count)
            {
                Vector3 toWaypoint = m_Path[m_PathIndex] - transform.position;
                toWaypoint.y = 0f;
                float distance = toWaypoint.magnitude;
                bool isLast = m_PathIndex == m_Path.Count - 1;
                if (distance < (isLast ? k_ArriveDistance : k_WaypointDistance))
                {
                    m_PathIndex++;
                    continue;
                }
                transform.rotation = Quaternion.LookRotation(toWaypoint);
                walk = toWaypoint / distance * Mathf.Min(m_MoveSpeed * deltaTime, distance);
                break;
            }

            m_VerticalSpeed = m_Controller.isGrounded ? -1f : m_VerticalSpeed - m_Gravity * deltaTime;
            Vector3 knockback = m_Knockback * deltaTime;
            m_Knockback = Vector3.Lerp(m_Knockback, Vector3.zero, 1f - Mathf.Exp(-m_KnockbackDrag * deltaTime));

            Vector3 before = transform.position;
            m_Controller.Move(walk + knockback + Vector3.up * (m_VerticalSpeed * deltaTime));

            if (walk.sqrMagnitude > 0f)
            {
                Vector3 moved = transform.position - before;
                moved.y = 0f;
                // Sliding along a wall still counts as progress; only near-zero movement is stuck.
                m_StuckTime = moved.magnitude < walk.magnitude * 0.05f ? m_StuckTime + deltaTime : 0f;
                if (m_StuckTime > k_StuckTimeout)
                {
                    if (m_Replans < k_MaxReplans)
                    {
                        m_Replans++;
                        PlanPath();
                    }
                    else
                    {
                        m_Path.Clear();
                    }
                }
            }
        }
    }
}
