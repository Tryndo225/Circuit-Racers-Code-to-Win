using System;

/// <summary>
/// Utility that scans a generated <see cref="LevelMap"/> road (tile value 1) and stamps
/// checkpoint tiles (-2) along sufficiently long straight segments.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// Traversal starts at <see cref="LevelMap.StartPoint"/> and follows the road until no
/// forward step is possible. A checkpoint is placed once per straight segment whose
/// length meets <paramref name="minstraightLengthForCheckPoint"/>; the checkpoint is
/// positioned roughly at the middle of that straight. Start/finish tiles are never
/// converted to checkpoints.
/// @thread Unity main thread (mutates <see cref="LevelMap.Tiles"/>).
/// </remarks>
public static class LevelCheckPointMaker
{
    /// <summary>
    /// Sentinel coordinate used to indicate an invalid direction/position.
    /// </summary>
    private static readonly Coordinates Invalid = new Coordinates(-1, -1);

    /// <summary>
    /// 4-neighbor step offsets (right, left, up, down) for road traversal.
    /// </summary>
    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };

    /// <summary>
    /// Walks the road network in <paramref name="levelMap"/> and converts the midpoint of each
    /// sufficiently long straight segment into a checkpoint tile (-2).
    /// </summary>
    /// <param name="levelMap">Target level definition containing the road grid.</param>
    /// <param name="minStraightLengthForCheckPoint">
    /// Minimum number of consecutive road tiles (including the first) required
    /// before a checkpoint is placed on that straight segment. (Typo preserved.)
    /// </param>
    /// <remarks>
    /// @ingroup level_gen
    /// Preconditions:
    /// <list type="bullet">
    /// <item><description><paramref name="levelMap"/> is non-null.</description></item>
    /// <item><description><c>levelMap.Tiles</c> is a valid 2D grid with road cells marked as 1.</description></item>
    /// </list>
    /// Postconditions:
    /// <list type="bullet">
    /// <item><description>Some road cells (value 1) may be converted to checkpoints (value -2).</description></item>
    /// <item><description>Start/finish cells are not modified.</description></item>
    /// </list>
    /// Complexity: O(N) over visited road tiles (single pass with 4-neighborhood checks).
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

    private static void PlaceCheckpointIfPossible(LevelMap levelMap, Coordinates straightStart, Coordinates direction, int straightLength, int minStraightLengthForCheckPoint)
    {
        if (straightLength >= minStraightLengthForCheckPoint && straightStart != Invalid)
        {
            int midOffset = (straightLength - 1) / 2;

            Coordinates checkpoint = straightStart + midOffset * direction;

            if (checkpoint != levelMap.StartPoint && checkpoint != levelMap.FinishPoint && levelMap.Tiles.InBounds(checkpoint.X, checkpoint.Y))
            {
                levelMap.Tiles.At(checkpoint) = -2;
            }
        }
    }

    /// <summary>
    /// Advances one step along the road from <paramref name="position"/> to an adjacent
    /// road tile (value 1), avoiding immediate backtracking and skipping the finish tile.
    /// </summary>
    /// <param name="levelMap">Level map containing the road grid.</param>
    /// <param name="position">Current traversal position; updated on success.</param>
    /// <param name="lastVisited">Previous position used to prevent backtracking; updated on success.</param>
    /// <returns>
    /// The offset (direction) used for the step if a move was made; otherwise
    /// <see cref="Invalid"/> when no forward step is possible.
    /// </returns>
    private static Coordinates Step(LevelMap levelMap, ref Coordinates position, ref Coordinates lastVisited)
    {
        foreach (var offset in offsets)
        {
            var next = position + offset;

            if (!levelMap.Tiles.InBounds(next.X, next.Y))
                continue;

            if (levelMap.Tiles[next.X, next.Y] == 1 && next != lastVisited && next != levelMap.FinishPoint)
            {
                lastVisited = position;
                position = next;
                return offset;
            }
        }

        return Invalid;
    }
}