using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.UIElements;
using System.Threading.Tasks;

#region Helper Structures / Classes

/// <summary>
/// Describes a single placeable track piece: which prefab to spawn and the world-space pose
/// (position and rotation) to apply at placement time.
/// </summary>
[Serializable]
public struct TrackPiece
{
    /// <summary>Prefab to instantiate for this track cell (may be <c>null</c> if no spawn).</summary>
    public GameObject Prefab;

    /// <summary>World-space position to place the prefab at.</summary>
    public Vector3 Position;

    /// <summary>World-space rotation to apply to the prefab.</summary>
    public Quaternion Rotation;

    /// <summary>Tile Size of the given prefab.</summary>
    public int Size;

    /// <summary>
    /// Creates a new <see cref="TrackPiece"/> with explicit prefab and pose.
    /// </summary>
    /// <param name="prefab">Prefab reference (can be <c>null</c> for "no piece").</param>
    /// <param name="position">World-space placement position.</param>
    /// <param name="rotation">World-space placement rotation.</param>
    public TrackPiece(GameObject prefab, Vector3 position, Quaternion rotation, int size = 3)
    {
        Prefab = prefab;
        Position = position;
        Rotation = rotation;
        Size = size;
    }
}

/// <summary>
/// Serializable dictionary mapping a string pattern key to a <see cref="TrackPiece"/> descriptor.
/// Keys are compact pattern encodings (e.g., 3x3 or 5x5 matrices concatenated into a string).
/// </summary>
[Serializable]
public class StringTrackPieceDictionary : SerializableDictionary<string, TrackPiece>
{ }

#endregion Helper Structures / Classes

