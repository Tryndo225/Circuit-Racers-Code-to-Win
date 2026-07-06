using System.Collections.Generic;
using static LevelMap;

/// <summary>
/// Validates the structural correctness of a generated <see cref="LevelMap"/>.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Checks map dimensions, start/finish validity, track connectivity, endpoint rules, checkpoint placement, and neighbour counts.
///
/// A valid map must:
/// - Have a non-null tile grid with dimensions matching <see cref="LevelMap.Width"/> and <see cref="LevelMap.Height"/>.
/// - Have start and finish points inside the map.
/// - Have start and finish points placed on track-compatible tiles.
/// - Contain one connected track component.
/// - Follow circuit or point-to-point endpoint rules.
/// - Keep track tiles connected with valid neighbour counts.
/// - Place checkpoints only on straight track sections.
///
/// Track-compatible tiles are:
/// - <see cref="LevelTileTypes.Track"/>
/// - <see cref="LevelTileTypes.CP"/>
/// </remarks>
public static class LevelMapValidator
{
	/// <summary>
	/// Validates a level map.
	/// </summary>
	/// <param name="lvl">Level map to validate.</param>
	/// <returns><c>true</c> if the level map satisfies all validation rules; otherwise <c>false</c>.</returns>
	public static bool Validate(LevelMap lvl)
	{
		if (lvl == null || lvl.Tiles == null)
			return false;

		if (lvl.Width <= 0 || lvl.Height <= 0)
			return false;

		if (lvl.Tiles.GetLength(0) != lvl.Width || lvl.Tiles.GetLength(1) != lvl.Height)
			return false;

		if (!IsInsideMap(lvl, lvl.StartPoint.X, lvl.StartPoint.Y) ||
			!IsInsideMap(lvl, lvl.FinishPoint.X, lvl.FinishPoint.Y))
			return false;

		if (!IsTrackTileAt(lvl, lvl.StartPoint.X, lvl.StartPoint.Y) ||
			!IsTrackTileAt(lvl, lvl.FinishPoint.X, lvl.FinishPoint.Y))
			return false;

		if (lvl.Circuit)
		{
			if (lvl.StartPoint.X != lvl.FinishPoint.X ||
				lvl.StartPoint.Y != lvl.FinishPoint.Y)
				return false;
		}
		else
		{
			if (lvl.StartPoint.X == lvl.FinishPoint.X &&
				lvl.StartPoint.Y == lvl.FinishPoint.Y)
				return false;
		}

		int trackTileCount = 0;
		int connectedStartX = -1;
		int connectedStartY = -1;
		int endpointCount = 0;

		for (int y = 0; y < lvl.Height; ++y)
		{
			for (int x = 0; x < lvl.Width; ++x)
			{
				if (!IsTrackTile(lvl.Tiles[x, y]))
					continue;

				++trackTileCount;

				if (connectedStartX == -1)
				{
					connectedStartX = x;
					connectedStartY = y;
				}

				int neighbours = CountTrackNeighbours(lvl, x, y);

				bool isStart = IsStartTile(lvl, x, y);
				bool isFinish = IsFinishTile(lvl, x, y);
				bool isCheckpoint = IsCheckpointTile(lvl, x, y);

				if (lvl.Circuit)
				{
					if (neighbours != 2)
						return false;

					if (isCheckpoint || isStart || isFinish)
					{
						if (!HasOppositeTrackNeighbours(lvl, x, y))
							return false;
					}
				}
				else
				{
					if (isStart || isFinish)
					{
						if (neighbours != 1)
							return false;

						++endpointCount;
					}
					else
					{
						if (neighbours != 2)
							return false;

						if (isCheckpoint)
						{
							if (!HasOppositeTrackNeighbours(lvl, x, y))
								return false;
						}
					}
				}
			}
		}

		if (trackTileCount == 0)
			return false;

		if (!lvl.Circuit && endpointCount != 2)
			return false;

		int connectedTrackTileCount = CountConnectedTrackTiles(lvl, connectedStartX, connectedStartY);

		return connectedTrackTileCount == trackTileCount;
	}

	/// <summary>
	/// Checks whether a tile value is part of the drivable track.
	/// </summary>
	/// <param name="tile">Tile value to check.</param>
	/// <returns>
	/// <c>true</c> when the tile is <see cref="LevelTileTypes.Track"/> or <see cref="LevelTileTypes.CP"/>;
	/// otherwise <c>false</c>.
	/// </returns>
	private static bool IsTrackTile(int tile)
	{
		return tile == (int)LevelTileTypes.Track ||
			   tile == (int)LevelTileTypes.CP;
	}

	/// <summary>
	/// Checks whether the tile at the given coordinate is a checkpoint tile.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns><c>true</c> if the tile is a checkpoint tile; otherwise <c>false</c>.</returns>
	private static bool IsCheckpointTile(LevelMap lvl, int x, int y)
	{
		return lvl.Tiles[x, y] == (int)LevelTileTypes.CP;
	}

