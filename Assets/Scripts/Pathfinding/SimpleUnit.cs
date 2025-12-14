using System.Collections.Generic;
using UnityEngine;

public sealed class SimpleUnit : MonoBehaviour
{
    public NavAgentType agentType = NavAgentType.Walker;
    public NavWorld navWorld;
    public Transform target;

    public float speed = 3.5f;
    public float arriveDistance = 0.25f;

    private readonly List<Vector3> _path = new List<Vector3>(256);
    private int _pathIndex;
    private bool _hasPath;

    public System.Action<SimpleUnit> OnArrived;
    public bool IsMoving => _hasPath;
    private void Start()
    {
        if (navWorld == null) navWorld = FindFirstObjectByType<NavWorld>();
        _hasPath = false;
    }

    // Button should call this -> it ENQUEUES (no heavy work here)
    public void RequestPath()
    {
        if (navWorld == null || target == null) return;
        navWorld.EnqueuePathRequest(this, target.position);
    }

    // NavWorld calls this when a path is computed
    public void ApplyPath(List<Vector3> newPath)
    {
        _path.Clear();
        _path.AddRange(newPath);
        _pathIndex = 0;
        _hasPath = _path.Count > 0;
    }

    private void Update()
    {
        if (!_hasPath) return;
        FollowPath();
    }

    private void FollowPath()
    {
        while (_pathIndex < _path.Count && Vector3.Distance(transform.position, _path[_pathIndex]) <= arriveDistance)
            _pathIndex++;

        if (_pathIndex >= _path.Count)
        {
            _hasPath = false;
            OnArrived?.Invoke(this);
            return;
        }


        Vector3 p = _path[_pathIndex];
        Vector3 to = p - transform.position;
        float d = to.magnitude;
        if (d < 0.0001f) return;

        Vector3 dir = to / d;
        transform.position += dir * (speed * Time.deltaTime);

        if (dir.sqrMagnitude > 0.0001f)
            transform.forward = Vector3.Slerp(transform.forward, dir, 10f * Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        if (!DebugDraw.UnitPathLines) return;
        if (_path.Count < 2) return;

        Gizmos.color = Color.green;
        for (int i = 0; i + 1 < _path.Count; i++)
            Gizmos.DrawLine(_path[i], _path[i + 1]);
    }
}
