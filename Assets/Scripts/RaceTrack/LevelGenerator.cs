using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

#region Seed Generation

/// <summary>
/// Thread-safe seed provider for initializing <see cref="Random"/> instances without collisions.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @thread Thread-safe (uses <see cref="Interlocked"/>).
/// </remarks>
public static class SeedFactory
{
	/// <summary>
	/// Internal seed state, initialized from <see cref="Environment.TickCount"/>.
	/// </summary>
	private static int _seed = Environment.TickCount;

	/// <summary>
	/// Returns the next integer seed, atomically advanced by a large odd constant.
	/// </summary>
	/// <returns>New pseudo-random seed value.</returns>
	public static int Next() => Interlocked.Add(ref _seed, unchecked((int)0x9E3779B9));
}

#endregion Seed Generation

#region Helper Classes

/// <summary>
/// Integer coordinate pair with arithmetic, equality, and serialization support.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @invariant Value equality is based on <see cref="X"/> and <see cref="Y"/>.
/// </remarks>
[Serializable]
public struct Coordinates : IEquatable<Coordinates>, ISerializable
{
	/// <summary>X coordinate (column index).</summary>
	public int X;

	/// <summary>Y coordinate (row index).</summary>
	public int Y;

	/// <summary>
	/// Constructs coordinates (x, y).
	/// </summary>
	public Coordinates(int x, int y)
	{
		this.X = x;
		this.Y = y;
	}

	/// <summary>
	/// Deserialization constructor for <see cref="ISerializable"/>.
	/// </summary>
	public Coordinates(SerializationInfo info, StreamingContext context)
	{
		X = info.GetInt32("x");
		Y = info.GetInt32("y");
	}

	/// <summary>Adds two coordinate vectors component-wise.</summary>
	public static Coordinates operator +(Coordinates a, Coordinates b)
	{ return new Coordinates(a.X + b.X, a.Y + b.Y); }

	/// <summary>Subtracts two coordinate vectors component-wise.</summary>
	public static Coordinates operator -(Coordinates a, Coordinates b)
	{ return new Coordinates(a.X - b.X, a.Y - b.Y); }

	/// <summary>Multiplies a coordinate by a scalar.</summary>
	public static Coordinates operator *(Coordinates a, int b)
	{ return new Coordinates(a.X * b, a.Y * b); }

	/// <summary>Multiplies a coordinate by a scalar.</summary>
	public static Coordinates operator *(int a, Coordinates b)
	{ return new Coordinates(a * b.X, a * b.Y); }

	/// <summary>Value equality on X and Y.</summary>
	public bool Equals(Coordinates o)
	{ return X == o.X && Y == o.Y; }

	/// <inheritdoc />
	public override bool Equals(object obj)
	{ return obj is Coordinates o && Equals(o); }

	/// <inheritdoc />
	public override int GetHashCode()
	{ return HashCode.Combine(X, Y); }

	/// <summary>Equality operator.</summary>
	public static bool operator ==(Coordinates a, Coordinates b)
	{ return a.Equals(b); }

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(Coordinates a, Coordinates b)
	{ return !a.Equals(b); }

	/// <summary>Readable string representation.</summary>
	public override string ToString()
	{ return $"({X}, {Y})"; }

	/// <summary>
	/// Serialization callback for <see cref="ISerializable"/>.
	/// </summary>
	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("x", X);
		info.AddValue("y", Y);
	}
}

#endregion Helper Classes

/// <summary>
/// Procedural level generator (static). Produces circuit or point-to-point tracks using
/// iterative target selection, BFS “flooding”, backtracking to carve roads, and spacing rules.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// Tiles semantics: 1 = road, 0 = empty, -1 = spacer, -2 = checkpoint, 2+ = BFS placeholders.
/// @thread Use on Unity main thread (operates on shared map arrays).
/// </remarks>
public static class LevelGenerator
{
	/// <summary>4-neighborhood offsets: right, left, up, down.</summary>
	private static readonly Coordinates[] offsets = new Coordinates[]
	{
		new Coordinates(1, 0),
		new Coordinates(-1, 0),
		new Coordinates(0, 1),
		new Coordinates(0, -1)
	};

	private const int startPadding = 2;

