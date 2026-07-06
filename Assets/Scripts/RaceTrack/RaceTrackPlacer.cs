using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#region Helper Structures / Classes

/// <summary>
/// Describes a single placeable track piece: prefab, world-space pose, and pattern size.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Stores the prefab and placement data used by <see cref="RaceTrackPlacer"/>.
/// </remarks>
[Serializable]
public struct TrackPiece
{
	/// <summary>
	/// Prefab to instantiate for this track cell.
	/// </summary>
	[Tooltip("Prefab to instantiate for this track cell.")]
	public GameObject Prefab;

	/// <summary>
	/// World-space position used when placing the prefab.
	/// </summary>
	[Tooltip("World-space position used when placing the prefab.")]
	public Vector3 Position;

	/// <summary>
	/// World-space rotation used when placing the prefab.
	/// </summary>
	[Tooltip("World-space rotation used when placing the prefab.")]
	public Quaternion Rotation;

	/// <summary>
	/// Pattern size used by this piece, usually 3 or 5.
	/// </summary>
	[Tooltip("Pattern size used by this piece, usually 3 or 5.")]
	public int Size;

	/// <summary>
	/// Creates a new <see cref="TrackPiece"/> with explicit prefab, pose, and pattern size.
	/// </summary>
	/// <param name="prefab">Prefab reference. May be null for an empty/default legend entry.</param>
	/// <param name="position">World-space placement position.</param>
	/// <param name="rotation">World-space placement rotation.</param>
	/// <param name="size">Pattern size used by this piece.</param>
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
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Stores the pattern legend used by <see cref="RaceTrackPlacer"/>.
///
/// Keys are compact pattern encodings, for example flattened 3x3 or 5x5 matrices.
/// </remarks>
[Serializable]
public class StringTrackPieceDictionary : SerializableDictionary<string, TrackPiece>
{ }

#endregion Helper Structures / Classes

/// <summary>
/// Procedural track placer that scans a <see cref="LevelMap"/>, matches local tile patterns,
/// and instantiates track-piece prefabs.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Builds the visible track scene from a grid-based level map.
///
/// The placer reads the selected level from <see cref="GameDataManager.CurrentLevelMap"/>,
/// creates a working placement map, walks the track from <see cref="LevelMap.StartPoint"/>,
/// instantiates the matching prefabs, registers checkpoints, and either starts the race or
/// initializes replay playback.
///
/// Requirements:
/// - <see cref="trackPieceLegend"/> keys must represent square patterns such as 3x3 or 5x5.
/// - <see cref="GameDataManager.CurrentLevelMap"/> must contain a valid <see cref="LevelMap"/> before placement starts.
/// - Track cells use <see cref="LevelMap.LevelTileTypes.Track"/>.
/// - Checkpoint cells use <see cref="LevelMap.LevelTileTypes.CP"/>.
///
/// Threading:
/// - Unity main thread only, because this component instantiates prefabs and accesses scene objects.
/// </remarks>
public class RaceTrackPlacer : MonoBehaviour
{
	/// <summary>
	/// Integer tile value used for road/track cells.
	/// </summary>
	private const int TrackTile = (int)LevelMap.LevelTileTypes.Track;

	/// <summary>
	/// Integer tile value used for checkpoint cells.
	/// </summary>
	private const int CheckpointTile = (int)LevelMap.LevelTileTypes.CP;

	/// <summary>
	/// Internal marker used while placing larger pieces so overlapping road cells are not placed twice.
	/// </summary>
	private const int UsedTileValue = -5;

	/// <summary>
	/// Neighbor offsets for four-way traversal and adjacency tests: right, left, up, and down.
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
	/// </summary>
	/// <remarks>
	/// Pattern strings are flattened character grids. In these strings:
	/// - <c>1</c> represents a track-compatible cell.
	/// - <c>X</c> represents an empty cell.
	/// - <c>C</c> may represent a checkpoint/start/finish marker during larger-pattern matching.
	///
	/// The associated <see cref="TrackPiece"/> carries the prefab and default rotation for the matched shape.
	/// Final world position is filled during placement.
	/// </remarks>
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

	/// <summary>
	/// Prefab used for cells covered by larger pieces so traversal can continue without visible overlap.
	/// </summary>
	[Tooltip("Prefab used for cells covered by larger pieces so traversal can continue without visible overlap.")]
	[SerializeField] private GameObject traversalPrefab;

	/// <summary>
	/// Prefab used for start/finish cells on circuit maps.
	/// </summary>
	[Tooltip("Prefab used for start/finish cells on circuit maps.")]
	[SerializeField] private GameObject raceTrackStartFinishPrefab;

