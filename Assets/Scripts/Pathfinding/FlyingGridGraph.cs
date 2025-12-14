using System;
using UnityEngine;

[Serializable]
public sealed class FlyGridGraph : INavGraph
{
    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(30, 10, 30));
    public float cellSize = 1f;
    public float agentRadius = 0.4f;
    public LayerMask obstacleMask;

    private int _sx, _sy, _sz;
    private Vector3 _origin;
    private bool[] _walkable; // here "free" means flyable

    public int NodeCount => _sx * _sy * _sz;

    public void Build()
    {
        _sx = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / cellSize));
        _sy = Mathf.Max(1, Mathf.FloorToInt(bounds.size.y / cellSize));
        _sz = Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / cellSize));

        _origin = bounds.min;
        _walkable = new bool[NodeCount];

        for (int z = 0; z < _sz; z++)
            for (int y = 0; y < _sy; y++)
                for (int x = 0; x < _sx; x++)
                {
                    int idx = ToIndex(x, y, z);
                    Vector3 p = CellCenter(x, y, z);

                    // "Free space" test: sphere overlap against obstacles
                    bool blocked = Physics.CheckSphere(p, agentRadius, obstacleMask, QueryTriggerInteraction.Ignore);
                    _walkable[idx] = !blocked;
                }
    }

    public int FindNearestNode(Vector3 worldPos)
    {
        if (_walkable == null || _walkable.Length == 0) return -1;

        int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - _origin.x) / cellSize), 0, _sx - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((worldPos.y - _origin.y) / cellSize), 0, _sy - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - _origin.z) / cellSize), 0, _sz - 1);

        // If exact cell blocked, do a small expanding search (cheap, not perfect)
        for (int r = 0; r <= 3; r++)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = x + dx, ny = y + dy, nz = z + dz;
                        if (!InBounds(nx, ny, nz)) continue;
                        int idx = ToIndex(nx, ny, nz);
                        if (_walkable[idx]) return idx;
                    }
        }

        return -1;
    }

    public Vector3 GetNodePosition(int node)
    {
        FromIndex(node, out int x, out int y, out int z);
        return CellCenter(x, y, z);
    }

    // 26-connected neighbors in 3D (faces+edges+corners)
    public int GetNeighborCount(int node) => 26;

    public int GetNeighbor(int node, int neighborIndex)
    {
        FromIndex(node, out int x, out int y, out int z);

        // Map 0..25 into dx,dy,dz excluding (0,0,0)
        int n = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    if (n == neighborIndex)
                    {
                        int nx = x + dx, ny = y + dy, nz = z + dz;
                        if (!InBounds(nx, ny, nz)) return -1;
                        int ni = ToIndex(nx, ny, nz);
                        return _walkable[ni] ? ni : -1;
                    }
                    n++;
                }

        return -1;
    }

    public float GetCost(int from, int to)
        => Vector3.Distance(GetNodePosition(from), GetNodePosition(to));

    private bool InBounds(int x, int y, int z)
        => x >= 0 && x < _sx && y >= 0 && y < _sy && z >= 0 && z < _sz;

    private int ToIndex(int x, int y, int z) => x + _sx * (y + _sy * z);

    private void FromIndex(int idx, out int x, out int y, out int z)
    {
        x = idx % _sx;
        int t = idx / _sx;
        y = t % _sy;
        z = t / _sy;
    }

    private Vector3 CellCenter(int x, int y, int z)
        => _origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, (z + 0.5f) * cellSize);
    public bool IsWalkable(int node) => _walkable != null && node >= 0 && node < _walkable.Length && _walkable[node];

}
