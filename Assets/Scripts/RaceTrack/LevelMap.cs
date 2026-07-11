#region Level Map Class

using System;
using System.Runtime.Serialization;
using System.Text;

/// <summary>
/// Serializable level definition for a grid-based race track.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Stores track dimensions, metadata, start/finish positions, lap settings, day/night state, and tile data.
///
/// Tile values are defined by <see cref="LevelTileTypes"/>:
/// - <see cref="LevelTileTypes.CP"/>: checkpoint.
/// - <see cref="LevelTileTypes.Spacer"/>: spacer.
/// - <see cref="LevelTileTypes.Grass"/>: empty/grass.
/// - <see cref="LevelTileTypes.Track"/>: road.
/// - <see cref="LevelTileTypes.PlaceHolder"/> and higher values: temporary placeholders used during generation.
///
/// The runtime tile grid is stored in <see cref="Tiles"/>. Because Unity does not serialize rectangular 2D arrays directly,
/// <see cref="tilesFlatRLEString"/> is used as a flattened and run-length encoded serialization representation.
/// </remarks>
[Serializable]
public class LevelMap : ISerializable, UnityEngine.ISerializationCallbackReceiver
{
	/// <summary>
	/// Four-neighbor step offsets used for grid traversal: right, left, up, and down.
	/// </summary>
	public static readonly Coordinates[] CardinalDirections = new Coordinates[]
	{
		new Coordinates(1, 0),
		new Coordinates(-1, 0),
		new Coordinates(0, 1),
		new Coordinates(0, -1)
	};

	/// <summary>
	/// Display name of the level.
	/// </summary>
	[UnityEngine.Tooltip("Display name of the level.")]
	public string Name;

	/// <summary>
	/// Width of the level grid in tiles.
	/// </summary>
	[UnityEngine.Tooltip("Width of the level grid in tiles.")]
	public int Width;

	/// <summary>
	/// Height of the level grid in tiles.
	/// </summary>
	[UnityEngine.Tooltip("Height of the level grid in tiles.")]
	public int Height;

	/// <summary>
	/// Whether the level is a closed circuit instead of a point-to-point track.
	/// </summary>
	[UnityEngine.Tooltip("Whether the level is a closed circuit instead of a point-to-point track.")]
	public bool Circuit;

	/// <summary>
	/// Start cell coordinate of the level.
	/// </summary>
	[UnityEngine.Tooltip("Start cell coordinate of the level.")]
	public Coordinates StartPoint;

	/// <summary>
	/// Finish cell coordinate of the level.
	/// </summary>
	/// <remarks>
	/// For circuit levels, this is expected to match <see cref="StartPoint"/>.
	/// </remarks>
	[UnityEngine.Tooltip("Finish cell coordinate of the level. For circuits, this usually matches the start point.")]
	public Coordinates FinishPoint;

	/// <summary>
	/// Number of laps used by the level.
	/// </summary>
	[UnityEngine.Tooltip("Number of laps used by the level.")]
	public int Laps;

	/// <summary>
	/// Number of checkpoints expected per lap.
	/// </summary>
	[UnityEngine.Tooltip("Number of checkpoints expected per lap.")]
	public int CheckpointCountPerLap;

	/// <summary>
	/// Number of road tiles recorded for this level.
	/// </summary>
	[UnityEngine.Tooltip("Number of road tiles recorded for this level.")]
	public int RoadTileCount;

	/// <summary>
	/// Whether the level should use the day track scene/variant.
	/// </summary>
	[UnityEngine.Tooltip("Whether the level should use the day track scene or variant.")]
	public bool IsDayTrack;

