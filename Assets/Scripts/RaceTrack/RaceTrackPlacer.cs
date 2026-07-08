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
///
/// In the generated legend, <see cref="Position"/> is used as a local placement offset.
/// During track placement, the placer converts it into the final world-space position.
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
	/// <remarks>
	/// In the generated rule legend this value is treated as a local offset. During placement,
	/// the final world-space position is filled based on the grid coordinate and block size.
	/// </remarks>
	[Tooltip("Placement position. In rules this acts as a local offset; during placement it becomes the final world-space position.")]
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
	/// <param name="position">Placement position or local placement offset.</param>
	/// <param name="rotation">Placement rotation.</param>
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
/// Cell value used by the visual track-piece rule editor.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Represents one cell in a square pattern used to generate a compact pattern key.
///
/// These values are converted into the same pattern symbols used by <see cref="RaceTrackPlacer"/>:
/// - <see cref="Empty"/> becomes <c>X</c>.
/// - <see cref="Track"/> becomes <c>1</c>.
/// - <see cref="Checkpoint"/> becomes <c>C</c> for center checkpoint/start/finish cells.
/// </remarks>
public enum TrackPatternCell
{
	/// <summary>
	/// Empty or non-track cell. Converted to <c>X</c>.
	/// </summary>
	Empty,

	/// <summary>
	/// Track-compatible cell. Converted to <c>1</c>.
	/// </summary>
	Track,

	/// <summary>
	/// Checkpoint, start, or finish cell when it is the center of the currently matched pattern. Converted to <c>C</c>.
	/// </summary>
	Checkpoint
}

/// <summary>
/// Inspector-editable rule used to generate one entry in the track-piece legend.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Stores a visual pattern, prefab, placement offset, rotation, and pattern size.
///
/// This class is the editable source data. The visual pattern is converted into a compact
/// string key and stored in <see cref="StringTrackPieceDictionary"/> by <see cref="RaceTrackPlacer"/>.
/// </remarks>
[Serializable]
public class TrackPieceRule
{
	/// <summary>
	/// Human-readable name shown in the Inspector foldout.
	/// </summary>
	[Tooltip("Optional name shown in the Inspector to make this rule easier to recognize.")]
	public string Name;

	/// <summary>
	/// Prefab to instantiate when this rule matches.
	/// </summary>
	[Tooltip("Prefab instantiated when this pattern matches.")]
	public GameObject Prefab;

	/// <summary>
	/// Local position offset applied to the generated grid position.
	/// </summary>
	[Tooltip("Local placement offset added to the generated world position.")]
	public Vector3 PositionOffset;

	/// <summary>
	/// Euler rotation used when placing this track piece.
	/// </summary>
	[Tooltip("Euler rotation used when placing this track piece.")]
	public Vector3 RotationEuler;

	/// <summary>
	/// Square pattern size used by this rule, usually 3 or 5.
	/// </summary>
	[Tooltip("Square pattern size. Usually 3 for normal pieces or 5 for larger pieces.")]
	public int Size = 3;

	/// <summary>
	/// Flattened visual pattern edited through the custom Inspector.
	/// </summary>
	[Tooltip("Flattened visual pattern. X = empty, 1 = track, C = checkpoint/start/finish.")]
	public TrackPatternCell[] Pattern = new TrackPatternCell[9];

	/// <summary>
	/// Creates an empty <see cref="TrackPieceRule"/>.
	/// </summary>
	public TrackPieceRule() { }

	/// <summary>
	/// Creates a new <see cref="TrackPieceRule"/> from a compact pattern string.
	/// </summary>
	/// <param name="name">Human-readable rule name.</param>
	/// <param name="patternKey">Flattened pattern string using <c>X</c>, <c>1</c>, and <c>C</c>.</param>
	/// <param name="prefab">Prefab to instantiate when the pattern matches.</param>
	/// <param name="positionOffset">Local placement offset.</param>
	/// <param name="rotationEuler">Euler rotation used for placement.</param>
	/// <param name="size">Square pattern size.</param>
	public TrackPieceRule(string name, string patternKey, GameObject prefab, Vector3 positionOffset, Vector3 rotationEuler, int size = 3)
	{
		Name = name;
		Prefab = prefab;
		PositionOffset = positionOffset;
		RotationEuler = rotationEuler;
		Size = size;
		Pattern = PatternFromKey(patternKey, size);
	}

	/// <summary>
	/// Converts this editable rule into a runtime <see cref="TrackPiece"/>.
	/// </summary>
	/// <returns>Track-piece descriptor used by the pattern legend.</returns>
	public TrackPiece ToTrackPiece()
	{
		return new TrackPiece(Prefab, PositionOffset, Quaternion.Euler(RotationEuler), Size);
	}

