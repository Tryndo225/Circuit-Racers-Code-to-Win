using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

#region Seed Generation

/// <summary>
/// Thread-safe seed provider for initializing <see cref="Random"/> instances without repeated seed values.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Produces integer seeds by atomically advancing an internal seed value.
///
/// This utility is used when code needs a fresh seed for procedural level generation.
/// It uses <see cref="Interlocked"/> so multiple callers can request seeds without updating
/// the shared seed value at the same time.
/// </remarks>
public static class SeedFactory
{
	/// <summary>
	/// Internal seed state initialized from <see cref="Environment.TickCount"/>.
	/// </summary>
	private static int _seed = Environment.TickCount;

	/// <summary>
	/// Returns the next integer seed and advances the internal seed state atomically.
	/// </summary>
	/// <returns>New integer seed value.</returns>
	public static int Next() => Interlocked.Add(ref _seed, unchecked((int)0x9E3779B9));
}

#endregion Seed Generation

#region Helper Classes

/// <summary>
/// Integer coordinate pair with arithmetic, equality, and serialization support.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Represents a tile-grid coordinate used by level generation and validation code.
///
/// Value equality is based on <see cref="X"/> and <see cref="Y"/>.
/// </remarks>
[Serializable]
public struct Coordinates : IEquatable<Coordinates>, ISerializable
{
	/// <summary>
	/// X coordinate, usually representing the column index in a tile grid.
	/// </summary>
	public int X;

	/// <summary>
	/// Y coordinate, usually representing the row index in a tile grid.
	/// </summary>
	public int Y;

	/// <summary>
	/// Constructs coordinates from integer components.
	/// </summary>
	/// <param name="x">X coordinate.</param>
	/// <param name="y">Y coordinate.</param>
	public Coordinates(int x, int y)
	{
		this.X = x;
		this.Y = y;
	}

	/// <summary>
	/// Deserialization constructor for <see cref="ISerializable"/>.
	/// </summary>
	/// <param name="info">Serialized coordinate data.</param>
	/// <param name="context">Serialization context.</param>
	public Coordinates(SerializationInfo info, StreamingContext context)
	{
		X = info.GetInt32("x");
		Y = info.GetInt32("y");
	}

	/// <summary>
	/// Adds two coordinate vectors component-wise.
	/// </summary>
	/// <param name="a">First coordinate.</param>
	/// <param name="b">Second coordinate.</param>
	/// <returns>Component-wise sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
	public static Coordinates operator +(Coordinates a, Coordinates b)
	{ return new Coordinates(a.X + b.X, a.Y + b.Y); }

	/// <summary>
	/// Subtracts two coordinate vectors component-wise.
	/// </summary>
	/// <param name="a">Coordinate to subtract from.</param>
	/// <param name="b">Coordinate to subtract.</param>
	/// <returns>Component-wise difference of <paramref name="a"/> and <paramref name="b"/>.</returns>
	public static Coordinates operator -(Coordinates a, Coordinates b)
	{ return new Coordinates(a.X - b.X, a.Y - b.Y); }

	/// <summary>
	/// Multiplies a coordinate by an integer scalar.
	/// </summary>
	/// <param name="a">Coordinate to scale.</param>
	/// <param name="b">Integer scale factor.</param>
	/// <returns>Scaled coordinate.</returns>
	public static Coordinates operator *(Coordinates a, int b)
	{ return new Coordinates(a.X * b, a.Y * b); }

	/// <summary>
	/// Multiplies a coordinate by an integer scalar.
	/// </summary>
	/// <param name="a">Integer scale factor.</param>
	/// <param name="b">Coordinate to scale.</param>
	/// <returns>Scaled coordinate.</returns>
	public static Coordinates operator *(int a, Coordinates b)
	{ return new Coordinates(a * b.X, a * b.Y); }

	/// <summary>
	/// Checks whether this coordinate has the same component values as another coordinate.
	/// </summary>
	/// <param name="o">Coordinate to compare with.</param>
	/// <returns><c>true</c> when both coordinates have equal X and Y values; otherwise <c>false</c>.</returns>
	public bool Equals(Coordinates o)
	{ return X == o.X && Y == o.Y; }

