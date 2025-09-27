/**
 * @file Docs_LevelGen.cs
 * @brief Documentation entry for the Level Generation subsystem.
 *
 * @defgroup level_gen Level Generation
 * @ingroup systems
 * @brief Procedural creation of grid-based tracks, with serializable maps, BFS carve/backtrack,
 *        spacing rules, and straight-segment checkpoint stamping.
 *
 * @details
 * The Level Generation subsystem comprises:
 * - ::SeedFactory — thread-safe seed source to initialize System.Random without collisions.
 * - ::Coordinates — small value type with vector-like ops, equality, and serialization.
 * - ::LevelMap — serializable grid map (name, size, loop flag, endpoints, tiles) with Unity-friendly
 *                flatten/unflatten for the 2D tile array.
 * - ::LevelGenerator — static builder that samples candidate targets, floods (BFS), backtracks to carve
 *                      roads, and applies spacer rules to avoid cramped layouts; can finish circuits.
 * - ::LevelCheckPointMaker — post-process that places checkpoints (-2) along long straight segments.
 *
 * Tile codes:
 * - -2: checkpoint
 * - -1: spacer (keep-out)
 * -  0: empty/grass
 * -  1: road
 * -  2+: BFS placeholders during flooding/backtrack
 *
 * Contents:
 * - see level_gen_overview
 * - see level_gen_inspector
 * - see level_gen_map
 * - see level_gen_generation
 * - see level_gen_checkpoints
 * - see level_gen_api
 * - see level_gen_integration
 * - see level_gen_performance
 * - see level_gen_troubleshooting
 * - see level_gen_versions
 *
 * ----------------------------------------------------------------------
 * @section level_gen_overview Overview
 *
 * Responsibilities:
 * - Produce point-to-point or circuit tracks on a rectangular grid.
 * - Enforce local spacing via spacer tiles to reduce overlapping/merging roads.
 * - Serialize/deserialize maps safely for Unity (2D tiles <-> flat int[]).
 * - Optionally stamp checkpoints on sufficiently long straights.
 *
 * Threading:
 * - Unity main thread (typical use). ::SeedFactory::Next is thread-safe.
 *
 * Invariants:
 * - StartPoint is within bounds; for circuits, FinishPoint == StartPoint.
 * - Carved paths are contiguous 4-neighborhood roads (tile == 1).
 * - Spacer tiles (-1) are only placed adjacent to multi-branch road nodes.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_inspector Data Types (Runtime/Serializable)
 *
 * - ::Coordinates (struct): X,Y; operators (+,-,* scalar), equality, GetHashCode(), ToString().
 * - ::LevelMap (class):
 *   * Name (string)
 *   * Width, Height (int)
 *   * Circular (bool)
 *   * StartPoint, FinishPoint (Coordinates)
 *   * Tiles (int[,]) — transient in Unity; stored as tilesFlat (int[]) for serialization
 *   * Unity: OnBeforeSerialize flattens, OnAfterDeserialize unflattens
 *
 * ----------------------------------------------------------------------
 * @section level_gen_map LevelMap & Tiles
 *
 * Coordinate system:
 * - Indexing is X-major: Tiles[x, y].
 *
 * Helpers (Array2DExtensions):
 * - ref T At<T>(T[,], Coordinates): by-ref element access.
 * - bool InBounds<T>(T[,], int x, int y): bounds check.
 * - T[,] Copy<T>(T[,]): deep copy.
 * - string Print(int[,]): debug dump (CSV-like).
 *
 * Serialization:
 * - FlattenTiles(): writes Tiles[x,y] -> tilesFlat[y * Width + x]
 * - UnflattenTiles(): reconstructs 2D grid from tilesFlat
 *
 * ----------------------------------------------------------------------
 * @section level_gen_generation Generation Pipeline
 *
 * 1) Initialize:
 *    - Create empty grid (0s). Place StartPoint at center; mark as road (1).
 *    - If Circular: CircuitStarter() reserves space (-1) around start and opens one axis.
 *
 * 2) Iterate (steps times):
 *    - PickTarget(): sample a candidate within +/- stepLenght (sic) of last point,
 *      reject if it violates spacing (adjacent road) or reachability.
 *    - FloodingAlgorithm(): BFS over 0s from last point to candidate; write placeholders (2+).
 *    - BackTrack(): walk decreasing placeholders to carve road (1) and call RoadSpacer() around nodes.
 *    - RemovePlaceholders(): clean up any remaining >1 to 0.
 *
 * 3) Finish:
 *    - If Circular: CircuitFinisher() floods back to StartPoint and backtracks to close the loop.
 *      Set FinishPoint = StartPoint.
 *    - Else: FinishPoint = last successfully carved point.
 *
 * Notes:
 * - Spacing: RoadSpacer() marks -1 around multi-adjacent road tiles to discourage tight clusters.
 * - Connectivity: TargetCheck() validates that a path exists and (for circuits) that the candidate
 *   can still reach StartPoint.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_checkpoints Checkpoint Stamping
 *
 * - After generation, ::LevelCheckPointMaker::GenerateCheckPoints(map, minStraightCountForChackPoint)
 *   walks the road from StartPoint, tracking direction. For each straight whose length >= threshold,
 *   one checkpoint (-2) is placed near the midpoint (never on start/finish).
 * - Complexity: O(N) over visited road tiles (4-neighborhood traversal).
 *
 * ----------------------------------------------------------------------
 * @section level_gen_api Public API Reference
 *
 * SeedFactory:
 * - static int Next(): thread-safe seed incrementer (atomic).
 *
 * LevelGenerator:
 * - static LevelMap GenerateLevel(int width, int height, bool isCircuit,
 *                                 int steps, int stepLenght, int maxAttempts, int seed)
 * - static LevelMap GenerateLevel(int width, int height, bool isCircuit,
 *                                 int steps, int stepLenght, int maxAttempts, System.Random rng)
 *
 * Internal concepts (used by the implementation):
 * - TryStep(): target selection + BFS + carve + cleanup for one iteration.
 * - PickTarget(): random candidate subject to spacing and reachability constraints.
 * - FloodingAlgorithm(): writes placeholders to mark BFS distance frontier.
 * - BackTrack(): converts placeholder path to 1s and applies RoadSpacer().
 * - CircuitStarter()/CircuitFinisher(): open/close near StartPoint for circuits.
 *
 * Checkpoint Maker:
 * - static void LevelCheckPointMaker.GenerateCheckPoints(LevelMap, int minStraightCountForChackPoint = 3)
 *
 * ----------------------------------------------------------------------
 * @section level_gen_integration Integration Notes
 *
 * - ::RaceTrackPlacer consumes LevelMap to instantiate prefabs and create ordered ::CheckPointListener
 *   components; also chooses the Start/Finish transform for the car spawn.
 * - ::TrackManager subscribes to those listeners, manages race flow, timers, and respawn.
 * - ::GameDataManager stores LevelMap within GameData (LevelData list) and persists best times.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_performance Performance and GC
 *
 * - BFS + backtrack are linear in the explored area; worst-case ~O(Width*Height).
 * - Larger grids and big step counts increase work; tune for target platforms.
 * - Debug printing (Print()) is expensive; disable in production builds.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_troubleshooting Troubleshooting
 *
 * - Circuit fails to close:
 *   * Increase steps or maxAttempts; ensure CircuitStarter had space to open an axis.
 * - Roads overlap / feel cramped:
 *   * Adjust step parameters or spacer logic; reduce sharp turns by lowering stepLenght.
 * - No checkpoints:
 *   * Ensure roads exist (1s) and minStraightCountForChackPoint isn’t too high.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_versions Version History
 *
 * - v1.2: Straight-segment strating and midpoint placement tweaks.
 * - v1.1: Circuit starter/finisher refinement; spacer rule improvements.
 * - v1.0: BFS/backtrack generator with serializable LevelMap.
 */