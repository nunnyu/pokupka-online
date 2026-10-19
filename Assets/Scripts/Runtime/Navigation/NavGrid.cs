using System.Collections.Generic;
using UnityEngine;

namespace Pokupka
{
    // Walkability grid over the world plus A* pathfinding on it. Used by the server to route movement.
    public class NavGrid : MonoBehaviour
    {
        [SerializeField, Tooltip("Covers -HalfSize..HalfSize on X and Z")]
        float m_HalfSize = 60f;
        [SerializeField]
        float m_CellSize = 0.5f;
        [SerializeField, Tooltip("Radius of the space a walker needs")]
        float m_AgentRadius = 0.4f;
        [SerializeField]
        float m_AgentHeight = 2f;
        [SerializeField]
        string m_GroundLayer = "Ground";
        [SerializeField, Tooltip("Layers that never block walking (besides ground)")]
        string[] m_IgnoredLayers = { "Characters", "UI", "Ignore Raycast" };
        [SerializeField, Tooltip("Stops searches that explore too many cells")]
        int m_MaxExpandedCells = 40000;

        static NavGrid s_Instance;

        int m_Width;
        bool[] m_Walkable;
        float[] m_GroundHeight;
        bool m_Built;

        // Reused between searches so nothing is allocated per click.
        float[] m_Cost;
        int[] m_Parent;
        int[] m_Visited;
        int m_SearchId;
        readonly MinHeap m_Open = new MinHeap();