	/// <summary>
	/// Converts the visual pattern into a compact flattened string key.
	/// </summary>
	/// <returns>Pattern string using <c>X</c>, <c>1</c>, and <c>C</c>.</returns>
	public string ToPatternKey()
	{
		NormalizePatternLength();

		StringBuilder builder = new StringBuilder(Size * Size);

		for (int i = 0; i < Pattern.Length; i++)
		{
			builder.Append(Pattern[i] switch
			{
				TrackPatternCell.Track => '1',
				TrackPatternCell.Checkpoint => 'C',
				_ => 'X'
			});
		}

		return builder.ToString();
	}

	/// <summary>
	/// Ensures that the pattern size is valid and that the pattern array has the correct length.
	/// </summary>
	/// <remarks>
	/// Pattern sizes must be odd so that there is a clear center cell.
	/// </remarks>
	public void NormalizePatternLength()
	{
		if (Size < 3)
			Size = 3;

		if (Size % 2 == 0)
			Size += 1;

		int requiredLength = Size * Size;

		if (Pattern == null)
		{
			Pattern = new TrackPatternCell[requiredLength];
			return;
		}

		if (Pattern.Length != requiredLength)
			Array.Resize(ref Pattern, requiredLength);
	}

	/// <summary>
	/// Converts a compact pattern string into visual pattern cells.
	/// </summary>
	/// <param name="key">Flattened pattern string using <c>X</c>, <c>1</c>, and <c>C</c>.</param>
	/// <param name="size">Square pattern size.</param>
	/// <returns>Pattern cell array.</returns>
	private static TrackPatternCell[] PatternFromKey(string key, int size)
	{
		int length = size * size;
		TrackPatternCell[] pattern = new TrackPatternCell[length];

		for (int i = 0; i < length; i++)
		{
			char c = i < key.Length ? key[i] : 'X';

			pattern[i] = c switch
			{
				'1' => TrackPatternCell.Track,
				'C' => TrackPatternCell.Checkpoint,
				_ => TrackPatternCell.Empty
			};
		}

		return pattern;
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
/// In this version, the dictionary is generated from <see cref="TrackPieceRule"/> entries so that
/// developers can edit rules visually while still inspecting the resulting pattern strings.
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
/// - <see cref="trackPieceRules"/> must represent square patterns such as 3x3 or 5x5.
/// - <see cref="trackPieceLegend"/> is generated from <see cref="trackPieceRules"/> and stores the compact pattern strings.
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

	[Header("Track Piece Rules")]

	/// <summary>
	/// Editable visual rule list used to generate <see cref="trackPieceLegend"/>.
	/// </summary>
	/// <remarks>
	/// Each rule stores a visual pattern grid and placement data. <see cref="OnValidate"/> converts
	/// this list into the read-only string-keyed legend shown below.
	/// </remarks>
	[SerializeField]
	private List<TrackPieceRule> trackPieceRules = new List<TrackPieceRule>()
	{
		new TrackPieceRule(
			"Straight Vertical",
			"X1X" +
			"X1X" +
			"X1X",
			null,
			Vector3.zero,
			Vector3.zero
		),

		new TrackPieceRule(
			"Straight Horizontal",
			"XXX" +
			"111" +
			"XXX",
			null,
			Vector3.zero,
			new Vector3(0f, 90f, 0f)
		),

		new TrackPieceRule(
			"Curve Up-Right",
			"X1X" +
			"X11" +
			"XXX",
			null,
			Vector3.zero,
			Vector3.zero
		),

		new TrackPieceRule(
			"Curve Up-Left",
			"X1X" +
			"11X" +
			"XXX",
			null,
			Vector3.zero,
			new Vector3(0f, 90f, 0f)
		),

		new TrackPieceRule(
			"Curve Down-Left",
			"XXX" +
			"11X" +
			"X1X",
			null,
			Vector3.zero,
			new Vector3(0f, 180f, 0f)
		),

		new TrackPieceRule(
			"Curve Down-Right",
			"XXX" +
			"X11" +
			"X1X",
			null,
			Vector3.zero,
			new Vector3(0f, 270f, 0f)
		),

		new TrackPieceRule(
			"Long Curve Up-Right",
			"XX1XX" +
			"XX1XX" +
			"XX111" +
			"XXXXX" +
			"XXXXX",
			null,
			Vector3.zero,
			Vector3.zero,
			5
		),

		new TrackPieceRule(
			"Long Curve Up-Left",
			"XX1XX" +
			"XX1XX" +
			"111XX" +
			"XXXXX" +
			"XXXXX",
			null,
			Vector3.zero,
			Vector3.zero,
			5
		),

		new TrackPieceRule(
			"Long Curve Down-Right",
			"XXXXX" +
			"XXXXX" +
			"XX111" +
			"XX1XX" +
			"XX1XX",
			null,
			Vector3.zero,
			Vector3.zero,
			5
		),

		new TrackPieceRule(
			"Long Curve Down-Left",
			"XXXXX" +
			"XXXXX" +
			"111XX" +
			"XX1XX" +
			"XX1XX",
			null,
			Vector3.zero,
			Vector3.zero,
			5
		)
	};

	[Header("Generated Pattern Legend")]

	/// <summary>
	/// Generated legend mapping string patterns to <see cref="TrackPiece"/> descriptors.
	/// </summary>
	/// <remarks>
	/// Pattern strings are flattened character grids. In these strings:
	/// - <c>1</c> represents a track-compatible cell.
	/// - <c>X</c> represents an empty cell.
	/// - <c>C</c> may represent a checkpoint/start/finish marker during larger-pattern matching.
	///
	/// The dictionary is rebuilt from <see cref="trackPieceRules"/> in <see cref="OnValidate"/> so
	/// the developer can inspect the generated pattern strings in the Inspector. It is also rebuilt
	/// in <see cref="Start"/> as a safety step before placement begins.
	/// </remarks>
	[Tooltip("Read-only generated dictionary. Built from the visual track-piece rules so the compact pattern strings can be inspected.")]
	[SerializeField, ReadOnly]
	private StringTrackPieceDictionary trackPieceLegend = new StringTrackPieceDictionary();

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

	#region Setup

	/// <summary>
	/// Unity validation callback used to keep the generated pattern legend in sync in the Inspector.
	/// </summary>
	/// <remarks>
	/// Whenever a rule is edited, Unity calls this method in the editor. The visual rule list is
	/// converted into the read-only <see cref="trackPieceLegend"/> so developers can immediately
	/// inspect the generated pattern strings.
	/// </remarks>
	private void OnValidate()
	{
		RebuildTrackPieceLegend(false);
	}

	/// <summary>
	/// Loads the current <see cref="LevelMap"/>, refreshes the generated legend, collects supported
	/// pattern sizes, and starts placement.
	/// </summary>
	private void Start()
	{
		LevelMap currentLevelMap = GameDataManager.Instance.CurrentLevelMap;

		if (currentLevelMap == null)
		{
			Debug.LogError("[RaceTrackPlacer] No current level map selected.");
			return;
		}

		levelMap = currentLevelMap.Copy();

		RebuildTrackPieceLegend(true);
		CollectPossibleSizes();

		PlaceTrackPieces();
	}

	/// <summary>
	/// Rebuilds the string-keyed legend from the visual <see cref="trackPieceRules"/> list.
	/// </summary>
	/// <param name="logDuplicates">Whether duplicate pattern keys should be reported to the console.</param>
	/// <remarks>
	/// This method is used both by <see cref="OnValidate"/> and <see cref="Start"/>. The generated
	/// dictionary remains serialized and visible in the Inspector, but the visual rule list is the
	/// source of truth.
	/// </remarks>
	private void RebuildTrackPieceLegend(bool logDuplicates)
	{
		if (trackPieceLegend == null)
			trackPieceLegend = new StringTrackPieceDictionary();

		trackPieceLegend.Clear();

		if (trackPieceRules == null)
			return;

		foreach (TrackPieceRule rule in trackPieceRules)
		{
			if (rule == null)
				continue;

			rule.NormalizePatternLength();

			string key = rule.ToPatternKey();
			TrackPiece piece = rule.ToTrackPiece();

			if (trackPieceLegend.ContainsKey(key) && logDuplicates)
			{
				Debug.LogWarning($"[RaceTrackPlacer] Duplicate track-piece pattern found: {key}. The later rule will overwrite the earlier one.");
			}

			trackPieceLegend[key] = piece;
		}
	}

	/// <summary>
	/// Collects supported pattern sizes from the generated legend.
	/// </summary>
	/// <remarks>
	/// Larger pattern sizes are sorted first so the placer attempts large pieces before smaller
	/// fallback pieces.
	/// </remarks>
	private void CollectPossibleSizes()
	{
		possibleSizes.Clear();

		foreach (TrackPiece value in trackPieceLegend.Values)
		{
			if (!possibleSizes.Contains(value.Size))
			{
				possibleSizes.Add(value.Size);
			}
		}

		possibleSizes.Sort((a, b) => b.CompareTo(a));
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
			trackPiece.Position = positionOffset + trackPiece.Position;

			if (coordinates == levelMap.StartPoint || coordinates == levelMap.FinishPoint)
			{
				Debug.Log($"[RaceTrackPlacer] Assigning start/finish prefab at {coordinates}");
				trackPiece.Prefab = levelMap.Circuit ? raceTrackStartFinishPrefab : raceTrackStartFinishP2pPrefab;
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
	/// - <c>C</c> marks a checkpoint, start, or finish only when it is the center cell currently being matched.
	///
	/// Checkpoint cells that appear as neighbouring cells are converted to <c>1</c>.
	/// This prevents the rule table from needing separate variants for ordinary track pieces that are merely
	/// next to a checkpoint.
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
					bool isCheckpointLike = levelMap.Tiles[x, y] == CheckpointTile || (x == levelMap.StartPoint.X && y == levelMap.StartPoint.Y) || (x == levelMap.FinishPoint.X && y == levelMap.FinishPoint.Y);

					bool isCenter = dx == 0 && dy == 0;

					if (isCheckpointLike)
					{
						c = isCenter ? 'C' : '1';
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