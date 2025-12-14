using UnityEngine;

public sealed class DebugHotkeys : MonoBehaviour
{
    public KeyCode toggleAll = KeyCode.F1;
    public KeyCode toggleNav = KeyCode.F2;
    public KeyCode toggleUnitLines = KeyCode.F3;

    // Optional: also toggle NavWorld node categories if you want
    public KeyCode toggleWalkNodes = KeyCode.F4;
    public KeyCode toggleFlyNodes  = KeyCode.F5;

    private NavWorld _nav;

    private void Awake()
    {
        _nav = FindFirstObjectByType<NavWorld>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleAll))
        {
            bool newState = !(DebugDraw.NavWorldGizmos || DebugDraw.UnitPathLines);
            DebugDraw.NavWorldGizmos = newState;
            DebugDraw.UnitPathLines  = newState;
        }

        if (Input.GetKeyDown(toggleNav))
            DebugDraw.NavWorldGizmos = !DebugDraw.NavWorldGizmos;

        if (Input.GetKeyDown(toggleUnitLines))
            DebugDraw.UnitPathLines = !DebugDraw.UnitPathLines;

        if (_nav != null)
        {
            if (Input.GetKeyDown(toggleWalkNodes))
                _nav.drawWalkNodes = !_nav.drawWalkNodes;

            if (Input.GetKeyDown(toggleFlyNodes))
                _nav.drawFlyNodes = !_nav.drawFlyNodes;
        }
    }
}
