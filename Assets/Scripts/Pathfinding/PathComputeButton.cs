using UnityEngine;
using UnityEngine.UI;

public sealed class PathComputeButton : MonoBehaviour
{
    public NavWorld navWorld;

    [Header("Behavior")]
    public bool rebuildGraphsBeforePath = false; // IMPORTANT: leave false if rebuild is huge
    public bool clearQueueBeforeEnqueue = true;

    public GameObject computeButton;
    void Awake()
    {
        computeButton.SetActive(true);
    }
    public void Compute()
    {
        if (navWorld == null)
            navWorld = FindFirstObjectByType<NavWorld>();

        if (navWorld == null) return;

        if (rebuildGraphsBeforePath)
            navWorld.RebuildAll();

        if (clearQueueBeforeEnqueue)
            navWorld.ClearQueue();

        var units = FindObjectsByType<SimpleUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i];
            if (u == null || u.target == null) continue;
            u.navWorld = navWorld;         // ensure reference
            u.RequestPath();               // enqueues
        }

        Debug.Log($"Enqueued {units.Length} units. Pending: {navWorld.PendingRequests}");
    }

    public KeyCode hotkey = KeyCode.P;
    private void Update()
    {
        if (Input.GetKeyDown(hotkey))
            Compute();
    }
}