	/// <inheritdoc />
	public override bool Equals(object obj)
	{ return obj is Coordinates o && Equals(o); }

	/// <inheritdoc />
	public override int GetHashCode()
	{ return HashCode.Combine(X, Y); }

	/// <summary>
	/// Checks whether two coordinates are equal.
	/// </summary>
	/// <param name="a">First coordinate.</param>
	/// <param name="b">Second coordinate.</param>
	/// <returns><c>true</c> when both coordinates are equal; otherwise <c>false</c>.</returns>
	public static bool operator ==(Coordinates a, Coordinates b)
	{ return a.Equals(b); }

	/// <summary>
	/// Checks whether two coordinates are not equal.
	/// </summary>
	/// <param name="a">First coordinate.</param>
	/// <param name="b">Second coordinate.</param>
	/// <returns><c>true</c> when the coordinates differ; otherwise <c>false</c>.</returns>
	public static bool operator !=(Coordinates a, Coordinates b)
	{ return !a.Equals(b); }

	/// <summary>
	/// Returns a readable string representation of the coordinate.
	/// </summary>
	/// <returns>Coordinate formatted as <c>(X, Y)</c>.</returns>
	public override string ToString()
	{ return $"({X}, {Y})"; }

	/// <summary>
	/// Writes coordinate data for <see cref="ISerializable"/>.
	/// </summary>
	/// <param name="info">Serialization target.</param>
	/// <param name="context">Serialization context.</param>
	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("x", X);
		info.AddValue("y", Y);
	}
}

#endregion Helper Classes

/// <summary>
/// Procedural level generator for circuit and point-to-point tracks.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Produces <see cref="LevelMap"/> instances by carving connected roads through a tile grid.
///
/// The generator starts from a random start position, repeatedly chooses target points, uses a flood-fill
/// search to find paths, backtracks those paths into road tiles, and applies spacer tiles around roads
/// to keep generated roads separated.
///
/// Tile values used by the generator come from <see cref="LevelMap.LevelTileTypes"/>:
/// - <see cref="LevelMap.LevelTileTypes.Grass"/>: empty tile.
/// - <see cref="LevelMap.LevelTileTypes.Track"/>: road tile.
/// - <see cref="LevelMap.LevelTileTypes.Spacer"/>: spacer tile used to keep roads separated.
/// - <see cref="LevelMap.LevelTileTypes.CP"/>: checkpoint tile, reserved for later checkpoint generation.
/// - <see cref="LevelMap.LevelTileTypes.PlaceHolder"/> and higher values: temporary flood-fill placeholders.
///
/// Threading:
/// - The generator is CPU-only and does not use Unity scene objects.
/// - It mutates the <see cref="LevelMap"/> and tile arrays it owns or receives, so callers should not share the same map instance across threads.
/// </remarks>
public static class LevelGenerator
{
	/// <summary>
	/// Integer tile value used for empty/grass cells.
	/// </summary>
	private const int GrassTile = (int)LevelMap.LevelTileTypes.Grass;

	/// <summary>
	/// Integer tile value used for road/track cells.
	/// </summary>
	private const int TrackTile = (int)LevelMap.LevelTileTypes.Track;

	/// <summary>
	/// Integer tile value used for spacer cells.
	/// </summary>
	private const int SpacerTile = (int)LevelMap.LevelTileTypes.Spacer;

	/// <summary>
	/// First integer tile value used for temporary flood-fill placeholders.
	/// </summary>
	private const int PlaceholderTile = (int)LevelMap.LevelTileTypes.PlaceHolder;

	/// <summary>
	/// Four-neighborhood offsets: right, left, up, and down.
	/// </summary>
	private static readonly Coordinates[] offsets = new Coordinates[]
	{
		new Coordinates(1, 0),
		new Coordinates(-1, 0),
		new Coordinates(0, 1),
		new Coordinates(0, -1)
	};

	/// <summary>
	/// Padding used when selecting the random start position so the start is not placed directly on the border.
	/// </summary>
	private const int startPadding = 2;

