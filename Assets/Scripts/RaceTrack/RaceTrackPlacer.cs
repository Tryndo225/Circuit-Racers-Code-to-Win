using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using Unity.Mathematics;

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

public class RaceTrackPlacer : MonoBehaviour
{
    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };

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
          "XCX" +
          "X1X", new TrackPiece(null, Vector3.zero, Quaternion.identity) }, // Straight Vertical Checkpoint

		{ "XXX" +
          "1C1" +
          "XXX", new TrackPiece(null, Vector3.zero, Quaternion.Euler(0, 90, 0)) }, // Straight Horizontal Checkpoint

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

    [SerializeField] private GameObject raceTrackStartFinishPrefab;
    [SerializeField] private List<GameObject> bigPieces;
    [SerializeField] private GameObject checkpointPrefab;

    [Header("Track Settings")]
    [SerializeField] private int blockOffset = 15;

    [Header("Track Layout")]
    [SerializeField, ReadOnly] private LevelMap levelMap;

    private List<int> possibleSizes = new List<int>();
    private StringBuilder patternBuilder = new StringBuilder();

    private void Start()
    {
        levelMap = GameDataManager.Instance.CurrentLevelMap;

        if (levelMap == null)
        {
            Debug.LogError("No LevelMap found in GameDataManager.");
            return;
        }
        else
        {
            Debug.Log("LevelMap found, starting track placement.");
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

    private void PlaceTrackPieces()
    {
        Debug.Log("Placing track pieces...");
        if (levelMap == null || levelMap.Tiles == null)
        {
            Debug.LogError("LevelMap or its Tiles are not set.");
            return;
        }

        var piecesToPlace = CreateTrack();
        Coordinates? position = levelMap.StartPoint;
        Coordinates? lastPosition = levelMap.StartPoint;
        bool nextToBigPiece = false;

        while (true)
        {
            lastPosition = position;
            position = Step(piecesToPlace, position.Value, lastPosition.Value);
            if (position == null)
                break;

            nextToBigPiece = false;
            foreach (var offset in offsets)
            {
                var neighbor = position.Value + offset;
                if (piecesToPlace.InBounds(neighbor.X, neighbor.Y) && bigPieces.Contains(piecesToPlace[neighbor.X, neighbor.Y].Prefab))
                {
                    nextToBigPiece = true;
                    break;
                }
            }
            if (piecesToPlace[position.Value.X, position.Value.Y].Prefab != null && !nextToBigPiece)
            {
                var newGameObject = Instantiate(piecesToPlace[position.Value.X, position.Value.Y].Prefab,
                            piecesToPlace[position.Value.X, position.Value.Y].Position,
                            piecesToPlace[position.Value.X, position.Value.Y].Rotation,
                            this.transform);

                if (piecesToPlace[position.Value.X, position.Value.Y].Prefab == raceTrackStartFinishPrefab || piecesToPlace[position.Value.X, position.Value.Y].Prefab == checkpointPrefab)
                {
                    FindFirstObjectByType<TrackManager>().CheckPoints.Add(newGameObject.GetComponentInChildren<CheckPointListener>());
                }
            }
        }
    }

    private Coordinates? Step(TrackPiece[,] piecesToPlace, Coordinates position, Coordinates lastPosition)
    {
        foreach (var offset in offsets)
        {
            var next = position + offset;
            if (!piecesToPlace.InBounds(next.X, next.Y))
                continue;
            if (piecesToPlace[next.X, next.Y].Prefab == null)
                continue;
            if (next == lastPosition)
                continue;

            return next;
        }

        return null;
    }

    private TrackPiece[,] CreateTrack()
    {
        Debug.Log("Creating track layout...");
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

    private TrackPiece? GetTrackPiece(Coordinates coordinates)
    {
        Debug.Log($"Getting track piece for coordinates: {coordinates}");
        foreach (var size in possibleSizes)
        {
            string pattern = ExtractPattern(coordinates, size);
            if (trackPieceLegend.TryGetValue(pattern, out TrackPiece trackPiece))
            {
                Vector3 positionOffset = new Vector3(coordinates.X * blockOffset, 0, coordinates.Y * blockOffset);
                trackPiece.Position = positionOffset;

                if (coordinates == levelMap.StartPoint || coordinates == levelMap.FinishPoint)
                {
                    trackPiece.Prefab = raceTrackStartFinishPrefab;
                }

                return trackPiece;
            }
        }
        return null;
    }

    private string ExtractPattern(Coordinates center, int size)
    {
        Debug.Log($"Extracting pattern at center: {center} with size: {size}");
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
                if (levelMap.Tiles.InBounds(x, y))
                {
                    int v = levelMap.Tiles[x, y];
                    c = (v == 1) ? '1' : (v == -2 ? 'C' : 'X');
                }
                pattern[py, px] = c;
            }
        }

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (pattern[x, y] == '1' && IsAlone(new Coordinates(x, y), pattern))
                {
                    pattern[x, y] = 'X'; // Isolated track piece treated as empty
                }
            }
        }

        patternBuilder.Clear();
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                patternBuilder.Append(pattern[i, j]);
            }
        }

        return patternBuilder.ToString();
    }

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
}