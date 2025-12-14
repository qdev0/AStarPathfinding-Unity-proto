using System.Collections.Generic;
using UnityEngine;

public sealed class NavWorld : MonoBehaviour
{
    [Header("Graphs")]
    public WalkSurfaceGridGraph walkGraph = new WalkSurfaceGridGraph();
    public FlyGridGraph flyGraph = new FlyGridGraph();

    [Header("Path Smoothing")]
    public bool enableSmoothing = true;
    public float walkSampleStep = 0.5f;

    [Header("Debug Draw")]
    public bool drawWalkNodes = true;
    public bool drawFlyNodes = true;
    public bool drawBlocked = true;
    public float gizmoNodeSize = 0.2f;
    public int maxGizmoNodes = 5000; // tune

    private AStar _aStar;
    private bool _built;
    private readonly List<int> _tmpNodePath = new List<int>(256);

    private struct PathRequest
    {
        public SimpleUnit unit;
        public NavAgentType type;
        public Vector3 goal;
    }


    [Header("Queue Processing")]
    public bool useQueue = true;

    [Tooltip("Max milliseconds spent computing paths per frame.")]
    public float msBudgetPerFrame = 2.0f;

    [Tooltip("Hard cap of requests per frame (applies even if time budget remains).")]
    public int maxRequestsPerFrame = 10;

    private readonly Queue<PathRequest> _queue = new Queue<PathRequest>(2048);
    private readonly Stack<List<Vector3>> _pathListPool = new Stack<List<Vector3>>(256);

    public int PendingRequests => _queue.Count;
    public bool IsBuilt => _built;

    [ContextMenu("Rebuild All Graphs")]
    public void RebuildAll()
    {
        walkGraph.Build();
        flyGraph.Build();
        _built = true;

        if (_aStar == null)
            _aStar = new AStar(2048);
    }

    public bool TryFindPath(NavAgentType type, Vector3 start, Vector3 goal, List<Vector3> outWorldPath)
    {
        outWorldPath.Clear();
        if (!_built) return false;

        if (_aStar == null)
            _aStar = new AStar(2048);

        INavGraph graph = (type == NavAgentType.Walker) ? (INavGraph)walkGraph : flyGraph;

        int s = graph.FindNearestNode(start);
        int g = graph.FindNearestNode(goal);
        if (s < 0 || g < 0) return false;

        if (!_aStar.FindPath(graph, s, g, _tmpNodePath)) return false;

        for (int i = 0; i < _tmpNodePath.Count; i++)
            outWorldPath.Add(graph.GetNodePosition(_tmpNodePath[i]));

        if (enableSmoothing)
            SmoothInPlace(type, outWorldPath);

        return true;
    }

    private void SmoothInPlace(NavAgentType type, List<Vector3> path)
    {
        if (path.Count <= 2) return;

        // Result buffer
        List<Vector3> smoothed = new List<Vector3>(path.Count);
        smoothed.Add(path[0]);

        int i = 0;
        while (i < path.Count - 1)
        {
            int best = i + 1;

            // Try to jump as far as possible
            for (int j = path.Count - 1; j > i + 1; j--)
            {
                bool ok = (type == NavAgentType.Flyer)
                    ? HasLineOfSightFly(path[i], path[j])
                    : HasLineOfSightWalk(path[i], path[j]);

                if (ok)
                {
                    best = j;
                    break;
                }
            }

            smoothed.Add(path[best]);
            i = best;
        }

        path.Clear();
        path.AddRange(smoothed);
    }
    public void ClearQueue()
    {
        _queue.Clear();
    }

    public void EnqueuePathRequest(SimpleUnit unit, Vector3 goalPosSnapshot)
    {
        if (unit == null) return;
        if (!_built) return;

        _queue.Enqueue(new PathRequest
        {
            unit = unit,
            type = unit.agentType,
            goal = goalPosSnapshot
        });
    }


    private void Update()
    {
        if (!useQueue) return;
        if (!_built) return;
        if (_queue.Count == 0) return;

        float startTime = Time.realtimeSinceStartup;
        int processed = 0;

        while (_queue.Count > 0 && processed < maxRequestsPerFrame)
        {
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            if (elapsedMs >= msBudgetPerFrame) break;

            var req = _queue.Dequeue();
            if (req.unit == null) continue;

            var tmp = GetPooledList();

            // compute start at execution time
            Vector3 start = req.unit.transform.position;

            bool ok = TryFindPath(req.type, start, req.goal, tmp);
            if (ok) req.unit.ApplyPath(tmp);
            else { tmp.Clear(); req.unit.ApplyPath(tmp); }

            ReturnPooledList(tmp);
            processed++;
        }
    }

