/// <summary>
/// Utility that scans a generated <see cref="LevelMap"/> road and places checkpoint tiles on long straight segments.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Converts viable road tiles into checkpoint tiles based on straight-segment length.
///
/// Traversal starts at <see cref="LevelMap.StartPoint"/> and follows connected road tiles until no
/// forward step is possible. A checkpoint is placed once per sufficiently long straight segment, roughly
/// at the middle of that straight. Start and finish tiles are never converted to checkpoints.
///
/// Tile values used by this utility come from <see cref="LevelMap.LevelTileTypes"/>:
/// - <see cref="LevelMap.LevelTileTypes.Track"/>: road tile.
/// - <see cref="LevelMap.LevelTileTypes.CP"/>: generated checkpoint tile.
///
/// Threading:
/// - This mutates <see cref="LevelMap.Tiles"/> directly and should be called from code that owns the level data.
/// </remarks>
public static class LevelCheckPointMaker
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
	/// Sentinel coordinate used to indicate an invalid direction or position.
	/// </summary>
	private static readonly Coordinates Invalid = new Coordinates(-1, -1);

	/// <summary>
	/// Walks the road network in <paramref name="levelMap"/> and converts midpoint tiles of sufficiently long
	/// straight segments into checkpoint tiles.
	/// </summary>
	/// <param name="levelMap">Target level definition containing the road grid.</param>
	/// <param name="minStraightLengthForCheckPoint">
	/// Minimum number of consecutive road tiles required before a checkpoint is placed on a straight segment.
	/// </param>
	/// <remarks>
	/// @ingroup level_gen
	///
	/// Preconditions:
	/// - <paramref name="levelMap"/> is non-null.
	/// - <c>levelMap.Tiles</c> is a valid 2D grid.
	/// - Road cells are marked with <see cref="LevelMap.LevelTileTypes.Track"/> and checkpoint cells with <see cref="LevelMap.LevelTileTypes.CP"/>.
	///
	/// Effects:
	/// - Some road cells may be converted to checkpoint cells with <see cref="LevelMap.LevelTileTypes.CP"/>.
	/// - <see cref="LevelMap.CheckpointCountPerLap"/> is incremented for each generated checkpoint.
	/// - Start and finish cells are not modified.
	/// </remarks>
	public static void GenerateCheckPoints(LevelMap levelMap, int minStraightLengthForCheckPoint = 4)
	{
		Coordinates position = levelMap.StartPoint;
		Coordinates lastVisited = levelMap.StartPoint;

		Coordinates direction = Invalid;
		Coordinates straightStart = Invalid;
		int straightLength = 0;

		while (true)
		{
			Coordinates newDirection = Step(levelMap, ref position, ref lastVisited);
			if (newDirection == Invalid)
				break;

			if (direction == Invalid)
			{
				direction = newDirection;
				continue;
			}

			if (newDirection == direction)
			{
				straightLength++;
				continue;
			}

			PlaceCheckpointIfPossible(levelMap, straightStart, direction, straightLength, minStraightLengthForCheckPoint);

			direction = newDirection;
			straightStart = position - direction;
			straightLength = 2;
		}
	}

	/// <summary>
	/// Places a checkpoint on a straight segment when the segment is long enough and the target tile is valid.
	/// </summary>
	/// <param name="levelMap">Level map whose tile grid should be modified.</param>
	/// <param name="straightStart">First coordinate of the straight segment.</param>
	/// <param name="direction">Direction of the straight segment.</param>
	/// <param name="straightLength">Length of the straight segment in tiles.</param>
	/// <param name="minStraightLengthForCheckPoint">Minimum straight length required to place a checkpoint.</param>
	/// <remarks>
	/// The checkpoint is placed at the middle of the straight segment. The method skips invalid starts,
	/// out-of-bounds coordinates, and coordinates equal to the level start or finish point.
	/// </remarks>
	private static void PlaceCheckpointIfPossible(LevelMap levelMap, Coordinates straightStart, Coordinates direction, int straightLength, int minStraightLengthForCheckPoint)
	{
		if (straightLength >= minStraightLengthForCheckPoint && straightStart != Invalid)
		{
			int midOffset = (straightLength - 1) / 2;

			Coordinates checkpoint = straightStart + midOffset * direction;

			if (checkpoint != levelMap.StartPoint && checkpoint != levelMap.FinishPoint && levelMap.Tiles.InBounds(checkpoint.X, checkpoint.Y))
			{
				levelMap.Tiles.At(checkpoint) = CheckpointTile;
				++levelMap.CheckpointCountPerLap;
			}
		}
	}

	/// <summary>
	/// Advances one step along the road from <paramref name="position"/> to a valid adjacent road tile.
	/// </summary>
	/// <param name="levelMap">Level map containing the road grid.</param>
	/// <param name="position">Current traversal position; updated when a valid step is found.</param>
	/// <param name="lastVisited">Previous traversal position used to avoid immediate backtracking; updated on success.</param>
	/// <returns>
	/// Direction offset used for the step if a move was made; otherwise <see cref="Invalid"/> when no forward step is possible.
	/// </returns>
	/// <remarks>
	/// The method checks the cardinal directions from <see cref="LevelMap.CardinalDirections"/>.
	/// It only steps onto tiles marked with <see cref="LevelMap.LevelTileTypes.Track"/> or <see cref="LevelMap.LevelTileTypes.CP"/>, avoids the immediately previous tile,
	/// and does not step onto <see cref="LevelMap.FinishPoint"/>.
	/// </remarks>
	private static Coordinates Step(LevelMap levelMap, ref Coordinates position, ref Coordinates lastVisited)
	{
		foreach (var offset in LevelMap.CardinalDirections)
		{
			var next = position + offset;

			if (!levelMap.Tiles.InBounds(next.X, next.Y))
				continue;

			if ((levelMap.Tiles[next.X, next.Y] == TrackTile || levelMap.Tiles[next.X, next.Y] == CheckpointTile) && next != lastVisited && next != levelMap.FinishPoint)
			{
				lastVisited = position;
				position = next;
				return offset;
			}
		}

		return Invalid;
	}
}