/// <summary>
/// Procedural track placer: scans a <see cref="LevelMap"/> grid, matches local patterns
/// around each path cell to a legend, and instantiates the appropriate track piece prefabs
/// (including start/finish and checkpoints). Also wires checkpoints to <see cref="TrackManager"/>.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @invariant <see cref="trackPieceLegend"/> keys must be perfect squares (3x3, 5x5, …).
/// @invariant <see cref="LevelMap"/> is present in <see cref="GameDataManager.CurrentLevelMap"/> when placing starts.
/// @thread Unity main thread (scene start and instantiation only).
/// </remarks>
public class RaceTrackPlacer : MonoBehaviour
{
    /// <summary>
    /// Neighbor offsets for 4-way traversal and adjacency tests (right, left, up, down).
    /// </summary>
    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };

    #region Legend

    [Header("Track Piece Prefabs")]
    /// <summary>
    /// Legend mapping of string patterns to <see cref="TrackPiece"/> descriptors.
    /// The string keys are flattened NxN character grids (e.g., '1' for track, 'X' for empty),
    /// optionally containing special markers (e.g., 'C' during matching).
    /// The associated <see cref="TrackPiece"/> carries default rotation for the shape; final world
    /// position is filled at placement time.
    /// </summary>
    [SerializeField]
    private StringTrackPieceDictionary trackPieceLegend = new StringTrackPieceDictionary()
    {
        { "X1X" +
          "X1X" +
          "X1X", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Straight Vertical

        { "XXX" +
          "111" +
          "XXX", new TrackPiece(null, Vector3.zero, Quaternion.Euler(0, 90, 0)) }, // Straight Horizontal

        { "X1X" +
          "X11" +
          "XXX", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Curve Up-Right

        { "X1X" +
          "11X" +
          "XXX", new TrackPiece(null, Vector3.zero, Quaternion.Euler(0, 90, 0)) }, // Curve Up-Left

        { "XXX" +
          "11X" +
          "X1X", new TrackPiece(null, Vector3.zero, Quaternion.Euler(0, 180, 0)) }, // Curve Down-Left

        { "XXX" +
          "X11" +
          "X1X", new TrackPiece(null, Vector3.zero, Quaternion.Euler(0, 270, 0)) }, // Curve Down-Right

        { "XX1XX" +
          "XX1XX" +
          "XX111" +
          "XXXXX" +
          "XXXXX", new TrackPiece(null, Vector3.zero, Quaternion.identity, 5) }, // Long Curve Up-Right

        { "XX1XX" +
          "XX1XX" +
          "111XX" +
          "XXXXX" +
          "XXXXX", new TrackPiece(null, Vector3.zero, Quaternion.identity, 5) }, // Long Curve Up-Left

        { "XXXXX" +
          "XXXXX" +
          "XX111" +
          "XX1XX" +
          "XX1XX", new TrackPiece(null, Vector3.zero, Quaternion.identity, 5) }, // Long Curve Down-Right

        { "XXXXX" +
          "XXXXX" +
          "111XX" +
          "XX1XX" +
          "XX1XX", new TrackPiece(null, Vector3.zero, Quaternion.identity, 5) }, // Long Curve Down-Left
    };

    #endregion Legend

    /// <summary>Prefab for cells next to larger pieces for no clipping/ovelap.</summary>
    [SerializeField] private GameObject blankPlaceHolderPrefab;

    [SerializeField] private GameObject bigBlockHelper;

    /// <summary>Prefab for start/finish cells (placed on <see cref="LevelMap.StartPoint"/> and <see cref="LevelMap.FinishPoint"/>).</summary>
    [SerializeField] private GameObject raceTrackStartFinishPrefab;

    /// <summary>Prefab for intermediate checkpoint cells (non-start/finish with special value).</summary>
    [SerializeField] private GameObject checkpointPrefab;

    [Header("Track Settings")]
    /// <summary>World-space spacing per grid step (meters) when placing pieces.</summary>
    [SerializeField] private float blockOffset = 15;

    [Header("Track Layout")]
    /// <summary>Runtime copy of the level layout pulled from <see cref="GameDataManager"/> at start.</summary>
    [SerializeField, ReadOnly] private LevelMap levelMap;

    /// <summary>All supported pattern sizes (e.g., 5, 3) derived from legend keys.</summary>
    private List<int> possibleSizes = new List<int>();

    /// <summary>Reusable string builder for pattern extraction to reduce allocations.</summary>
    private StringBuilder patternBuilder = new StringBuilder();

    #region Setup

    /// <summary>
    /// Loads the current <see cref="LevelMap"/>, validates legend key sizes, and kicks off placement.
    /// </summary>
    private void Start()
    {
        levelMap = GameDataManager.Instance.CurrentLevelMap;

        if (levelMap == null)
        {
            return;
        }
        else
        {
            foreach (var value in trackPieceLegend.Values)
            {
                possibleSizes.Add(value.Size);
            }
            possibleSizes.Sort((a, b) => b.CompareTo(a)); // Sort descending

            PlaceTrackPieces();
        }
    }

    #endregion Setup

    #region Track Placement

    /// <summary>
    /// Creates a placement map from the level tiles, then walks the path from StartPoint,
    /// instantiating prefabs and registering checkpoints with <see cref="TrackManager"/>.
    /// </summary>
    private void PlaceTrackPieces()
    {
        if (levelMap == null || levelMap.Tiles == null)
        {
            Debug.LogError("LevelMap or its Tiles are not set.");
            return;
        }
        var piecesToPlace = CreateTrack();

        StopOverlaying(piecesToPlace);

        Coordinates? position = levelMap.StartPoint;
        HashSet<Coordinates> visited = new HashSet<Coordinates>();

        var trackManager = FindFirstObjectByType<TrackManager>();

        while (true)
        {
            if (position == null)
                break;

            if (piecesToPlace[position.Value.X, position.Value.Y].Prefab != null && piecesToPlace[position.Value.X, position.Value.Y].Prefab != blankPlaceHolderPrefab)
            {
                var newGameObject = Instantiate(piecesToPlace[position.Value.X, position.Value.Y].Prefab,
                            piecesToPlace[position.Value.X, position.Value.Y].Position,
                            piecesToPlace[position.Value.X, position.Value.Y].Rotation,
                            this.transform);

                if (piecesToPlace[position.Value.X, position.Value.Y].Prefab == raceTrackStartFinishPrefab || piecesToPlace[position.Value.X, position.Value.Y].Prefab == checkpointPrefab)
                {
                    trackManager.CheckPoints.Add(newGameObject.GetComponentInChildren<CheckPointListener>());

                    if (position.Value == levelMap.StartPoint)
                    {
                        trackManager.CarSpawn = newGameObject.transform;
                    }
                }
            }

            visited.Add(position.Value);
            position = Step(piecesToPlace, position.Value, visited);
        }

        trackManager.StartRace();
    }

    private void StopOverlaying(TrackPiece[,] pieces)
    {
        int surroundingBlockOverlayed;
        for (int i = 0; i < pieces.GetLength(0); i++)
        {
            for (int j = 0; j < pieces.GetLength(1); j++)
            {
                surroundingBlockOverlayed = (pieces[i, j].Size / 2) - 1;

                if (pieces[i, j].Prefab != null && 0 < surroundingBlockOverlayed)
                {
                    RemoveAllSurrounding(new Coordinates(i, j), pieces, surroundingBlockOverlayed);
                }
            }
        }
    }

    #endregion Track Placement

    #region Track Traversal

    /// <summary>
    /// Chooses the next unvisited neighbor cell that has a placeable piece, using 4-way adjacency.
    /// Returns <c>null</c> if no continuation exists.
    /// </summary>
    private Coordinates? Step(TrackPiece[,] piecesToPlace, Coordinates position, HashSet<Coordinates> visited)
    {
        foreach (var offset in offsets)
        {
            var next = position + offset;
            if (piecesToPlace.InBounds(next.X, next.Y) && piecesToPlace[next.X, next.Y].Prefab != null && !visited.Contains(next))
                return next;
        }

        return null;
    }

    #endregion Track Traversal

    #region Track Creation

    /// <summary>
    /// Produces a 2D map of <see cref="TrackPiece"/> descriptors for all track cells (tile values 1 and -2).
    /// Start/finish and checkpoints are assigned their dedicated prefabs in matching.
    /// </summary>
    private TrackPiece[,] CreateTrack()
    {
        TrackPiece[,] piecesToPlace = new TrackPiece[levelMap.Tiles.GetLength(0), levelMap.Tiles.GetLength(1)];
        TrackPiece? trackPiece;
        Coordinates coordinates;

        for (int x = 0; x < levelMap.Tiles.GetLength(0); x++)
        {
            for (int y = 0; y < levelMap.Tiles.GetLength(1); y++)
            {
                if (levelMap.Tiles[x, y] != 1 && levelMap.Tiles[x, y] != -2)
                    continue;

                coordinates = new Coordinates(x, y);
                trackPiece = GetTrackPiece(coordinates);

                if (trackPiece != null && trackPiece.Value.Prefab != null)
                {
                    piecesToPlace[x, y] = trackPiece.Value;
                }
            }
        }

        return piecesToPlace;
    }

    #region Pattern Matching

    /// <summary>
    /// Extracts local patterns of supported sizes around <paramref name="coordinates"/>, tries to find
    /// a legend match, then returns a filled <see cref="TrackPiece"/> with world position and final prefab
    /// (start/finish and checkpoints override the legend prefab).
    /// </summary>
    private TrackPiece? GetTrackPiece(Coordinates coordinates)
    {
        string pattern = "";

        foreach (var size in possibleSizes)
        {
            pattern = ExtractPattern(coordinates, size);
            if (trackPieceLegend.TryGetValue(pattern, out TrackPiece trackPiece))
            {
                Vector3 positionOffset = transform.position + new Vector3(coordinates.X * blockOffset, 0f, coordinates.Y * blockOffset);
                trackPiece.Position = positionOffset;

                if (coordinates == levelMap.StartPoint || coordinates == levelMap.FinishPoint)
                {
                    trackPiece.Prefab = raceTrackStartFinishPrefab;
                }
                else if (levelMap.Tiles.At(coordinates) == -2)
                {
                    trackPiece.Prefab = checkpointPrefab;
                }

                return trackPiece;
            }
        }

        Debug.LogWarning($"No track piece found for pattern at {coordinates}: {pattern}");

        return null;
    }

    /// <summary>
    /// Builds a flattened NxN pattern string centered at <paramref name="center"/>:
    /// - '1' marks track cells
    /// - 'X' marks empty/out-of-bounds
    /// - 'C' may mark checkpoints on larger kernels so they can be matched distinctly
    /// Isolated singletons are treated as empty to avoid stray tiles.
    /// </summary>
    private string ExtractPattern(Coordinates center, int size)
    {
        int halfSize = size / 2;
        char[,] pattern = new char[size, size];

        for (int dy = -halfSize; dy <= halfSize; dy++)
        {
            for (int dx = -halfSize; dx <= halfSize; dx++)
            {
                int x = center.X + dx;
                int y = center.Y + dy;

                int px = dx + halfSize;
                int py = dy + halfSize;

                char c = 'X';
                if (dx != 0 && dy != 0)
                {
                    c = 'X'; // Diagonal positions are always empty
                }
                else if (levelMap.Tiles.InBounds(x, y))
                {
                    if (levelMap.Tiles[x, y] == -2 || (x == levelMap.StartPoint.X && y == levelMap.StartPoint.Y) || (x == levelMap.FinishPoint.X && y == levelMap.FinishPoint.Y))
                    {
                        if (halfSize > 1)
                            c = 'C'; // Checkpoint treated as special piece in larger patterns
                        else
                            c = '1'; // Checkpoint treated as normal piece in smallest patterns
                    }
                    else if (levelMap.Tiles[x, y] == 1)
                    {
                        c = '1'; // Track piece
                    }
                }
                pattern[py, px] = c;
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (pattern[y, x] == '1' && IsAlone(new Coordinates(y, x), pattern))
                {
                    pattern[y, x] = 'X'; // Isolated track piece treated as empty
                }
            }
        }

        patternBuilder.Clear();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                patternBuilder.Append(pattern[y, x]);
            }
        }

        return patternBuilder.ToString();
    }

    #endregion Pattern Matching

    #endregion Track Creation

    #region Helper Methods

    /// <summary>
    /// Returns true if the given cell has no 4-way neighbor that is not 'X' in the provided pattern.
    /// Used to cull isolated track singles.
    /// </summary>
    private bool IsAlone(Coordinates position, char[,] pattern)
    {
        foreach (var offset in offsets)
        {
            var neighbor = position + offset;
            if (pattern.InBounds(neighbor.X, neighbor.Y) && pattern[neighbor.X, neighbor.Y] != 'X')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Checks whether any 4-way neighbor of <paramref name="position"/> in <paramref name="pieces"/>
    /// belongs to <paramref name="types"/> or matches a specific long-curve key in the legend.
    /// If true, placement at <paramref name="position"/> may be suppressed to avoid clashes.
    /// </summary>
    private bool NextToTypes(Coordinates position, TrackPiece[,] pieces, List<GameObject> types)
    {
        bool nextToType = false;
        foreach (var offset in offsets)
        {
            var neighbor = position + offset;
            if (pieces.InBounds(neighbor.X, neighbor.Y) && types.Contains(pieces[neighbor.X, neighbor.Y].Prefab))
            {
                nextToType = true;
                break;
            }
        }
        return nextToType;
    }

    private void RemoveBlankPieces(TrackPiece[,] pieces)
    {
        for (var i = 0; i < pieces.GetLength(0); i++)
        {
            for (var j = 0; j < pieces.GetLength(1); j++)
            {
                var piece = pieces[i, j];
                if (piece.Prefab == blankPlaceHolderPrefab)
                {
                    piece.Prefab = null;
                }
            }
        }
    }

    private void RemoveAllSurrounding(Coordinates position, TrackPiece[,] pieces, int halfSize)
    {
        for (int dx = -halfSize; dx <= halfSize; dx++)
        {
            for (int dy = -halfSize; dy <= halfSize; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int x = position.X + dx;
                int y = position.Y + dy;
                if (pieces.InBounds(x, y) && pieces[x, y].Prefab != null)
                {
                    pieces[x, y].Prefab = blankPlaceHolderPrefab;
                }
            }
        }
    }

    #endregion Helper Methods
}