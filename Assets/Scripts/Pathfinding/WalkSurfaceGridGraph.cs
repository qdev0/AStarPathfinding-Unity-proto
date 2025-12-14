using System;
using UnityEngine;

[Serializable]
public sealed class WalkSurfaceGridGraph : INavGraph
{
    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(30, 5, 30));
    public float cellSize = 1f;

    [Header("Walker shape")]
    public float agentRadius = 0.4f;
    public float agentHeight = 2.0f;

    [Header("Walk rules")]
    public float maxSlopeDegrees = 45f;
    public float maxStepHeight = 0.5f;

    [Header("Physics")]
    public LayerMask groundMask;
    public LayerMask obstacleMask;

    private int _sx, _sz;
    private Vector3 _origin;
    private bool[] _walkable;
    private Vector3[] _pos; // node positions at capsule center
    private Vector3[] _groundNormal;

    public int NodeCount => _sx * _sz;

    public void Build()
    {
        _sx = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / cellSize));
        _sz = Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / cellSize));
        _origin = bounds.min;

        _walkable = new bool[NodeCount];
        _pos = new Vector3[NodeCount];
        _groundNormal = new Vector3[NodeCount];

        float castTop = bounds.max.y + 2f;
        float castDist = bounds.size.y + 10f;

        for (int z = 0; z < _sz; z++)
            for (int x = 0; x < _sx; x++)
            {
                int idx = ToIndex(x, z);
                Vector3 xz = new Vector3(
                    _origin.x + (x + 0.5f) * cellSize,
                    castTop,
                    _origin.z + (z + 0.5f) * cellSize
                );

                // Find ground under this cell
                if (!Physics.Raycast(xz, Vector3.down, out RaycastHit hit, castDist, groundMask, QueryTriggerInteraction.Ignore))
                {
                    _walkable[idx] = false;
                    continue;
                }

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > maxSlopeDegrees)
                {
                    _walkable[idx] = false;
                    continue;
                }

                // Place node at capsule center height above ground
                Vector3 center = hit.point + Vector3.up * (agentHeight * 0.5f);
                _pos[idx] = center;
                _groundNormal[idx] = hit.normal;

                // Clearance test: capsule overlap against obstacles
                // Physics.CheckCapsule expects two sphere centers (bottom, top). :contentReference[oaicite:0]{index=0}
                float half = Mathf.Max(agentRadius, agentHeight * 0.5f);
                Vector3 bottom = center + Vector3.down * (half - agentRadius);
                Vector3 top = center + Vector3.up * (half - agentRadius);

                bool blocked = Physics.CheckCapsule(bottom, top, agentRadius, obstacleMask, QueryTriggerInteraction.Ignore);
                _walkable[idx] = !blocked;
            }
    }

    public int FindNearestNode(Vector3 worldPos)
    {
        if (_walkable == null || _walkable.Length == 0) return -1;

        int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - _origin.x) / cellSize), 0, _sx - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - _origin.z) / cellSize), 0, _sz - 1);

        for (int r = 0; r <= 3; r++)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (!InBounds(nx, nz)) continue;
                    int idx = ToIndex(nx, nz);
                    if (_walkable[idx]) return idx;
                }
        }

        return -1;
    }

    public Vector3 GetNodePosition(int node) => _pos[node];

    // 8-connected neighbors on surface grid
    public int GetNeighborCount(int node) => 8;

public int GetNeighbor(int node, int neighborIndex)
{
    FromIndex(node, out int x, out int z);

    (int dx, int dz) = neighborIndex switch
    {
        0 => (0, 1),
        1 => (0, -1),
        2 => (1, 0),
        3 => (-1, 0),
        4 => (1, 1),
        5 => (-1, 1),
        6 => (1, -1),
        7 => (-1, -1),
        _ => (0, 0)
    };

    int nx = x + dx;
    int nz = z + dz;
    if (!InBounds(nx, nz)) return -1;

    int ni = ToIndex(nx, nz);
    if (!_walkable[ni]) return -1;

    // --- NO CORNER CUTTING ---
    // If diagonal, both side cells must also be walkable.
    if (dx != 0 && dz != 0)
    {
        int sideA = ToIndex(x + dx, z);     // step in x only
        int sideB = ToIndex(x, z + dz);     // step in z only
        if (!_walkable[sideA] || !_walkable[sideB]) return -1;
    }

    // Step height rule
    float dy = Mathf.Abs(_pos[ni].y - _pos[node].y);
    if (dy > maxStepHeight) return -1;

    return ni;
}


    public float GetCost(int from, int to)
    {
        // Slightly penalize diagonals naturally via distance
        return Vector3.Distance(_pos[from], _pos[to]);
    }

    private bool InBounds(int x, int z) => x >= 0 && x < _sx && z >= 0 && z < _sz;

    private int ToIndex(int x, int z) => x + _sx * z;

    private void FromIndex(int idx, out int x, out int z)
    {
        x = idx % _sx;
        z = idx / _sx;
    }

    public bool IsWalkable(int node) => _walkable != null && node >= 0 && node < _walkable.Length && _walkable[node];

}
