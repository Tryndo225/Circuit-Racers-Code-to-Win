using System.Collections.Generic;
using static LevelMap;

public static class LevelMapValidator
{
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

	private static bool IsTrackTile(int tile)
	{
		return tile == (int)LevelTileTypes.Track ||
			   tile == (int)LevelTileTypes.CP;
	}

	private static bool IsCheckpointTile(LevelMap lvl, int x, int y)
	{
		return lvl.Tiles[x, y] == (int)LevelTileTypes.CP;
	}

	private static bool IsStartTile(LevelMap lvl, int x, int y)
	{
		return lvl.StartPoint.X == x && lvl.StartPoint.Y == y;
	}

	private static bool IsFinishTile(LevelMap lvl, int x, int y)
	{
		return lvl.FinishPoint.X == x && lvl.FinishPoint.Y == y;
	}

	private static bool IsInsideMap(LevelMap lvl, int x, int y)
	{
		return x >= 0 && x < lvl.Width && y >= 0 && y < lvl.Height;
	}

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

	private static bool IsTrackTileAt(LevelMap lvl, int x, int y)
	{
		if (!IsInsideMap(lvl, x, y))
			return false;

		return IsTrackTile(lvl.Tiles[x, y]);
	}

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