	/// <summary>
	/// Prefab used for start/finish cells on point-to-point maps.
	/// </summary>
	[Tooltip("Prefab used for start/finish cells on point-to-point maps.")]
	[SerializeField] private GameObject raceTrackStartFinishP2pPrefab;

	/// <summary>
	/// Prefab used for intermediate checkpoint cells.
	/// </summary>
	[Tooltip("Prefab used for intermediate checkpoint cells.")]
	[SerializeField] private GameObject checkpointPrefab;

	[Header("Track Settings")]
	/// <summary>
	/// World-space spacing per grid step.
	/// </summary>
	[Tooltip("World-space spacing per grid step.")]
	[SerializeField] private float blockOffset = 15;

	[Header("Track Layout")]
	/// <summary>
	/// Runtime copy of the selected level layout.
	/// </summary>
	[Tooltip("Runtime copy of the selected level layout.")]
	[SerializeField, ReadOnly] private LevelMap levelMap;

	[Header("Build Options")]
	/// <summary>
	/// Whether this placer should build the track for replay playback instead of starting a live race.
	/// </summary>
	[Tooltip("If enabled, the track is built for replay playback instead of starting a live race.")]
	[SerializeField] private bool isBuildForReplay;

	/// <summary>
	/// Supported pattern sizes derived from the legend values.
	/// </summary>
	private List<int> possibleSizes = new List<int>();

	/// <summary>
	/// Reusable string builder for pattern extraction.
	/// </summary>
	private StringBuilder patternBuilder = new StringBuilder();

	/// <summary>
	/// Traversal piece used for internally occupied cells.
	/// </summary>
	private TrackPiece traversalTrackPiece;

	#region Setup

	/// <summary>
	/// Loads the current <see cref="LevelMap"/>, collects supported pattern sizes, and starts placement.
	/// </summary>
	private void Start()
	{
		traversalTrackPiece = new TrackPiece(traversalPrefab, Vector3.zero, Quaternion.identity);

		LevelMap currentLevelMap = GameDataManager.Instance.CurrentLevelMap;

		if (currentLevelMap == null)
		{
			Debug.LogError("[RaceTrackPlacer] No current level map selected.");
			return;
		}

		levelMap = currentLevelMap.Copy();

		foreach (var value in trackPieceLegend.Values)
		{
			if (!possibleSizes.Contains(value.Size))
			{
				possibleSizes.Add(value.Size);
			}
		}

		possibleSizes.Sort((a, b) => b.CompareTo(a));

		PlaceTrackPieces();
	}

	#endregion Setup

	#region Track Placement

	/// <summary>
	/// Creates a placement map, walks the track from the start point, instantiates prefabs,
	/// registers checkpoints, and starts either race or replay mode.
	/// </summary>
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

		int checkpointAmount = 0;

		while (true)
		{
			if (position == null)
				break;

			if (piecesToPlace[position.Value.X, position.Value.Y].Prefab != null && piecesToPlace[position.Value.X, position.Value.Y].Prefab != traversalPrefab)
			{
				var newGameObject = Instantiate(piecesToPlace[position.Value.X, position.Value.Y].Prefab, piecesToPlace[position.Value.X, position.Value.Y].Position, piecesToPlace[position.Value.X, position.Value.Y].Rotation, this.transform);

				if (IsCheckpointTile(position.Value))
				{
					CheckPointListener checkpoint = newGameObject.GetComponentInChildren<CheckPointListener>();

					if (checkpoint == null)
					{
						Debug.LogError($"[RaceTrackPlacer] Checkpoint tile at {position.Value} was placed, but prefab has no CheckPointListener.");
					}
					else
					{
						checkpoint.CheckpointOrder = checkpointAmount;
						++checkpointAmount;
					}

					if (IsStartTile(position.Value) && !isBuildForReplay)
						TrackManager.Instance.CarSpawn = newGameObject.transform;
				}
			}

			visited.Add(position.Value);
			position = Step(piecesToPlace, position.Value, visited);
		}

		CheckPointManager.Instance.ClearCheckPoints();
		CheckPointManager.Instance.AutoAddCheckpoints();

		if (!isBuildForReplay)
		{
			TrackManager.Instance.StartRace(levelMap);
		}
		else
		{
			Debug.Log("Building finished setting Replay");
			ReplayPreviewer.Instance.SetReplay(GameDataManager.Instance.CurrentGameData.GetBestReplay(GameDataManager.Instance.CurrentLevelMap));
			Debug.Log("Replay Set.");
		}
	}

