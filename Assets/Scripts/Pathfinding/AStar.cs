using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AStar
{
    // min heap for open set(node, fscore)
    private sealed class MinHeap
    {
        private (int node, float f)[] _heap;
        private int _size;
        public MinHeap(int capacity) => _heap = new (int, float)[Mathf.Max(16, capacity)];
        public void Clear() => _size = 0;
        public int Count => _size;

        public void Push(int node, float f)
        {
            if (_size >= _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
            int i = _size++;
            _heap[i] = (node, f);
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heap[p].f <= _heap[i].f) break;
                (_heap[p], _heap[i]) = (_heap[i], _heap[p]);
                i = p;
            }
        }

        public int Pop()
        {
            int result = _heap[0].node;
            _heap[0] = _heap[--_size];
            int i = 0;

            while (true)
            {
                int l = i * 2 + 1;
                if (l >= _size) break;
                int r = l + 1;
                int c = (r < _size && _heap[r].f < _heap[l].f) ? r : l;
                if (_heap[i].f <= _heap[c].f) break;
                (_heap[i], _heap[c]) = (_heap[c], _heap[i]);
                i = c;
            }

            return result;
        }
    }

    private readonly MinHeap _open;
    private float[] _g;
    private int[] _cameFrom;
    private byte[] _state; // 0=unseen, 1=open, 2=closed

    public AStar(int initialCapacity = 1024)
    {
        _open = new MinHeap(initialCapacity);
        _g = new float[initialCapacity];
        _cameFrom = new int[initialCapacity];
        _state = new byte[initialCapacity];
    }

    public bool FindPath(INavGraph graph, int start, int goal, List<int> outNodePath)
    {
        if (start < 0 || goal < 0) return false;
        EnsureCapacity(graph.NodeCount);

        // Reset state
        Array.Fill(_state, (byte)0, 0, graph.NodeCount);
        for (int i = 0; i < graph.NodeCount; i++)
        {
            _g[i] = float.PositiveInfinity;
            _cameFrom[i] = -1;
        }

        _open.Clear();
        _g[start] = 0f;
        _open.Push(start, Heuristic(graph, start, goal));
        _state[start] = 1; // open

        while (_open.Count > 0)
        {
            int current = _open.Pop();
            if (_state[current] == 2) continue; // skip stale entry
            _state[current] = 2; // closed

            if (current == goal)
            {
                Reconstruct(goal, outNodePath);
                return true;
            }

            int nCount = graph.GetNeighborCount(current);
            for (int i = 0; i < nCount; i++)
            {
                int nb = graph.GetNeighbor(current, i);
                if (nb < 0) continue;
                if (_state[nb] == 2) continue;

                float tentative = _g[current] + graph.GetCost(current, nb);
                if (tentative < _g[nb])
                {
                    _g[nb] = tentative;
                    _cameFrom[nb] = current;
                    float f = tentative + Heuristic(graph, nb, goal);
                    _open.Push(nb, f);
                    _state[nb] = 1;
                }
            }
        }
        return false;
    }

    private void Reconstruct(int goal, List<int> outNodePath)
    {
        outNodePath.Clear();
        for(int n = goal;n!= -1; n = _cameFrom[n]) outNodePath.Add(n);
        outNodePath.Reverse();
    }

    private float Heuristic(INavGraph graph, int start, int goal) => Vector3.Distance(graph.GetNodePosition(start), graph.GetNodePosition(goal));


    private void EnsureCapacity(int n)
    {
        if (_g.Length >= n) return;
        Array.Resize(ref _g, n);
        Array.Resize(ref _cameFrom, n);
        Array.Resize(ref _state, n);
    }
}
