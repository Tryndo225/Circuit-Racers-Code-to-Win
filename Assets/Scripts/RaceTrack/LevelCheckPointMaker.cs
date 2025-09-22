using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;

public class LevelCheckPointMaker
{
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
        Coordinates lastDirection = new Coordinates(0, 0);
        Coordinates direction = new Coordinates(0, 0);

        int straightCount = 0;
        bool turnedSinceLastCheckPoint = false;

        while (lastVisited != new Coordinates(-1, -1))
        {
            lastVisited = position;
            lastDirection = direction;
            direction = Step(levelMap, ref position, lastVisited);

            if (direction == lastDirection)
            {
                straightCount++;
                continue;
            }
            else
            {
                straightCount = 0;
                turnedSinceLastCheckPoint = true;
                lastDirection = direction;
            }

            if (straightCount >= minStraightCountForChackPoint && turnedSinceLastCheckPoint)
            {
                levelMap.Tiles.At(lastVisited) = -2;
                turnedSinceLastCheckPoint = false;
            }
        }
    }

    private static Coordinates Step(LevelMap levelMap, ref Coordinates position, Coordinates lastVisited)
    {
        foreach (var offset in offsets)
        {
            var next = position + offset;

            if (levelMap.Tiles[next.X, next.Y] == 1 && lastVisited != next && next != levelMap.FinishPoint)
            {
                position = next;
                return offset;
            }
        }

        return new Coordinates(-1, -1);
    }
}