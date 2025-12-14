using UnityEngine;

public enum NavAgentType
{
    Walker,
    Flyer
}

public interface INavGraph
{
    // returns neares node index to world position. -1 if none found
    int FindNearestNode(Vector3 worldPos);

    // get node world position
    Vector3 GetNodePosition(int node);

    // for A*: iterate neighbors of a node
    int GetNeighborCount(int node);
    int GetNeighbor(int node, int index);

    // cost from node to neighbor
    float GetCost(int from, int to);

    // node count for internal arrays
    int NodeCount{ get;}
}