	/// <summary>
	/// Runtime 2D tile grid.
	/// </summary>
	/// <remarks>
	/// Tile values are represented by <see cref="LevelTileTypes"/>:
	/// <list type="bullet">
	/// <item><description><see cref="LevelTileTypes.CP"/> = checkpoint.</description></item>
	/// <item><description><see cref="LevelTileTypes.Spacer"/> = spacer.</description></item>
	/// <item><description><see cref="LevelTileTypes.Grass"/> = empty/grass.</description></item>
	/// <item><description><see cref="LevelTileTypes.Track"/> = road.</description></item>
	/// <item><description><see cref="LevelTileTypes.PlaceHolder"/> and higher values = temporary placeholders used during generation.</description></item>
	/// </list>
	///
	/// This field is not serialized directly. It is flattened and encoded into <see cref="tilesFlatRLEString"/> 
	/// before serialization and rebuilt from that RLE string after deserialization.
	/// </remarks>
	[NonSerialized] public int[,] Tiles;

	/// <summary>
	/// Integer tile values used by the level grid.
	/// </summary>
	/// <remarks>
	/// The enum defines the named base values stored in <see cref="Tiles"/>. Placeholder values may increase
	/// above <see cref="PlaceHolder"/> during generation.
	/// </remarks>
	public enum LevelTileTypes
	{
		/// <summary>
		/// Checkpoint tile.
		/// </summary>
		CP = -2,

		/// <summary>
		/// Spacer tile.
		/// </summary>
		Spacer = -1,

		/// <summary>
		/// Empty or grass tile.
		/// </summary>
		Grass = 0,

		/// <summary>
		/// Road or track tile.
		/// </summary>
		Track = 1,

		/// <summary>
		/// Temporary placeholder value used by generation algorithms.
		/// </summary>
		PlaceHolder = 2
	}

	/// <summary>
	/// Tile array converted to RLE string used for Unity serialization of <see cref="Tiles"/>.
	/// </summary>
	[UnityEngine.Tooltip("Flattened tile array and compressed with RLE")]
	[UnityEngine.SerializeField] private string tilesFlatRLEString;

	/// <summary>
	/// Creates a default empty level map.
	/// </summary>
	public LevelMap()
	{
		Name = "Unnamed";
		Width = 0;
		Height = 0;
		Circuit = false;
		StartPoint = new Coordinates(0, 0);
		FinishPoint = new Coordinates(0, 0);
		Tiles = new int[0, 0];
		Laps = 3;
		CheckpointCountPerLap = 0;
		RoadTileCount = 0;
		IsDayTrack = true;
	}

	/// <summary>
	/// Deserialization constructor for <see cref="ISerializable"/>.
	/// </summary>
	/// <param name="info">Serialized level data.</param>
	/// <param name="context">Serialization context.</param>
	public LevelMap(SerializationInfo info, StreamingContext context)
	{
		Name = info.GetString("name");
		Width = info.GetInt32("width");
		Height = info.GetInt32("height");
		Circuit = info.GetBoolean("circular");
		StartPoint = (Coordinates)info.GetValue("startPoint", typeof(Coordinates));
		FinishPoint = (Coordinates)info.GetValue("finishPoint", typeof(Coordinates));
		Laps = info.GetInt32("laps");
		CheckpointCountPerLap = info.GetInt32("checkpointCountPerLap");
		RoadTileCount = info.GetInt32("roadTileCount");
		IsDayTrack = info.GetBoolean("isDayTrack");
		tilesFlatRLEString = info.GetString("tilesFlat");
		UnflattenTilesFromRLEString();
	}