	/// <summary>
	/// Generates a level using an explicit integer seed.
	/// </summary>
	/// <param name="width">Grid width in tiles.</param>
	/// <param name="height">Grid height in tiles.</param>
	/// <param name="isCircuit">Whether the generated track should be a closed circuit.</param>
	/// <param name="steps">Number of target-selection/carving iterations to attempt.</param>
	/// <param name="stepLength">Maximum coordinate offset used when attempting to pick a target.</param>
	/// <param name="maxAttempts">Maximum number of target-picking attempts per step.</param>
	/// <param name="seed">Seed used to initialize the random generator.</param>
	/// <returns>A populated <see cref="LevelMap"/>.</returns>
	public static LevelMap GenerateLevel(int width, int height, bool isCircuit, int steps, int stepLength, int maxAttempts, int seed)
	{
		return GenerateLevel(width, height, isCircuit, steps, stepLength, maxAttempts, new Random(seed));
	}

	/// <summary>
	/// Generates a level using a provided <see cref="Random"/> source.
	/// </summary>
	/// <param name="width">Grid width in tiles.</param>
	/// <param name="height">Grid height in tiles.</param>
	/// <param name="isCircuit">Whether the generated track should be a closed circuit.</param>
	/// <param name="steps">Number of target-selection/carving iterations to attempt.</param>
	/// <param name="stepLength">Maximum coordinate offset used when attempting to pick a target.</param>
	/// <param name="maxAttempts">Maximum number of target-picking attempts per step.</param>
	/// <param name="rng">Random generator used for all random choices during generation.</param>
	/// <returns>A populated <see cref="LevelMap"/>.</returns>
	/// <remarks>
	/// If the generated road coverage is too small, generation retries recursively using the same random source.
	/// </remarks>
	public static LevelMap GenerateLevel(int width, int height, bool isCircuit, int steps, int stepLength, int maxAttempts, Random rng)
	{

		LevelMap levelMap = new LevelMap();
		levelMap.StartPoint = new Coordinates(rng.Next(0 + startPadding, width - startPadding), rng.Next(0 + startPadding, height - startPadding));
		levelMap.Width = width;
		levelMap.Height = height;
		levelMap.Circuit = isCircuit;
		levelMap.Tiles = new int[width, height];
		levelMap.IsDayTrack = rng.Next(0, 2) == 0;

		for (int x = 0; x < width; ++x)
		{
			for (int y = 0; y < height; ++y)
			{
				levelMap.Tiles[x, y] = GrassTile; // Initialize all tiles as empty
			}
		}

		levelMap.Tiles.At(levelMap.StartPoint) = TrackTile; // Set start point as track
		var lastValidPoint = levelMap.StartPoint;
		TrackStarter(levelMap, rng);

		Coordinates possiblePoint;

		for (int i = 0; i < steps; ++i)
		{

			possiblePoint = TryStep(lastValidPoint, levelMap, stepLength, maxAttempts, rng);

			if (possiblePoint == new Coordinates(-1, -1))
			{
				break;
			}

			lastValidPoint = possiblePoint;
		}

		if (isCircuit)
		{
			CircuitFinisher(levelMap, lastValidPoint);
		}
		else
		{
			levelMap.FinishPoint = lastValidPoint;
		}

		if (levelMap.RoadTileCount < levelMap.Height * levelMap.Width / 5)
		{
			return GenerateLevel(width, height, isCircuit, steps, stepLength, maxAttempts, rng);
		}

		return levelMap;
	}

	#region Step