    // Flyer shortcut test: sphere-cast through the world.
    private bool HasLineOfSightFly(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float dist = d.magnitude;
        if (dist <= 0.0001f) return true;

        Vector3 dir = d / dist;

        // Slightly reduce distance to avoid “touching” the destination collider at the end
        float castDist = Mathf.Max(0f, dist - 0.02f);

        // If anything blocks a sphere of agentRadius, no shortcut.
        return !Physics.SphereCast(a, flyGraph.agentRadius, dir, out _, castDist, flyGraph.obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // Walker shortcut test:
    // 1) CapsuleCast for obstacle clearance
    // 2) Sample along the segment to ensure ground exists + slope ok + step height ok
    private bool HasLineOfSightWalk(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float dist = d.magnitude;
        if (dist <= 0.0001f) return true;

        Vector3 dir = d / dist;

        // --- (1) Capsule clearance ---
        // Build capsule endpoints from "center" point a (and we’ll cast along dir)
        float radius = walkGraph.agentRadius;
        float height = walkGraph.agentHeight;

        float half = Mathf.Max(radius, height * 0.5f);
        Vector3 aBottom = a + Vector3.down * (half - radius);
        Vector3 aTop = a + Vector3.up * (half - radius);

        float castDist = Mathf.Max(0f, dist - 0.02f);

        if (Physics.CapsuleCast(aBottom, aTop, radius, dir, out _, castDist, walkGraph.obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        // --- (2) Ground validity sampling ---
        // We raycast down at intervals along the shortcut to ensure:
        // - there is ground
        // - slope is within maxSlopeDegrees
        // - height changes don't exceed maxStepHeight between samples
        float step = Mathf.Max(0.2f, walkSampleStep > 0 ? walkSampleStep : walkGraph.cellSize * 0.5f);

        float castTop = walkGraph.bounds.max.y + 2f;
        float castDistDown = walkGraph.bounds.size.y + 10f;

        float prevGroundY = float.NaN;

        for (float t = 0f; t <= dist; t += step)
        {
            Vector3 p = a + dir * t;
            Vector3 rayStart = new Vector3(p.x, castTop, p.z);

            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, castDistDown, walkGraph.groundMask, QueryTriggerInteraction.Ignore))
                return false;

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > walkGraph.maxSlopeDegrees)
                return false;

            if (!float.IsNaN(prevGroundY))
            {
                float dy = Mathf.Abs(hit.point.y - prevGroundY);
                if (dy > walkGraph.maxStepHeight)
                    return false;
            }

            prevGroundY = hit.point.y;
        }

        return true;
    }

    private List<Vector3> GetPooledList()
    {
        if (_pathListPool.Count > 0)
        {
            var l = _pathListPool.Pop();
            l.Clear();
            return l;
        }
        return new List<Vector3>(128);
    }

    private void ReturnPooledList(List<Vector3> l)
    {
        if (l == null) return;
        l.Clear();
        _pathListPool.Push(l);
    }

    private void OnDrawGizmos()
    {
        if (!DebugDraw.NavWorldGizmos) return;
        Gizmos.matrix = Matrix4x4.identity;

        // Bounds
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(walkGraph.bounds.center, walkGraph.bounds.size);
        Gizmos.DrawWireCube(flyGraph.bounds.center, flyGraph.bounds.size);

        // If graphs not built yet, nothing else to draw
        if (walkGraph.NodeCount <= 0 || flyGraph.NodeCount <= 0) return;

        // Walk nodes
        if (drawWalkNodes)
        {
            int drawn = 0;

            for (int i = 0; i < walkGraph.NodeCount; i++)
            {
                if (drawn >= maxGizmoNodes) break;

                bool ok = walkGraph.IsWalkable(i);
                if (!ok && !drawBlocked) continue;

                Gizmos.color = ok ? Color.cyan : new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawSphere(walkGraph.GetNodePosition(i), gizmoNodeSize);
                drawn++;

            }
        }

        // Fly nodes (slice)
        if (drawFlyNodes)
        {
            // draw only nodes near the center Y for readability
            float midY = flyGraph.bounds.center.y;
            float halfSlice = flyGraph.cellSize * 0.6f;

            for (int i = 0; i < flyGraph.NodeCount; i++)
            {
                Vector3 p = flyGraph.GetNodePosition(i);
                if (Mathf.Abs(p.y - midY) > halfSlice) continue;

                bool ok = flyGraph.IsWalkable(i);
                if (!ok && !drawBlocked) continue;

                Gizmos.color = ok ? Color.yellow : new Color(1f, 0f, 0f, 0.25f);
                Gizmos.DrawSphere(p, gizmoNodeSize);
            }
        }
    }

}
