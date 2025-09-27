using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.UIElements;

#region Helper Structures / Classes
[Serializable]
public struct TrackPiece
{
    public GameObject Prefab;
    public Vector3 Position;
    public Quaternion Rotation;

    public TrackPiece(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Prefab = prefab;
        Position = position;
        Rotation = rotation;
    }
}

[Serializable]
public class StringTrackPieceDictionary : SerializableDictionary<string, TrackPiece>
{ }
#endregion Helper Structures / Classes

public class RaceTrackPlacer : MonoBehaviour
{
    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };
    #region Legend
    [Header("Track Piece Prefabs")]
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
          "XXXXX", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Long Curve Up-Right

        { "XX1XX" +
          "XX1XX" +
          "111XX" +
          "XXXXX" +
          "XXXXX", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Long Curve Up-Left

		{ "XXXXX" +
          "XXXXX" +
          "XX111" +
          "XX1XX" +
          "XX1XX", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Long Curve Down-Right

		{ "XXXXX" +
          "XXXXX" +
          "111XX" +
          "XX1XX" +
          "XX1XX", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Long Curve Down-Left
	};
    #endregion Legend

    [SerializeField] private GameObject raceTrackStartFinishPrefab;
    [SerializeField] private List<GameObject> bigPieces;
    [SerializeField] private GameObject checkpointPrefab;

    [Header("Track Settings")]
    [SerializeField] private int blockOffset = 15;

    [Header("Track Layout")]
    [SerializeField, ReadOnly] private LevelMap levelMap;

    private List<int> possibleSizes = new List<int>();
    private StringBuilder patternBuilder = new StringBuilder();

    #region Setup
    private void Start()
    {
        levelMap = GameDataManager.Instance.CurrentLevelMap;

        if (levelMap == null)
        {
            return;
        }
        else
        {
            foreach (var key in trackPieceLegend.Keys)
            {
                int keySquared = Mathf.RoundToInt(Mathf.Sqrt(key.Length));

                if (keySquared * keySquared != key.Length)
                {
                    Debug.LogError($"Legend key length {key.Length} is not a perfect square: {key}");
                    continue;
                }
                if (!possibleSizes.Contains(keySquared))
                    possibleSizes.Add(keySquared);
            }
            possibleSizes.Sort((a, b) => b.CompareTo(a)); // Sort descending
            PlaceTrackPieces();
        }
    }
    #endregion Setup

    #region Track Placement
    private void PlaceTrackPieces()
    {
        if (levelMap == null || levelMap.Tiles == null)
        {
            Debug.LogError("LevelMap or its Tiles are not set.");
            return;
        }

        var piecesToPlace = CreateTrack();
        Coordinates? position = levelMap.StartPoint;
        HashSet<Coordinates> visited = new HashSet<Coordinates>();

        bool nextToBigPiece = false;
        var trackManager = FindFirstObjectByType<TrackManager>();

        while (true)
        {
            if (position == null)
                break;

            nextToBigPiece = NextToTypes(position.Value, piecesToPlace, bigPieces);
            
            if (piecesToPlace[position.Value.X, position.Value.Y].Prefab != null && !nextToBigPiece)
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
    #endregion Track Placement

    #region Track Traversal

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

    private bool NextToTypes(Coordinates position, TrackPiece[,] pieces, List<GameObject> types)
    {
        bool nextToType = false;
        foreach (var offset in offsets)
        {
            var neighbor = position + offset;
            if (pieces.InBounds(neighbor.X, neighbor.Y) && (types.Contains(pieces[neighbor.X, neighbor.Y].Prefab) || pieces[neighbor.X, neighbor.Y].Prefab == trackPieceLegend["XX1XXXX1XXXX111XXXXXXXXXX"].Prefab))
            {
                nextToType = true;
                break;
            }
        }
        return nextToType;
    }
    #endregion Helper Methods
}