using System.Collections.Generic;
using static LevelMap;

public static class LevelMapValidator
{
	public static bool Validate(LevelMap lvl)
	{
		if (lvl == null || lvl.Tiles == null)
		{
			return false;
		}

		if (!lvl.Circuit)
		{
			return false;
		}

		if (lvl.Width <= 0 || lvl.Height <= 0)
		{
			return false;
		}

		if (lvl.Tiles.GetLength(0) != lvl.Width ||
			lvl.Tiles.GetLength(1) != lvl.Height)
		{
			return false;
		}

		int trackTileCount = 0;
		int startX = -1;
		int startY = -1;

		for (int y = 0; y < lvl.Height; ++y)
		{
			for (int x = 0; x < lvl.Width; ++x)
			{
				if (!IsTrackTile(lvl.Tiles[x, y]))
				{
					continue;
				}

				++trackTileCount;

				if (startX == -1)
				{
					startX = x;
					startY = y;
				}

				if (CountTrackNeighbours(lvl, x, y) != 2)
				{
					return false;
				}
			}
		}

		if (trackTileCount == 0)
		{
			return false;
		}

		int connectedTrackTileCount = CountConnectedTrackTiles(lvl, startX, startY);

		return connectedTrackTileCount == trackTileCount;
	}

	private static bool IsTrackTile(int tile)
	{
		return tile == (int)LevelTileTypes.Track ||
			tile == (int)LevelTileTypes.CP;
	}

	private static int CountTrackNeighbours(LevelMap lvl, int x, int y)
	{
		int count = 0;

		foreach (Coordinates direction in CardinalDirections)
		{
			int neighbourX = x + direction.X;
			int neighbourY = y + direction.Y;

			if (IsTrackTileAt(lvl, neighbourX, neighbourY))
			{
				++count;
			}
		}

		return count;
	}

	private static bool IsTrackTileAt(LevelMap lvl, int x, int y)
	{
		if (x < 0 || x >= lvl.Width ||
			y < 0 || y >= lvl.Height)
		{
			return false;
		}

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
		if (x < 0 || x >= lvl.Width ||
			y < 0 || y >= lvl.Height)
		{
			return;
		}

		if (visited[x, y])
		{
			return;
		}

		if (!IsTrackTile(lvl.Tiles[x, y]))
		{
			return;
		}

		visited[x, y] = true;
		queue.Enqueue(y * lvl.Width + x);
	}
}