	/// <summary>
	/// Attempts one generation step from the current road end.
	/// </summary>
	/// <param name="currentPoint">Current road end from which the next target should be reached.</param>
	/// <param name="levelMap">Level map being generated.</param>
	/// <param name="stepLength">Maximum coordinate offset used when picking a target.</param>
	/// <param name="maxAttempts">Maximum number of attempts to find a valid target.</param>
	/// <param name="rng">Random generator used for target selection.</param>
	/// <returns>New road end point when successful; otherwise <c>(-1, -1)</c>.</returns>
	/// <remarks>
	/// The step picks a target, runs the flood-fill path search, backtracks the found path into road tiles,
	/// and removes temporary placeholder values afterward.
	/// </remarks>
	private static Coordinates TryStep(Coordinates currentPoint, LevelMap levelMap, int stepLength, int maxAttempts, Random rng)
	{
		var target = PickTarget(currentPoint, levelMap, stepLength, maxAttempts, rng);
		if (target == new Coordinates(-1, -1))
		{
			return new Coordinates(-1, -1);
		}

		List<Coordinates> modifiedPositions = new List<Coordinates>();
		if (FloodingAlgorithm(currentPoint, target, levelMap.Tiles, modifiedPositions))
		{
			levelMap.RoadTileCount += BackTrack(target, levelMap.Tiles);
			RemovePlaceholders(levelMap.Tiles, modifiedPositions);
			return target;
		}
		else
		{
			RemovePlaceholders(levelMap.Tiles, modifiedPositions);
			return new Coordinates(-1, -1);
		}
	}

	#endregion Step

	#region Target Selection

	/// <summary>
	/// Attempts to pick a valid target coordinate near <paramref name="lastPoint"/>.
	/// </summary>
	/// <param name="lastPoint">Current road end used as the center for random target selection.</param>
	/// <param name="levelMap">Level map being generated.</param>
	/// <param name="stepLength">Maximum coordinate offset from <paramref name="lastPoint"/>.</param>
	/// <param name="maxAttempts">Maximum number of attempts to find a valid target.</param>
	/// <param name="rng">Random generator used for target selection.</param>
	/// <returns>A valid target coordinate, or <c>(-1, -1)</c> when no valid target was found.</returns>
	private static Coordinates PickTarget(Coordinates lastPoint, LevelMap levelMap, int stepLength, int maxAttempts, Random rng)
	{
		int count = 0;
		Coordinates target;

		while (count < maxAttempts)
		{
			int newX = lastPoint.X + rng.Next(-stepLength, stepLength + 1);
			int newY = lastPoint.Y + rng.Next(-stepLength, stepLength + 1);

			if (!levelMap.Tiles.InBounds(newX, newY))
			{
				continue;
			}

			target = new Coordinates(newX, newY);

			if (TargetCheck(lastPoint, target, levelMap))
			{
				return target;
			}
			count++;
		}

		return new Coordinates(-1, -1);
	}

	/// <summary>
	/// Checks whether a target coordinate is suitable for the next generated road segment.
	/// </summary>
	/// <param name="current">Current road end.</param>
	/// <param name="target">Candidate target coordinate.</param>
	/// <param name="levelMap">Level map being generated.</param>
	/// <returns><c>true</c> if the target can be used; otherwise <c>false</c>.</returns>
	/// <remarks>
	/// A target is rejected when it overlaps an existing road, is adjacent to existing road tiles,
	/// or cannot be reached by the flood-fill path search. For circuit maps, the temporary result is
	/// also checked for reachability back to the start point.
	/// </remarks>
	private static bool TargetCheck(Coordinates current, Coordinates target, LevelMap levelMap)
	{
		if (levelMap.Tiles.At(target) == TrackTile)
		{
			return false;
		}

		for (int x = -1; x <= 1; ++x)
		{
			for (int y = -1; y <= 1; ++y)
			{
				int checkX = target.X + x;
				int checkY = target.Y + y;
				if (!levelMap.Tiles.InBounds(checkX, checkY))
				{
					continue;
				}
				else if (levelMap.Tiles[checkX, checkY] == TrackTile)
				{
					return false;
				}
			}
		}

		int[,] tilesCopy = levelMap.Tiles.Copy();
		List<Coordinates> modifiedPositions = new List<Coordinates>();
		bool check = FloodingAlgorithm(current, target, tilesCopy, modifiedPositions);
		BackTrack(target, tilesCopy);
		RemovePlaceholders(tilesCopy, modifiedPositions);

		if (levelMap.Circuit)
		{
			return FloodingAlgorithm(target, levelMap.StartPoint, tilesCopy) && check;
		}

		return check;
	}

	#endregion Target Selection

	#region Circuit Starting

