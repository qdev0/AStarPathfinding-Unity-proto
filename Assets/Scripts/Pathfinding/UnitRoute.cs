using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UnitRoute : MonoBehaviour
{
    [Serializable]
    public class Leg
    {
        public NavWorld navWorld;
        public Transform target;

        [Tooltip("Optional: rebuild this NavWorld before starting this leg.")]
        public bool rebuildBefore = false;
    }

    public SimpleUnit unit;
    public List<Leg> legs = new List<Leg>();

    public bool loop = false;
    public bool destroyOnFinish = false;
    public bool autoStart = false;

    int _legIndex = -1;

    private void Awake()
    {
        if (unit == null) unit = GetComponent<SimpleUnit>();
        if (unit != null) unit.OnArrived += HandleArrived;
    }

    private void Start()
    {
        if (autoStart) StartRoute();
    }

    private void OnDestroy()
    {
        if (unit != null) unit.OnArrived -= HandleArrived;
    }

    public void StartRoute()
    {
        if (unit == null || legs.Count == 0) return;
        _legIndex = -1;
        GoNextLeg();
    }

    private void HandleArrived(SimpleUnit u)
    {
        GoNextLeg();
    }

    private void GoNextLeg()
    {
        if (legs.Count == 0) return;

        _legIndex++;

        if (_legIndex >= legs.Count)
        {
            if (loop)
                _legIndex = 0;
            else
            {
                if (destroyOnFinish) Destroy(gameObject);
                return;
            }
        }

        var leg = legs[_legIndex];
        if (leg == null || leg.navWorld == null || leg.target == null)
        {
            GoNextLeg(); // skip invalid leg
            return;
        }

        if (leg.rebuildBefore)
            leg.navWorld.RebuildAll();

        // enqueue the path on the correct NavWorld
        leg.navWorld.EnqueuePathRequest(unit, leg.target.position);
    }
}