	/// <summary>
	/// Replaces cells around larger placed pieces with traversal placeholders to avoid visual overlap.
	/// </summary>
	/// <param name="pieces">Placement map to adjust.</param>
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
	/// Chooses the next unvisited neighboring cell with a placeable piece.
	/// </summary>
	/// <param name="piecesToPlace">Placement map being traversed.</param>
	/// <param name="position">Current track coordinate.</param>
	/// <param name="visited">Already visited coordinates.</param>
	/// <returns>Next coordinate, or <c>null</c> when no continuation exists.</returns>
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
	/// Produces a 2D map of <see cref="TrackPiece"/> descriptors for track and checkpoint cells.
	/// </summary>
	/// <returns>Placement map containing track-piece descriptors.</returns>
	private TrackPiece[,] CreateTrack()
	{
		TrackPiece[,] piecesToPlace = new TrackPiece[levelMap.Tiles.GetLength(0), levelMap.Tiles.GetLength(1)];
		TrackPiece? trackPiece;
		Coordinates coordinates;

		foreach (int size in possibleSizes)
		{
			for (int x = 0; x < levelMap.Tiles.GetLength(0); x++)
			{
				for (int y = 0; y < levelMap.Tiles.GetLength(1); y++)
				{
					if (piecesToPlace[x, y].Prefab != null || !IsTrackOrCheckpointValue(levelMap.Tiles[x, y]))
						continue;

					coordinates = new Coordinates(x, y);
					trackPiece = GetTrackPiece(coordinates, size, size == possibleSizes[possibleSizes.Count - 1]);

					if (trackPiece != null && trackPiece.Value.Prefab != null)
					{
						MarkSpaceAsUsed(coordinates, size);
						AddTraversalPlaceHolders(coordinates, size, piecesToPlace);
						piecesToPlace[x, y] = trackPiece.Value;
					}
				}
			}
		}

		return piecesToPlace;
	}

	#region Pattern Matching

	/// <summary>
	/// Extracts a local pattern, finds a matching legend entry, and returns a filled <see cref="TrackPiece"/>.
	/// </summary>
	/// <param name="coordinates">Center coordinate to match.</param>
	/// <param name="size">Pattern size to extract.</param>
	/// <param name="allowOverlap">Whether internally used cells may still count as track while matching.</param>
	/// <returns>Matched track piece, or <c>null</c> if no matching pattern exists.</returns>
	private TrackPiece? GetTrackPiece(Coordinates coordinates, int size, bool allowOverlap)
	{
		string pattern = ExtractPattern(coordinates, size, allowOverlap);
		if (trackPieceLegend.TryGetValue(pattern, out TrackPiece trackPiece))
		{
			Vector3 positionOffset = transform.position + new Vector3(coordinates.X * blockOffset, 0f, coordinates.Y * blockOffset);
			trackPiece.Position = positionOffset;

			if (coordinates == levelMap.StartPoint || coordinates == levelMap.FinishPoint)
			{
				Debug.Log($"[RaceTrackPlacer] Assigning start/finish prefab at {coordinates}");
				trackPiece.Prefab = levelMap.Circuit ? raceTrackStartFinishPrefab : raceTrackStartFinishP2pPrefab;
			}
			else if (levelMap.Tiles.At(coordinates) == CheckpointTile)
			{
				trackPiece.Prefab = checkpointPrefab;
			}

			return trackPiece;
		}

		return null;
	}