	/// <summary>
	/// Prepares the initial road opening around <see cref="LevelMap.StartPoint"/>.
	/// </summary>
	/// <param name="levelMap">Level map being generated.</param>
	/// <param name="rng">Random generator used to choose the opening direction when applicable.</param>
	/// <remarks>
	/// Tiles around the start point are first marked as spacers. The method then opens horizontal or vertical
	/// neighboring tiles so generation can continue away from the start point.
	/// </remarks>
	private static void TrackStarter(LevelMap levelMap, Random rng)
	{
		for (int i = -1; i <= 1; ++i)
		{
			for (int j = -1; j <= 1; ++j)
			{
				int checkX = levelMap.StartPoint.X + i;
				int checkY = levelMap.StartPoint.Y + j;
				if (levelMap.Tiles.InBounds(checkX, checkY))
				{
					levelMap.Tiles[checkX, checkY] = SpacerTile; // Fill the area around start point
				}
			}
		}

		if (rng.Next(0, 2) % 2 == 0 && levelMap.Tiles.InBounds(levelMap.StartPoint.X - 1, levelMap.StartPoint.Y) && levelMap.Tiles.InBounds(levelMap.StartPoint.X + 1, levelMap.StartPoint.Y))
		{
			// Horizontal start
			if (levelMap.Circuit)
			{
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = GrassTile;
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = GrassTile;
			}
			else if (rng.Next(0, 2) % 2 == 0)
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = GrassTile;
			else
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = GrassTile;
		}
		else if (levelMap.Tiles.InBounds(levelMap.StartPoint.X, levelMap.StartPoint.Y - 1) && levelMap.Tiles.InBounds(levelMap.StartPoint.X, levelMap.StartPoint.Y + 1))
		{
			// Vertical start
			if (levelMap.Circuit)
			{
				levelMap.Tiles[levelMap.StartPoint.X, levelMap.StartPoint.Y + 1] = GrassTile;
				levelMap.Tiles[levelMap.StartPoint.X, levelMap.StartPoint.Y - 1] = GrassTile;
			}
			else if (rng.Next(0, 2) % 2 == 0)
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = GrassTile;
			else
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = GrassTile;
		}
		else
		{
			throw new Exception("Circuit starting failed");
		}

	}

	#endregion Circuit Starting

	#region Circuit Finishing

	/// <summary>
	/// Completes a circuit by connecting the last generated road point back to <see cref="LevelMap.StartPoint"/>.
	/// </summary>
	/// <param name="levelMap">Level map being generated.</param>
	/// <param name="lastPoint">Last valid road point generated before circuit finishing.</param>
	/// <remarks>
	/// The method uses the same flood-fill and backtracking process as normal road generation.
	/// On success, <see cref="LevelMap.FinishPoint"/> is set to <see cref="LevelMap.StartPoint"/>.
	/// </remarks>
	private static void CircuitFinisher(LevelMap levelMap, Coordinates lastPoint)
	{
		List<Coordinates> modifiedPositions = new List<Coordinates>();
		if (FloodingAlgorithm(lastPoint, levelMap.StartPoint, levelMap.Tiles, modifiedPositions))
		{
			BackTrack(levelMap.StartPoint, levelMap.Tiles);
			RemovePlaceholders(levelMap.Tiles, modifiedPositions);
			levelMap.FinishPoint = levelMap.StartPoint;
		}
		else
		{
			throw new Exception("Circuit finishing failed");
		}
	}

	#endregion Circuit Finishing

	#region Flooding and Backtracking

	/// <summary>
	/// Internal flood-fill queue item containing a position and its current distance/turn value.
	/// </summary>
	private struct FloodStep
	{
		/// <summary>
		/// Current flood-fill position.
		/// </summary>
		public Coordinates position;

		/// <summary>
		/// Distance/turn value written into the tile grid at this position.
		/// </summary>
		public int turn;

		/// <summary>
		/// Constructs a flood-fill queue item.
		/// </summary>
		/// <param name="position">Current flood-fill position.</param>
		/// <param name="step">Distance/turn value for this position.</param>
		public FloodStep(Coordinates position, int step)
		{
			this.position = position;
			this.turn = step;
		}
	}