	/// <summary>
	/// Generates a level using an explicit seed to initialize <see cref="Random"/>.
	/// </summary>
	/// <param name="width">Grid width (tiles).</param>
	/// <param name="height">Grid height (tiles).</param>
	/// <param name="isCircuit">True for a closed loop; false for point-to-point.</param>
	/// <param name="steps">Number of carving iterations.</param>
	/// <param name="stepLenght">Maximum Manhattan distance to attempt per step (typo preserved).</param>
	/// <param name="maxAttempts">Max attempts to find a valid target per step.</param>
	/// <param name="seed">Seed for RNG.</param>
	/// <returns>A populated <see cref="LevelMap"/>.</returns>
	public static LevelMap GenerateLevel(int width, int height, bool isCircuit, int steps, int stepLenght, int maxAttempts, int seed)
	{
		return GenerateLevel(width, height, isCircuit, steps, stepLenght, maxAttempts, new Random(seed));
	}

	/// <summary>
	/// Generates a level using a provided <see cref="Random"/> source.
	/// </summary>
	/// <param name="width">Grid width (tiles).</param>
	/// <param name="height">Grid height (tiles).</param>
	/// <param name="isCircuit">True for a closed loop; false for point-to-point.</param>
	/// <param name="steps">Number of carving iterations.</param>
	/// <param name="stepLenght">Maximum manhattan distance to attempt per step (typo preserved).</param>
	/// <param name="maxAttempts">Max attempts to find a valid target per step.</param>
	/// <param name="rng">Random generator.</param>
	/// <returns>A populated <see cref="LevelMap"/>.</returns>
	public static LevelMap GenerateLevel(int width, int height, bool isCircuit, int steps, int stepLenght, int maxAttempts, Random rng)
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
				levelMap.Tiles[x, y] = 0; // Initialize all tiles as empty
			}
		}

		levelMap.Tiles.At(levelMap.StartPoint) = 1; // Set start point as track
		var lastValidPoint = levelMap.StartPoint;
		TrackStarter(levelMap, rng);

		Coordinates possiblePoint;

		for (int i = 0; i < steps; ++i)
		{

			possiblePoint = TryStep(lastValidPoint, levelMap, stepLenght, maxAttempts, rng);

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
			return GenerateLevel(width, height, isCircuit, steps, stepLenght, maxAttempts, rng);
		}

		return levelMap;
	}

	#region Step

	/// <summary>
	/// Attempts a single carve step: pick target, BFS flood, backtrack to carve, cleanup placeholders.
	/// </summary>
	/// <param name="currentPoint">Current road end.</param>
	/// <param name="levelMap">Target level map.</param>
	/// <param name="stepLength">Maximum step length.</param>
	/// <param name="maxAttempts">Maximum attempts to pick a valid target.</param>
	/// <param name="rng">Random source.</param>
	/// <returns>New end point or (-1,-1) on failure.</returns>
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
	/// Picks a reachable target coordinate near <paramref name="lastPoint"/> within constraints.
	/// </summary>
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
	/// Validates a target: not overlapping existing road, spaced from neighbors, and path-reachable.
	/// For circuits, also checks reachability back to start.
	/// </summary>
	private static bool TargetCheck(Coordinates current, Coordinates target, LevelMap levelMap)
	{
		if (levelMap.Tiles.At(target) == 1)
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
				else if (levelMap.Tiles[checkX, checkY] == 1)
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
	/// Prepares a circuit start area by clearing neighbors around <see cref="LevelMap.StartPoint"/>
	/// and opening one axis to begin the loop.
	/// </summary>
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
					levelMap.Tiles[checkX, checkY] = -1; // Fill the area around start point
				}
			}
		}

		if (rng.Next(0, 2) % 2 == 0 && levelMap.Tiles.InBounds(levelMap.StartPoint.X - 1, levelMap.StartPoint.Y) && levelMap.Tiles.InBounds(levelMap.StartPoint.X + 1, levelMap.StartPoint.Y))
		{
			// Horizontal start
			if (levelMap.Circuit)
			{
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = 0;
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = 0;
			}
			else if (rng.Next(0, 2) % 2 == 0)
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = 0;
			else
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = 0;
		}
		else if (levelMap.Tiles.InBounds(levelMap.StartPoint.X, levelMap.StartPoint.Y - 1) && levelMap.Tiles.InBounds(levelMap.StartPoint.X, levelMap.StartPoint.Y + 1))
		{
			// Vertical start
			if (levelMap.Circuit)
			{
				levelMap.Tiles[levelMap.StartPoint.X, levelMap.StartPoint.Y + 1] = 0;
				levelMap.Tiles[levelMap.StartPoint.X, levelMap.StartPoint.Y - 1] = 0;
			}
			else if (rng.Next(0, 2) % 2 == 0)
				levelMap.Tiles[levelMap.StartPoint.X + 1, levelMap.StartPoint.Y] = 0;
			else
				levelMap.Tiles[levelMap.StartPoint.X - 1, levelMap.StartPoint.Y] = 0;
		}
		else
		{
			throw new Exception("Circuit starting failed");
		}

	}

	#endregion Circuit Starting

	#region Circuit Finishing

	/// <summary>
	/// Completes a circuit by BFS from the last point back to <see cref="LevelMap.StartPoint"/>,
	/// carving the final segment.
	/// </summary>
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
	/// Internal BFS step (position + turn count).
	/// </summary>
	private struct FloodStep
	{
		/// <summary>Current position.</summary>
		public Coordinates position;

		/// <summary>Turn (distance counter) at this position.</summary>
		public int turn;

		/// <summary>Constructs a flood step.</summary>
		public FloodStep(Coordinates position, int step)
		{
			this.position = position;
			this.turn = step;
		}
	}

	/// <summary>
	/// BFS flood from <paramref name="start"/> to <paramref name="target"/> over empty cells (0),
	/// marking visited cells with increasing placeholder values (2+).
	/// </summary>
	/// <param name="start">Starting coordinate.</param>
	/// <param name="target">Target coordinate.</param>
	/// <param name="tiles">Tile grid (modified in-place).</param>
	/// <param name="modifiedPositions">Optional collection of positions modified during flood.</param>
	/// <returns>True if target is reached; otherwise false.</returns>
	private static bool FloodingAlgorithm(Coordinates start, Coordinates target, int[,] tiles, List<Coordinates> modifiedPositions = null)
	{
		FloodStep step = new FloodStep(start, 2);
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
					else if (tiles[newX, newY] == 0)
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
	/// Converts placeholder numbers written by BFS back into road (1) by walking from
	/// <paramref name="start"/> down to the origin along decreasing turn counts.
	/// </summary>
	private static int BackTrack(Coordinates start, int[,] tiles)
	{
		int roadsPlaced = 0;
		var backtrackPoint = start;
		Coordinates newPoint = start;

		var step = tiles.At(start) - 1;
		tiles.At(start) = 1;
		++roadsPlaced;

		while (1 < step)
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
						tiles.At(newPoint) = 1;
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
	/// Removes temporary placeholder values (&gt;1) that were written during flood.
	/// </summary>
	private static void RemovePlaceholders(int[,] tiles, List<Coordinates> modifiedPositions)
	{
		foreach (var position in modifiedPositions)
		{
			if (tiles.At(position) > 1)
			{
				tiles.At(position) = 0;
			}
		}
	}

	#endregion Flooding and Backtracking

	#region Road Spacing

	/// <summary>
	/// Places spacers (-1) around road tiles that have more than one adjacent road neighbor,
	/// to keep branches visually separated.
	/// </summary>
	private static void RoadSpacer(Coordinates tile, int[,] tiles)
	{
		var count = 0;
		foreach (var offset in offsets)
		{
			int newX = tile.X + offset.X;
			int newY = tile.Y + offset.Y;
			if (tiles.InBounds(newX, newY) && tiles[newX, newY] == 1)
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
				if (tiles.InBounds(newX, newY) && (tiles[newX, newY] > 1 || tiles[newX, newY] == 0))
				{
					tiles[newX, newY] = -1;
				}
			}
		}
	}

	#endregion Road Spacing
}

