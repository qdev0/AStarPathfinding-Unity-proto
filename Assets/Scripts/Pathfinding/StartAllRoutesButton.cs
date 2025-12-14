using UnityEngine;

public sealed class StartAllRoutes : MonoBehaviour
{
    public KeyCode key = KeyCode.O;

    public void StartAll()
    {
        var routes = FindObjectsByType<UnitRoute>(FindObjectsSortMode.None);
        for (int i = 0; i < routes.Length; i++)
            routes[i].StartRoute();
    }

    private void Update()
    {
        if (Input.GetKeyDown(key))
            StartAll();
    }
}