	/// <summary>
	/// Runs a breadth-first flood fill from <paramref name="start"/> to <paramref name="target"/>.
	/// </summary>
	/// <param name="start">Starting coordinate.</param>
	/// <param name="target">Target coordinate.</param>
	/// <param name="tiles">Tile grid modified in place with temporary placeholder values.</param>
	/// <param name="modifiedPositions">Optional collection of positions modified during the flood fill.</param>
	/// <returns><c>true</c> if the target was reached; otherwise <c>false</c>.</returns>
	/// <remarks>
	/// Empty cells with value <see cref="LevelMap.LevelTileTypes.Grass"/> can be visited. Visited cells are marked with increasing placeholder
	/// values beginning at <see cref="LevelMap.LevelTileTypes.PlaceHolder"/>. The modified positions list can later be passed to
	/// <see cref="RemovePlaceholders"/> for cleanup.
	/// </remarks>
	private static bool FloodingAlgorithm(Coordinates start, Coordinates target, int[,] tiles, List<Coordinates> modifiedPositions = null)
	{
		FloodStep step = new FloodStep(start, PlaceholderTile);
		FloodStep newStep;

		tiles.At(start) = step.turn;

		Queue<FloodStep> queue = new Queue<FloodStep>();
		modifiedPositions ??= new List<Coordinates>();

		queue.Enqueue(step);
		modifiedPositions.Add(step.position);

		while (queue.Count != 0)
		{
			step = queue.Dequeue();

			foreach (var offset in offsets)
			{
				int newX = step.position.X + offset.X;
				int newY = step.position.Y + offset.Y;
				if (tiles.InBounds(newX, newY))
				{
					if (newX == target.X && newY == target.Y)
					{
						tiles.At(target) = step.turn + 1;
						modifiedPositions?.Add(target);
						return true;
					}
					else if (tiles[newX, newY] == GrassTile)
					{
						tiles[newX, newY] = step.turn + 1;
						newStep = new FloodStep(new Coordinates(newX, newY), step.turn + 1);
						queue.Enqueue(newStep);
						modifiedPositions.Add(newStep.position);
					}
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Converts a temporary flood-fill path into road tiles by walking backward through decreasing placeholder values.
	/// </summary>
	/// <param name="start">Target coordinate from which backtracking begins.</param>
	/// <param name="tiles">Tile grid containing flood-fill placeholder values.</param>
	/// <returns>Number of road tiles placed during backtracking.</returns>
	private static int BackTrack(Coordinates start, int[,] tiles)
	{
		int roadsPlaced = 0;
		var backtrackPoint = start;
		Coordinates newPoint = start;

		var step = tiles.At(start) - 1;
		tiles.At(start) = TrackTile;
		++roadsPlaced;

		while (TrackTile < step)
		{
			foreach (var offset in offsets)
			{
				int newX = backtrackPoint.X + offset.X;
				int newY = backtrackPoint.Y + offset.Y;
				if (tiles.InBounds(newX, newY))
				{
					if (tiles[newX, newY] == step)
					{
						newPoint = new Coordinates(newX, newY);
						tiles.At(newPoint) = TrackTile;
						++roadsPlaced;
						RoadSpacer(backtrackPoint, tiles);
						backtrackPoint = newPoint;
						break;
					}
				}
			}
			step--;
		}
		RoadSpacer(backtrackPoint, tiles);
		return roadsPlaced;
	}

	/// <summary>
	/// Removes temporary flood-fill placeholder values from modified tile positions.
	/// </summary>
	/// <param name="tiles">Tile grid to clean.</param>
	/// <param name="modifiedPositions">Positions that may contain temporary placeholder values.</param>
	private static void RemovePlaceholders(int[,] tiles, List<Coordinates> modifiedPositions)
	{
		foreach (var position in modifiedPositions)
		{
			if (tiles.At(position) >= PlaceholderTile)
			{
				tiles.At(position) = GrassTile;
			}
		}
	}

	#endregion Flooding and Backtracking

	#region Road Spacing

	/// <summary>
	/// Places spacer tiles around a road tile when it has more than one adjacent road neighbor.
	/// </summary>
	/// <param name="tile">Road tile coordinate used as the spacing source.</param>
	/// <param name="tiles">Tile grid to modify.</param>
	/// <remarks>
	/// Spacer tiles use <see cref="LevelMap.LevelTileTypes.Spacer"/>. They are written only over empty cells or temporary placeholder cells.
	/// </remarks>
	private static void RoadSpacer(Coordinates tile, int[,] tiles)
	{
		var count = 0;
		foreach (var offset in offsets)
		{
			int newX = tile.X + offset.X;
			int newY = tile.Y + offset.Y;
			if (tiles.InBounds(newX, newY) && tiles[newX, newY] == TrackTile)
			{
				count++;
			}
		}

		if (count > 1)
		{
			foreach (var offset in offsets)
			{
				int newX = tile.X + offset.X;
				int newY = tile.Y + offset.Y;
				if (tiles.InBounds(newX, newY) && (tiles[newX, newY] >= PlaceholderTile || tiles[newX, newY] == GrassTile))
				{
					tiles[newX, newY] = SpacerTile;
				}
			}
		}
	}

	#endregion Road Spacing
}

#region Extensions

/// <summary>
/// Extension helpers for rectangular 2D arrays used by level generation.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Provides coordinate-based indexing, bounds checks, copying, printing, and max-value lookup.
/// </remarks>
public static class Array2DExtensions
{
	/// <summary>
	/// Returns a by-reference alias to the element at <paramref name="coords"/>.
	/// </summary>
	/// <typeparam name="T">Array element type.</typeparam>
	/// <param name="array">Source rectangular 2D array.</param>
	/// <param name="coords">Coordinate used as <c>[X, Y]</c>.</param>
	/// <returns>Reference to the array element at <paramref name="coords"/>.</returns>
	public static ref T At<T>(this T[,] array, Coordinates coords)
	{
		return ref array[coords.X, coords.Y];
	}

	/// <summary>
	/// Formats a 2D integer array into a simple comma-separated multiline string.
	/// </summary>
	/// <param name="array">Array to format.</param>
	/// <returns>String containing all array values row by row.</returns>
	public static string Print(this int[,] array)
	{
		string s = "";
		for (int y = 0; y < array.GetLength(1); ++y)
		{
			for (int x = 0; x < array.GetLength(0); ++x)
			{
				s += array[x, y].ToString("D2") + ',';
			}
			s += "\n";
		}
		return s;
	}

	/// <summary>
	/// Checks whether an index pair is inside the bounds of a rectangular 2D array.
	/// </summary>
	/// <typeparam name="T">Array element type.</typeparam>
	/// <param name="array">Array whose bounds should be checked.</param>
	/// <param name="x">X index.</param>
	/// <param name="y">Y index.</param>
	/// <returns><c>true</c> when <paramref name="x"/> and <paramref name="y"/> are valid indices; otherwise <c>false</c>.</returns>
	public static bool InBounds<T>(this T[,] array, int x, int y)
	{
		return x >= 0 && x < array.GetLength(0) &&
			   y >= 0 && y < array.GetLength(1);
	}

	/// <summary>
	/// Creates a shallow element copy of a rectangular 2D array.
	/// </summary>
	/// <typeparam name="T">Array element type.</typeparam>
	/// <param name="array">Array to copy.</param>
	/// <returns>New rectangular 2D array containing the same element values.</returns>
	public static T[,] Copy<T>(this T[,] array)
	{
		T[,] newArray = new T[array.GetLength(0), array.GetLength(1)];
		Array.Copy(array, newArray, array.Length);
		return newArray;
	}

	/// <summary>
	/// Finds the maximum element in a rectangular 2D array using the default comparer.
	/// </summary>
	/// <typeparam name="T">Comparable element type.</typeparam>
	/// <param name="array">Array to scan.</param>
	/// <returns>Largest element according to <see cref="IComparable{T}.CompareTo(T)"/>.</returns>
	public static T Max<T>(this T[,] array) where T : IComparable<T>
	{
		T max = array[0, 0];
		for (int x = 0; x < array.GetLength(0); ++x)
		{
			for (int y = 0; y < array.GetLength(1); ++y)
			{
				if (array[x, y].CompareTo(max) > 0)
				{
					max = array[x, y];
				}
			}
		}
		return max;
	}
}

#endregion Extensions