#region Extensions

/// <summary>
/// 2D array helpers used by the level generator (indexing, bounds checks, copying, printing).
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @thread Pure CPU utilities; safe on main thread.
/// </remarks>
public static class Array2DExtensions
{
	/// <summary>
	/// Returns a by-ref alias to <paramref name="array"/> at <paramref name="coords"/> (X,Y).
	/// </summary>
	public static ref T At<T>(this T[,] array, Coordinates coords)
	{
		return ref array[coords.X, coords.Y];
	}

	/// <summary>
	/// Formats a 2D int array into a simple CSV-like string (for debugging).
	/// </summary>
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
	/// True if (x,y) is within the array bounds.
	/// </summary>
	public static bool InBounds<T>(this T[,] array, int x, int y)
	{
		return x >= 0 && x < array.GetLength(0) &&
			   y >= 0 && y < array.GetLength(1);
	}

	/// <summary>
	/// Deep-copies a rectangular 2D array.
	/// </summary>
	public static T[,] Copy<T>(this T[,] array)
	{
		T[,] newArray = new T[array.GetLength(0), array.GetLength(1)];
		Array.Copy(array, newArray, array.Length);
		return newArray;
	}

	/// <summary>
	/// Returns the maximum element in a 2D array using the default comparer.
	/// </summary>
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