	/// <summary>
	/// Writes this level's serializable data into the provided serialization store.
	/// </summary>
	/// <param name="info">Serialization target.</param>
	/// <param name="context">Serialization context.</param>
	/// <remarks>
	/// The runtime <see cref="Tiles"/> grid is flattened, compressed with run-length encoding,
	/// and stored as a string before being added to the serialization data.
	/// </remarks>
	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("name", Name);
		info.AddValue("width", Width);
		info.AddValue("height", Height);
		info.AddValue("circular", Circuit);
		info.AddValue("startPoint", StartPoint);
		info.AddValue("finishPoint", FinishPoint);
		info.AddValue("laps", Laps);
		info.AddValue("checkpointCountPerLap", CheckpointCountPerLap);
		info.AddValue("roadTileCount", RoadTileCount);
		info.AddValue("isDayTrack", IsDayTrack);
		FlattenTilesToRLEString();
		info.AddValue("tilesFlat", tilesFlatRLEString);
	}

	/// <summary>
	/// Creates a flattened copy of the current <see cref="Tiles"/> grid.
	/// </summary>
	/// <returns>
	/// A one-dimensional array containing tile values in row-major order.
	/// </returns>
	public int[] GetFlatTiles()
	{
		if (Tiles == null)
			UnityEngine.Debug.LogError("Tiles is null when trying to flatten. Ensure Tiles is initialized before serialization.");

		int[] flatTiles = new int[Width * Height];
		for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
				flatTiles[y * Width + x] = Tiles[x, y];

		return flatTiles;
	}

	/// <summary>
	/// Flattens <see cref="Tiles"/> and writes the result into <see cref="tilesFlatRLEString"/>
	/// using run-length encoding.
	/// </summary>
	private void FlattenTilesToRLEString()
	{
		if (Tiles == null) return;
		int[] tilesFlat = GetFlatTiles();
		tilesFlatRLEString = ImportExportManager.EncodeFlatTilesRLE(ImportExportManager.ConvertIntArrayToSByteArray(tilesFlat));
	}


	/// <summary>
	/// Rebuilds a 2D tile grid from a flattened tile array.
	/// </summary>
	/// <param name="flatTiles">Flattened tile array in row-major order.</param>
	/// <param name="height">Height of the resulting 2D grid.</param>
	/// <param name="width">Width of the resulting 2D grid.</param>
	/// <returns>Two-dimensional tile grid reconstructed from <paramref name="flatTiles"/>.</returns>
	public static int[,] GetUnflattenedTiles(int[] flatTiles, int height, int width)
	{
		if (width <= 0 || height <= 0 || flatTiles == null)
			UnityEngine.Debug.LogError("Invalid dimensions or tilesFlat is null when trying to unflatten. Ensure Width, Height, and tilesFlat are properly initialized before deserialization.");

		int[,] Tiles = new int[width, height];
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				Tiles[x, y] = flatTiles[y * width + x];

		return Tiles;
	}

	/// <summary>
	/// Decodes <see cref="tilesFlatRLEString"/> and rebuilds the runtime <see cref="Tiles"/> grid.
	/// </summary>
	private void UnflattenTilesFromRLEString()
	{
		if (Width <= 0 || Height <= 0 || tilesFlatRLEString == null) return;

		int[] tilesFlat = new int[Width * Height];
		bool resultCheck = ImportExportManager.TryDecodeFlatTilesRLE(tilesFlatRLEString, Width * Height, out tilesFlat);

		Tiles = GetUnflattenedTiles(tilesFlat, Height, Width);
	}


	/// <summary>
	/// Unity serialization callback that updates the RLE tile string before serialization.
	/// </summary>
	public void OnBeforeSerialize()
	{
		FlattenTilesToRLEString();
	}

	/// <summary>
	/// Unity serialization callback that rebuilds <see cref="Tiles"/> after deserialization.
	/// </summary>
	public void OnAfterDeserialize()
	{
		UnflattenTilesFromRLEString();
	}

	/// <summary>
	/// Creates a copy of this level map.
	/// </summary>
	/// <returns>A new <see cref="LevelMap"/> instance with copied metadata and tile data.</returns>
	public LevelMap Copy()
	{
		LevelMap newMap = new LevelMap();

		newMap.Name = this.Name;
		newMap.Width = this.Width;
		newMap.Height = this.Height;
		newMap.Circuit = this.Circuit;
		newMap.StartPoint = this.StartPoint;
		newMap.FinishPoint = this.FinishPoint;
		newMap.Laps = this.Laps;
		newMap.CheckpointCountPerLap = this.CheckpointCountPerLap;
		newMap.RoadTileCount = this.RoadTileCount;
		newMap.IsDayTrack = this.IsDayTrack;
		newMap.Tiles = this.Tiles.Copy();

		return newMap;
	}

	/// <summary>
	/// Returns the tile grid as a multiline string.
	/// </summary>
	/// <returns>String containing all tile values row by row.</returns>
	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		for (int y = 0; y < Height; ++y)
		{
			for (int x = 0; x < Width; ++x)
			{
				sb.Append(Tiles[x, y]);
			}
			sb.AppendLine();
		}
		return sb.ToString();
	}

	/// <summary>
	/// Checks whether two level maps are equal.
	/// </summary>
	/// <param name="a">First level map.</param>
	/// <param name="b">Second level map.</param>
	/// <returns><c>true</c> when both level maps are equal; otherwise <c>false</c>.</returns>
	public static bool operator ==(LevelMap a, LevelMap b)
	{
		if (ReferenceEquals(a, b))
		{
			return true;
		}

		if (a is null || b is null)
		{
			return false;
		}

		return a.Equals(b);
	}

	/// <summary>
	/// Checks whether two level maps are not equal.
	/// </summary>
	/// <param name="a">First level map.</param>
	/// <param name="b">Second level map.</param>
	/// <returns><c>true</c> when the level maps differ; otherwise <c>false</c>.</returns>
	public static bool operator !=(LevelMap a, LevelMap b)
	{
		return !(a == b);
	}

	/// <summary>
	/// Checks whether this level map has the same data as another object.
	/// </summary>
	/// <param name="obj">Object to compare with this level map.</param>
	/// <returns><c>true</c> when <paramref name="obj"/> is an equal <see cref="LevelMap"/>; otherwise <c>false</c>.</returns>
	public override bool Equals(object obj)
	{
		if (obj is not LevelMap other)
		{
			return false;
		}

		if (Name != other.Name || Width != other.Width || Height != other.Height || Circuit != other.Circuit || StartPoint != other.StartPoint || FinishPoint != other.FinishPoint ||
			Laps != other.Laps || CheckpointCountPerLap != other.CheckpointCountPerLap || RoadTileCount != other.RoadTileCount || IsDayTrack != other.IsDayTrack)
		{
			return false;
		}

		return TilesEqual(Tiles, other.Tiles, Width, Height);
	}

	/// <summary>
	/// Checks whether two tile grids contain the same values for the given dimensions.
	/// </summary>
	/// <param name="first">First tile grid.</param>
	/// <param name="second">Second tile grid.</param>
	/// <param name="width">Width to compare.</param>
	/// <param name="height">Height to compare.</param>
	/// <returns><c>true</c> when both grids match; otherwise <c>false</c>.</returns>
	private static bool TilesEqual(int[,] first, int[,] second, int width, int height)
	{
		if (ReferenceEquals(first, second))
		{
			return true;
		}

		if (first == null || second == null)
		{
			return false;
		}

		if (first.GetLength(0) != second.GetLength(0) ||
			first.GetLength(1) != second.GetLength(1))
		{
			return false;
		}

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (first[x, y] != second[x, y])
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Computes a hash code from level metadata and tile values.
	/// </summary>
	/// <returns>Hash code for this level map.</returns>
	public override int GetHashCode()
	{
		HashCode hash = new HashCode();

		hash.Add(Name);
		hash.Add(Width);
		hash.Add(Height);
		hash.Add(Circuit);
		hash.Add(StartPoint);
		hash.Add(FinishPoint);
		hash.Add(Laps);
		hash.Add(CheckpointCountPerLap);
		hash.Add(RoadTileCount);
		hash.Add(IsDayTrack);

		if (Tiles != null)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					hash.Add(Tiles[x, y]);
				}
			}
		}

		return hash.ToHashCode();
	}
}

#endregion Level Map Class