        static readonly Vector2Int[] k_Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        public static NavGrid Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<NavGrid>();
                }
                if (s_Instance == null)
                {
                    s_Instance = new GameObject("NavGrid").AddComponent<NavGrid>();
                }
                return s_Instance;
            }
        }

        public void Build()
        {
            m_Width = Mathf.CeilToInt(m_HalfSize * 2f / m_CellSize);
            int count = m_Width * m_Width;
            m_Walkable = new bool[count];
            m_GroundHeight = new float[count];
            m_Cost = new float[count];
            m_Parent = new int[count];
            m_Visited = new int[count];

            int groundMask = LayerMask.GetMask(m_GroundLayer);
            int obstacleMask = ~(groundMask | LayerMask.GetMask(m_IgnoredLayers));
            for (int z = 0; z < m_Width; z++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    int index = x + z * m_Width;
                    Vector3 center = CellCenter(x, z);
                    var from = new Vector3(center.x, 50f, center.z);
                    if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    m_GroundHeight[index] = hit.point.y;
                    Vector3 bottom = hit.point + Vector3.up * (m_AgentRadius + 0.1f);
                    Vector3 top = hit.point + Vector3.up * (m_AgentHeight - m_AgentRadius);
                    m_Walkable[index] = !Physics.CheckCapsule(bottom, top, m_AgentRadius, obstacleMask, QueryTriggerInteraction.Ignore);
                }
            }
            m_Built = true;
        }

        // Returns smoothed waypoints from start to (the nearest reachable cell to) end, or false if there's no path.
        public bool FindPath(Vector3 start, Vector3 end, List<Vector3> path)
        {
            path.Clear();
            if (!m_Built)
            {
                Build();
            }
            if (!TryGetCell(start, out int startCell) || !TryGetCell(end, out int endCell))
            {
                return false;
            }
            startCell = NearestWalkable(startCell);
            endCell = NearestWalkable(endCell);
            if (startCell < 0 || endCell < 0)
            {
                return false;
            }

            if (!Search(startCell, endCell))
            {
                return false;
            }

            var cells = new List<int>();
            for (int cell = endCell; cell != startCell; cell = m_Parent[cell])
            {
                cells.Add(cell);
            }
            cells.Add(startCell);
            cells.Reverse();
            Smooth(cells, path);

            if (endCell == CellIndex(end))
            {
                path[path.Count - 1] = new Vector3(end.x, path[path.Count - 1].y, end.z);
            }
            return true;
        }

        bool Search(int startCell, int endCell)
        {
            m_SearchId++;
            m_Open.Clear();
            Visit(startCell, 0f, startCell);
            m_Open.Push(startCell, Heuristic(startCell, endCell));

            int expanded = 0;
            while (m_Open.Count > 0)
            {
                int current = m_Open.Pop();
                if (current == endCell)
                {
                    return true;
                }
                if (++expanded > m_MaxExpandedCells)
                {
                    return false;
                }

                int cx = current % m_Width;
                int cz = current / m_Width;
                foreach (Vector2Int step in k_Neighbours)
                {
                    int nx = cx + step.x;
                    int nz = cz + step.y;
                    if (!IsWalkable(nx, nz))
                    {
                        continue;
                    }
                    bool diagonal = step.x != 0 && step.y != 0;
                    // No squeezing diagonally past a blocked corner.
                    if (diagonal && (!IsWalkable(cx + step.x, cz) || !IsWalkable(cx, cz + step.y)))
                    {
                        continue;
                    }

                    int neighbour = nx + nz * m_Width;
                    float cost = m_Cost[current] + (diagonal ? 1.41421356f : 1f);
                    if (m_Visited[neighbour] == m_SearchId && cost >= m_Cost[neighbour])
                    {
                        continue;
                    }
                    Visit(neighbour, cost, current);
                    m_Open.Push(neighbour, cost + Heuristic(neighbour, endCell));
                }
            }
            return false;
        }

        void Visit(int cell, float cost, int parent)
        {
            m_Visited[cell] = m_SearchId;
            m_Cost[cell] = cost;
            m_Parent[cell] = parent;
        }

        // Octile distance: the exact cost on an 8-direction grid with no obstacles.
        float Heuristic(int from, int to)
        {
            int dx = Mathf.Abs(from % m_Width - to % m_Width);
            int dz = Mathf.Abs(from / m_Width - to / m_Width);
            return Mathf.Max(dx, dz) + 0.41421356f * Mathf.Min(dx, dz);
        }

        // Keeps only the cells where the path has to turn.
        void Smooth(List<int> cells, List<Vector3> path)
        {
            int anchor = 0;
            for (int i = 2; i < cells.Count; i++)
            {
                if (!HasLineOfSight(cells[anchor], cells[i]))
                {
                    anchor = i - 1;
                    path.Add(CellPoint(cells[anchor]));
                }
            }
            path.Add(CellPoint(cells[cells.Count - 1]));
        }

        bool HasLineOfSight(int from, int to)
        {
            Vector3 a = CellPoint(from);
            Vector3 b = CellPoint(to);
            float distance = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int steps = Mathf.CeilToInt(distance / (m_CellSize * 0.5f));
            for (int i = 1; i < steps; i++)
            {
                Vector3 point = Vector3.Lerp(a, b, (float)i / steps);
                // Check a little to each side too, so the walker's body fits along the line.
                if (!IsWalkableAt(point))
                {
                    return false;
                }
            }
            return true;
        }

        int NearestWalkable(int cell)
        {
            int cx = cell % m_Width;
            int cz = cell / m_Width;
            if (IsWalkable(cx, cz))
            {
                return cell;
            }
            for (int radius = 1; radius <= 12; radius++)
            {
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius || !IsWalkable(cx + dx, cz + dz))
                        {
                            continue;
                        }
                        float d = dx * dx + dz * dz;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = cx + dx + (cz + dz) * m_Width;
                        }
                    }
                }
                if (best >= 0)
                {
                    return best;
                }
            }
            return -1;
        }

        bool IsWalkable(int x, int z)
        {
            return x >= 0 && z >= 0 && x < m_Width && z < m_Width && m_Walkable[x + z * m_Width];
        }

        bool IsWalkableAt(Vector3 point)
        {
            return TryGetCell(point, out int cell) && m_Walkable[cell];
        }

        bool TryGetCell(Vector3 point, out int cell)
        {
            int x = Mathf.FloorToInt((point.x + m_HalfSize) / m_CellSize);
            int z = Mathf.FloorToInt((point.z + m_HalfSize) / m_CellSize);
            cell = x + z * m_Width;
            return x >= 0 && z >= 0 && x < m_Width && z < m_Width;
        }

        int CellIndex(Vector3 point)
        {
            return TryGetCell(point, out int cell) ? cell : -1;
        }

        Vector3 CellCenter(int x, int z)
        {
            return new Vector3(-m_HalfSize + (x + 0.5f) * m_CellSize, 0f, -m_HalfSize + (z + 0.5f) * m_CellSize);
        }

        Vector3 CellPoint(int cell)
        {
            Vector3 center = CellCenter(cell % m_Width, cell / m_Width);
            center.y = m_GroundHeight[cell];
            return center;
        }

        void OnDrawGizmosSelected()
        {
            if (!m_Built)
            {
                return;
            }
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            var size = new Vector3(m_CellSize, 0.05f, m_CellSize);
            for (int i = 0; i < m_Walkable.Length; i++)
            {
                if (!m_Walkable[i] && m_GroundHeight[i] != 0f)
                {
                    Gizmos.DrawCube(CellPoint(i), size);
                }
            }
        }

        // Binary min-heap of cell indices ordered by priority.
        class MinHeap
        {
            readonly List<(int cell, float priority)> m_Items = new List<(int, float)>();

            public int Count => m_Items.Count;

            public void Clear()
            {
                m_Items.Clear();
            }

            public void Push(int cell, float priority)
            {
                m_Items.Add((cell, priority));
                int i = m_Items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (m_Items[parent].priority <= m_Items[i].priority)
                    {
                        break;
                    }
                    (m_Items[parent], m_Items[i]) = (m_Items[i], m_Items[parent]);
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = m_Items[0].cell;
                int last = m_Items.Count - 1;
                m_Items[0] = m_Items[last];
                m_Items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < m_Items.Count && m_Items[left].priority < m_Items[smallest].priority)
                    {
                        smallest = left;
                    }
                    if (right < m_Items.Count && m_Items[right].priority < m_Items[smallest].priority)
                    {
                        smallest = right;
                    }
                    if (smallest == i)
                    {
                        break;
                    }
                    (m_Items[smallest], m_Items[i]) = (m_Items[i], m_Items[smallest]);
                    i = smallest;
                }
                return top;
            }
        }
    }
}
