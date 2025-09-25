using System;

public static class LevelCheckPointMaker
{
    private static readonly Coordinates Invalid = new Coordinates(-1, -1);

    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };

    public static void GenerateCheckPoints(LevelMap levelMap, int minStraightCountForChackPoint = 3)
    {
        Coordinates position = levelMap.StartPoint;
        Coordinates lastVisited = levelMap.StartPoint;

        Coordinates direction = Invalid;
        Coordinates newDirection = Invalid;
        int straightCount = 0;
        Coordinates straightStart = Invalid;
        bool checkpointPlacedForThisStraight = true;

        while (true)
        {
            newDirection = Step(levelMap, ref position, ref lastVisited);

            if (newDirection == Invalid)
                break;

            if (newDirection == direction)
            {
                straightCount++;
            }
            else
            {
                if (direction != Invalid)
                {
                    straightStart = position;
                    straightCount = 1;
                    checkpointPlacedForThisStraight = false;
                }
                direction = newDirection;
            }

            if (!checkpointPlacedForThisStraight && straightCount >= minStraightCountForChackPoint)
            {
                if (straightStart != levelMap.StartPoint && straightStart != levelMap.FinishPoint)
                {
                    levelMap.Tiles.At(straightStart + ((straightCount / 2) - 1) * direction) = -2;
                }
                checkpointPlacedForThisStraight = true;
            }
        }
    }

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