	/// <summary>
	/// Builds a flattened pattern string centered at the given coordinate.
	/// </summary>
	/// <param name="center">Center coordinate of the pattern.</param>
	/// <param name="size">Pattern width and height.</param>
	/// <param name="allowOverlap">Whether internally used cells may count as track.</param>
	/// <returns>Flattened pattern string.</returns>
	/// <remarks>
	/// Pattern symbols:
	/// - <c>1</c> marks track-compatible cells.
	/// - <c>X</c> marks empty cells.
	/// - <c>C</c> marks checkpoints, start, or finish in larger kernels.
	///
	/// Isolated single track symbols are converted to <c>X</c> to avoid stray matches.
	/// </remarks>
	private string ExtractPattern(Coordinates center, int size, bool allowOverlap)
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
					c = 'X';
				}
				else if (levelMap.Tiles.InBounds(x, y))
				{
					if (levelMap.Tiles[x, y] == CheckpointTile ||
						(x == levelMap.StartPoint.X && y == levelMap.StartPoint.Y) ||
						(x == levelMap.FinishPoint.X && y == levelMap.FinishPoint.Y))
					{
						if ((Mathf.Abs(dx) < 2 || Mathf.Abs(dy) < 2) && halfSize > 1)
							c = 'C';
						else
							c = '1';
					}
					else if (levelMap.Tiles[x, y] == TrackTile || allowOverlap && levelMap.Tiles[x, y] == UsedTileValue)
					{
						c = '1';
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
					pattern[y, x] = 'X';
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
	/// Checks whether a tile value should be treated as track during placement.
	/// </summary>
	/// <param name="tileValue">Tile value to check.</param>
	/// <returns><c>true</c> for track and checkpoint tiles; otherwise <c>false</c>.</returns>
	private static bool IsTrackOrCheckpointValue(int tileValue)
	{
		return tileValue == TrackTile || tileValue == CheckpointTile;
	}

	/// <summary>
	/// Returns true if the given cell has no four-way neighbor that is not empty in the pattern.
	/// </summary>
	/// <param name="position">Pattern coordinate to check.</param>
	/// <param name="pattern">Pattern grid.</param>
	/// <returns><c>true</c> if the pattern cell is isolated; otherwise <c>false</c>.</returns>
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
	/// Replaces surrounding placed pieces with traversal placeholders.
	/// </summary>
	/// <param name="position">Center coordinate of the larger piece.</param>
	/// <param name="pieces">Placement map to modify.</param>
	/// <param name="halfSize">Radius of the surrounding area to replace.</param>
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
					pieces[x, y].Prefab = traversalPrefab;
				}
			}
		}
	}

	/// <summary>
	/// Marks cells occupied by a larger piece so they are not placed as independent visible pieces.
	/// </summary>
	/// <param name="position">Center coordinate of the placed piece.</param>
	/// <param name="size">Pattern size of the placed piece.</param>
	private void MarkSpaceAsUsed(Coordinates position, int size)
	{
		int usedArea = (size / 2) - 1;
		Coordinates coords;

		for (int dx = -usedArea; dx <= usedArea; dx++)
		{
			for (int dy = -usedArea; dy <= usedArea; dy++)
			{
				if (dx != 0 && dy != 0)
					continue;

				coords = new Coordinates(position.X + dx, position.Y + dy);

				if (!levelMap.Tiles.InBounds(coords.X, coords.Y))
					continue;

				if (coords == levelMap.StartPoint || coords == levelMap.FinishPoint || levelMap.Tiles.At(coords) == CheckpointTile)
					continue;

				if (levelMap.Tiles.At(coords) == TrackTile)
					levelMap.Tiles.At(coords) = UsedTileValue;
			}
		}
	}

	/// <summary>
	/// Adds traversal placeholders to the placement map for cells already covered by a larger piece.
	/// </summary>
	/// <param name="position">Center coordinate of the placed piece.</param>
	/// <param name="size">Pattern size of the placed piece.</param>
	/// <param name="pieces">Placement map to update.</param>
	private void AddTraversalPlaceHolders(Coordinates position, int size, TrackPiece[,] pieces)
	{
		int usedArea = (size / 2) - 1;

		for (int dx = -usedArea; dx <= usedArea; dx++)
		{
			for (int dy = -usedArea; dy <= usedArea; dy++)
			{
				if (dx != 0 && dy != 0)
					continue;

				int x = position.X + dx;
				int y = position.Y + dy;

				if (levelMap.Tiles.InBounds(x, y) && levelMap.Tiles[x, y] == UsedTileValue)
				{
					if (pieces[x, y].Prefab == null)
						pieces[x, y].Prefab = traversalPrefab;
				}
			}
		}
	}

	/// <summary>
	/// Returns true if the coordinate represents the start, finish, or an intermediate checkpoint.
	/// </summary>
	/// <param name="coordinates">Coordinate to check.</param>
	/// <returns><c>true</c> if the coordinate should receive a checkpoint-style prefab.</returns>
	private bool IsCheckpointTile(Coordinates coordinates)
	{
		return coordinates == levelMap.StartPoint ||
			   coordinates == levelMap.FinishPoint ||
			   levelMap.Tiles.At(coordinates) == CheckpointTile;
	}

	/// <summary>
	/// Returns true if the coordinate represents the race start.
	/// </summary>
	/// <param name="coordinates">Coordinate to check.</param>
	/// <returns><c>true</c> if the coordinate is the level start point.</returns>
	private bool IsStartTile(Coordinates coordinates)
	{
		return coordinates == levelMap.StartPoint;
	}

	#endregion Helper Methods
}