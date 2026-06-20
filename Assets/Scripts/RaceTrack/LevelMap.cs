#region Level Map Class

using System;
using System.Runtime.Serialization;
using System.Text;

/// <summary>
/// Serializable level definition for a grid-based track: dimensions, circuit flag, endpoints,
/// and a 2D tile map with Unity-friendly flatten/unflatten for serialization.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// Tile codes:
/// -2 = checkpoint, -1 = spacer, 0 = empty/grass, 1 = road, 2+ = BFS placeholders during generation.
/// @invariant <see cref="Tiles"/> size equals <see cref="Width"/> × <see cref="Height"/> when present.
/// @thread Unity main thread for Unity serialization callbacks.
/// </remarks>
[Serializable]
public class LevelMap : ISerializable, UnityEngine.ISerializationCallbackReceiver
{
	/// <summary>
	/// 4-neighbor step offsets (right, left, up, down) for road traversal.
	/// </summary>
	public static readonly Coordinates[] CardinalDirections = new Coordinates[]
	{
		new Coordinates(1, 0),
		new Coordinates(-1, 0),
		new Coordinates(0, 1),
		new Coordinates(0, -1)
	};

	/// <summary>Display name for the level.</summary>
	public string Name;

	/// <summary>Grid width in tiles.</summary>
	public int Width;

	/// <summary>Grid height in tiles.</summary>
	public int Height;

	/// <summary>True for looped circuits; false for point-to-point tracks.</summary>
	public bool Circuit;

	/// <summary>Start cell coordinates.</summary>
	public Coordinates StartPoint;

	/// <summary>Finish cell coordinates (equals <see cref="StartPoint"/> for circuits).</summary>
	public Coordinates FinishPoint;

	public int Laps;

	public int CheckpointCountPerLap;

	public int RoadTileCount;

	/// <summary>
	/// 2D tile grid:
	/// <list type="bullet">
	/// <item>-2 = checkpoint</item>
	/// <item>-1 = spacer (reserved)</item>
	/// <item>0 = empty/grass</item>
	/// <item>1 = road</item>
	/// <item>2+ = internal placeholders during BFS generation</item>
	/// </list>
	/// </summary>
	[NonSerialized] public int[,] Tiles; // -2 = checkpoint, -1 = spacer, 0 = grass, 1 = road, 2 and up = placeholder during generation

	public enum LevelTileTypes
	{
		CP = -2,
		Spacer = -1,
		Grass = 0,
		Track = 1,
		PlaceHolder = 2
	}

	/// <summary>Flattened array used for Unity serialization of <see cref="Tiles"/>.</summary>
	[UnityEngine.SerializeField] private int[] tilesFlat;

	/// <summary>
	/// Creates a default empty map.
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
	}

	/// <summary>
	/// Deserialization constructor for <see cref="ISerializable"/>.
	/// </summary>
	public LevelMap(SerializationInfo info, StreamingContext context)
	{
		Name = info.GetString("name");
		Width = info.GetInt32("width");
		Height = info.GetInt32("height");
		Circuit = info.GetBoolean("circular");
		StartPoint = (Coordinates)info.GetValue("startPoint", typeof(Coordinates));
		FinishPoint = (Coordinates)info.GetValue("finishPoint", typeof(Coordinates));
		tilesFlat = (int[])info.GetValue("tilesFlat", typeof(int[]));
		UnflattenTiles();
	}

	/// <summary>
	/// Populates the <paramref name="info"/> store with metadata and a flattened tile array.
	/// </summary>
	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("name", Name);
		info.AddValue("width", Width);
		info.AddValue("height", Height);
		info.AddValue("circular", Circuit);
		info.AddValue("startPoint", StartPoint);
		info.AddValue("finishPoint", FinishPoint);
		FlattenTiles();
		info.AddValue("tilesFlat", tilesFlat);
	}

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
	/// Writes <see cref="Tiles"/> into <see cref="tilesFlat"/> for Unity serialization.
	/// </summary>
	private void FlattenTiles()
	{
		if (Tiles == null) return;
		tilesFlat = GetFlatTiles();
	}


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
	/// Recreates <see cref="Tiles"/> from <see cref="tilesFlat"/> after deserialization.
	/// </summary>
	private void UnflattenTiles()
	{
		if (Width <= 0 || Height <= 0 || tilesFlat == null) return;
		Tiles = GetUnflattenedTiles(tilesFlat, Height, Width);
	}


	/// <summary>
	/// Unity callback: ensure <see cref="tilesFlat"/> is up to date before serialization.
	/// </summary>
	public void OnBeforeSerialize()
	{
		FlattenTiles();
	}

	/// <summary>
	/// Unity callback: rebuild <see cref="Tiles"/> after deserialization.
	/// </summary>
	public void OnAfterDeserialize()
	{
		UnflattenTiles();
	}

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
		newMap.Tiles = this.Tiles.Copy();

		return newMap;
	}

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
}

#endregion Level Map Class