	/// <summary>
	/// Checks whether the given coordinate is the level start point.
	/// </summary>
	/// <param name="lvl">Level map containing the start point.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns><c>true</c> if the coordinate matches <see cref="LevelMap.StartPoint"/>; otherwise <c>false</c>.</returns>
	private static bool IsStartTile(LevelMap lvl, int x, int y)
	{
		return lvl.StartPoint.X == x && lvl.StartPoint.Y == y;
	}

	/// <summary>
	/// Checks whether the given coordinate is the level finish point.
	/// </summary>
	/// <param name="lvl">Level map containing the finish point.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns><c>true</c> if the coordinate matches <see cref="LevelMap.FinishPoint"/>; otherwise <c>false</c>.</returns>
	private static bool IsFinishTile(LevelMap lvl, int x, int y)
	{
		return lvl.FinishPoint.X == x && lvl.FinishPoint.Y == y;
	}

	/// <summary>
	/// Checks whether a coordinate is inside the level map bounds.
	/// </summary>
	/// <param name="lvl">Level map whose bounds should be checked.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns><c>true</c> if the coordinate is inside the map; otherwise <c>false</c>.</returns>
	private static bool IsInsideMap(LevelMap lvl, int x, int y)
	{
		return x >= 0 && x < lvl.Width && y >= 0 && y < lvl.Height;
	}

	/// <summary>
	/// Counts drivable track neighbours around a tile using the four cardinal directions.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns>Number of adjacent track-compatible tiles.</returns>
	private static int CountTrackNeighbours(LevelMap lvl, int x, int y)
	{
		int count = 0;

		foreach (Coordinates direction in CardinalDirections)
		{
			int neighbourX = x + direction.X;
			int neighbourY = y + direction.Y;

			if (IsTrackTileAt(lvl, neighbourX, neighbourY))
				++count;
		}

		return count;
	}

	/// <summary>
	/// Checks whether a tile has two opposite track neighbours, forming a straight segment.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns>
	/// <c>true</c> if the tile has either left-right neighbours or up-down neighbours, without perpendicular neighbours.
	/// Otherwise <c>false</c>.
	/// </returns>
	private static bool HasOppositeTrackNeighbours(LevelMap lvl, int x, int y)
	{
		bool left = IsTrackTileAt(lvl, x - 1, y);
		bool right = IsTrackTileAt(lvl, x + 1, y);
		bool down = IsTrackTileAt(lvl, x, y - 1);
		bool up = IsTrackTileAt(lvl, x, y + 1);

		bool horizontalStraight = left && right && !up && !down;
		bool verticalStraight = up && down && !left && !right;

		return horizontalStraight || verticalStraight;
	}

	/// <summary>
	/// Checks whether a coordinate is inside the map and contains a track-compatible tile.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="x">Tile X coordinate.</param>
	/// <param name="y">Tile Y coordinate.</param>
	/// <returns><c>true</c> if the coordinate is inside the map and contains a track-compatible tile; otherwise <c>false</c>.</returns>
	private static bool IsTrackTileAt(LevelMap lvl, int x, int y)
	{
		if (!IsInsideMap(lvl, x, y))
			return false;

		return IsTrackTile(lvl.Tiles[x, y]);
	}

	/// <summary>
	/// Counts how many track-compatible tiles are connected to the starting coordinate.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="startX">Starting tile X coordinate.</param>
	/// <param name="startY">Starting tile Y coordinate.</param>
	/// <returns>Number of connected track-compatible tiles reachable from the starting coordinate.</returns>
	/// <remarks>
	/// Connectivity is checked with a breadth-first search over four-neighbour movement.
	/// </remarks>
	private static int CountConnectedTrackTiles(LevelMap lvl, int startX, int startY)
	{
		bool[,] visited = new bool[lvl.Width, lvl.Height];
		Queue<int> queue = new Queue<int>();

		visited[startX, startY] = true;
		queue.Enqueue(startY * lvl.Width + startX);

		int count = 0;

		while (queue.Count > 0)
		{
			int encoded = queue.Dequeue();

			int x = encoded % lvl.Width;
			int y = encoded / lvl.Width;

			++count;

			foreach (Coordinates direction in CardinalDirections)
			{
				int neighbourX = x + direction.X;
				int neighbourY = y + direction.Y;

				AddNeighbour(lvl, visited, queue, neighbourX, neighbourY);
			}
		}

		return count;
	}

	/// <summary>
	/// Adds a neighbouring track tile to the connectivity search queue when it is valid and unvisited.
	/// </summary>
	/// <param name="lvl">Level map containing the tile grid.</param>
	/// <param name="visited">Visited tile lookup used by the breadth-first search.</param>
	/// <param name="queue">Queue of encoded tile coordinates to visit.</param>
	/// <param name="x">Neighbour tile X coordinate.</param>
	/// <param name="y">Neighbour tile Y coordinate.</param>
	private static void AddNeighbour(LevelMap lvl, bool[,] visited, Queue<int> queue, int x, int y)
	{
		if (x < 0 || x >= lvl.Width || y < 0 || y >= lvl.Height)
			return;

		if (visited[x, y])
			return;

		if (!IsTrackTile(lvl.Tiles[x, y]))
			return;

		visited[x, y] = true;
		queue.Enqueue(y * lvl.Width + x);
	}
}