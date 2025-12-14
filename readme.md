# Unity 6 URP Pathfinding Project (Walking + Flying)

This project is an implementation of custom pathfinding in Unity 6 (URP), supporting:

- **Walking units** constrained to a surface (2D grid sampled from ground)
- **Flying units** moving through 3D space (voxel grid)
- **A\*** pathfinding (single reusable implementation for both graphs)
- **Path smoothing** (line-of-sight shortcutting)
- **Queued path computation** with a per-frame time budget (prevents long freezes with many units)
- **Debug visualization** toggles (gizmos + unit path lines)
- **Multi-target routing** support (optional extension)

This is structured like a production system: **graphs describe traversable space**, **A\*** searches the graph, **units only follow paths**, and heavy work is **queued**.

## Showcase

### YouTube Video
[![Watch the demo for version](https://img.youtube.com/vi/aa8MzW0ylYg/hqdefault.jpg)](https://www.youtube.com/watch?v=aa8MzW0ylYg)

---

## Table of Contents

- [Scene Setup](#scene-setup)
- [Core Concepts](#core-concepts)
  - [Graphs](#graphs)
  - [A*](#a)
  - [Path Smoothing](#path-smoothing)
  - [Queued Computation](#queued-computation)
- [Scripts](#scripts)
- [Debug Controls](#debug-controls)
- [Performance Notes](#performance-notes)
- [Common Problems](#common-problems)



## Scene Setup

### Layers
Create two layers:
- `Ground`
- `Obstacle`

Assign:
- Floor/terrain meshes → `Ground`
- Walls/blocks/props that should block movement → `Obstacle`

Ensure all obstacles have **Colliders**.

### NavWorld
Create an empty GameObject `NavWorld` with the `NavWorld` component.

Configure:
- `walkGraph.groundMask` = `Ground`
- `walkGraph.obstacleMask` = `Obstacle`
- `flyGraph.obstacleMask` = `Obstacle`

Bounds must cover where units/targets exist.

### Units
Create prefabs or scene objects:
- Walker: capsule (or any mesh), `SimpleUnit` set to `Walker`
- Flyer: sphere (or any mesh), `SimpleUnit` set to `Flyer`

Assign a `target` Transform (or use your own target selection logic).

### Build Graphs
Graphs are built by calling:
- `NavWorld.RebuildAll()` (context menu, button, or your own trigger)

## Core Concepts

### Graphs

#### Walking Graph (surface grid)
`WalkSurfaceGridGraph` builds nodes by:
1. Scanning a 2D grid inside `bounds` (XZ)
2. **Raycasting down** to find ground height + normal
3. Rejecting nodes if:
   - no ground found
   - slope exceeds `maxSlopeDegrees`
   - a capsule at that position overlaps obstacles (`CheckCapsule`)
4. Neighbor connections are typically **8-connected** (N/S/E/W + diagonals)
5. Optional rule: **disable corner cutting** (diagonal requires both side cells open)

Walking constraints:
- slope limit
- step height limit
- capsule clearance

#### Flying Graph (3D voxel grid)
`FlyGridGraph` builds nodes by:
1. Scanning a 3D grid inside `bounds` (XYZ)
2. Marking a voxel as blocked if a sphere at its center overlaps obstacles (`CheckSphere`)
3. Neighbor connections typically **26-connected** (or fewer for performance)

Flying constraints:
- sphere clearance
- obstacle avoidance

### A*

`AStar` is implemented once and works on any `INavGraph`:
- Open set = min-heap (priority queue)
- g-cost = traveled cost
- h-cost = Euclidean heuristic
- Outputs a list of node indices, converted into world positions by `NavWorld`

A\* runs per request unless you implement goal-based reuse strategies (e.g., flow fields).


### Path Smoothing

After A\* returns a waypoint list, smoothing removes unnecessary intermediate points.

- Flyer smoothing uses `SphereCast` from point A → B
- Walker smoothing uses:
  - `CapsuleCast` for clearance
  - ground sampling along the segment to ensure:
    - ground exists
    - slope acceptable
    - step height changes acceptable

Smoothing makes paths look more natural and reduces “grid movement.”

### Queued Computation

With many units (hundreds/thousands), computing paths in one frame causes freezes.

`NavWorld` implements a request queue:
- Requests are enqueued with `EnqueuePathRequest(unit, goalPos)`
- Each frame, `NavWorld.Update()` processes paths until:
  - time budget `msBudgetPerFrame` is used
  - request cap `maxRequestsPerFrame` is reached

This spreads work across frames and keeps FPS stable.


## Scripts

### `NavTypes.cs`
Defines:
- `NavAgentType` (`Walker`, `Flyer`)
- `INavGraph` interface used by A\*

### `AStar.cs`
Reusable A* implementation with:
- binary heap open set
- no per-frame garbage allocations (uses arrays internally)

### `WalkSurfaceGridGraph.cs`
Builds a ground-constrained grid using physics checks:
- raycasts for ground
- capsule overlap for clearance
- slope + step constraints
- neighbor accessors for A\*

### `FlyGridGraph.cs`
Builds a 3D voxel grid:
- sphere overlap for clearance
- 3D neighbor iteration for A\*

### `NavWorld.cs`
Orchestrates:
- graph building (`RebuildAll`)
- path requests and queue processing
- smoothing
- debug gizmos

Key public methods:
- `RebuildAll()`
- `EnqueuePathRequest(SimpleUnit unit, Vector3 goal)`
- `TryFindPath(...)` (used internally by queue)

### `SimpleUnit.cs`
A minimal path follower:
- receives a computed path via `ApplyPath(List<Vector3>)`
- moves along waypoints until finished
- stops after reaching final point (no auto-replanning)
- optional debug path line drawing

### `PathComputeButton.cs`
Triggers:
- optional rebuild
- finds all `SimpleUnit` instances
- enqueues path requests for them

### `DebugDraw.cs` + `DebugHotkeys.cs`
Global toggles:
- NavWorld gizmos
- unit path lines
Hotkeys can enable/disable at runtime.

## Debug Controls

Typical setup:
- Toggle all debug draw: `F1`
- Toggle NavWorld gizmos: `F2`
- Toggle unit path lines: `F3`
- Toggle walk node draw: `F4`
- Toggle fly node draw: `F5`

Notes:
- Drawing many gizmos is expensive. Prefer:
  - draw only blocked nodes
  - draw only slices
  - draw only when selected
  - disable gizmos in play mode unless debugging

## Performance Notes

### Biggest knobs
- Increase `cellSize` (reduces node count dramatically)
- Reduce flyer neighbor count (26-connected is expensive)
- Use queue budget:
  - `msBudgetPerFrame` ~ 1–5 ms
  - `maxRequestsPerFrame` ~ 5–50 depending on complexity

### Avoid expensive debug drawing
Gizmos can destroy FPS even when units are gone.
Prefer editor-only and “blocked-only” drawing.

### Avoid mass Destroy()
If you spawn and remove thousands of units repeatedly, prefer pooling:
- `gameObject.SetActive(false)`
- reuse later


## Common Problems

### Units ignore obstacles
Causes:
- diagonal corner cutting in walking grid
- movement via `transform.position` bypasses physics

Fixes:
- enforce “no corner cutting” for diagonals
- use `CharacterController.Move()` or cast-before-move

### Units ignore obstacles when switching NavWorld
Cause:
- each NavWorld only represents obstacles inside its bounds
- planning outside bounds yields invalid paths

Fixes:
- use one NavWorld for continuous space
- or add overlap + portals between NavWorlds
- or disallow cross-world legs unless both start + goal are inside the same world bounds

### Massive freeze when starting
Cause:
- graph rebuild (physics checks for every cell) is expensive
Fix:
- build once
- or chunk/time-slice building later



