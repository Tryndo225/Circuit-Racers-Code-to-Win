using System;
using System.Threading;
using System.Collections.Generic;

internal static class SeedFactory
{
    private static int _seed = Environment.TickCount;

    public static int Next() => Interlocked.Add(ref _seed, unchecked((int)0x9E3779B9));
}

public class LevelMap
{
    public string name;
    public int width;
    public int height;
    public bool circular;
    public Coordinates startPoint;
    public Coordinates finishPoint;
    public int[,] tiles; // -1 = spacer, 0 = grass, 1 = road
}

public struct Coordinates : IEquatable<Coordinates>
{
    public int x;
    public int y;

    public Coordinates(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Equals(Coordinates o)
    { return x == o.x && y == o.y; }

    public override bool Equals(object obj)
    { return obj is Coordinates o && Equals(o); }

    public override int GetHashCode()
    { return HashCode.Combine(x, y); }

    public static bool operator ==(Coordinates a, Coordinates b)
    { return a.Equals(b); }

    public static bool operator !=(Coordinates a, Coordinates b)
    { return !a.Equals(b); }

    public override string ToString()
    { return $"({x}, {y})"; }
}

public class LevelGenerator
{
    private static readonly Coordinates[] offsets = new Coordinates[]
    {
        new Coordinates(1, 0),
        new Coordinates(-1, 0),
        new Coordinates(0, 1),
        new Coordinates(0, -1)
    };

    private readonly int _steps;
    private readonly int _stepLength;
    private readonly int _maxAttempts;

    public LevelGenerator(int steps, int stepLenght, int maxAttempts)
    {
        _steps = steps;
        _stepLength = stepLenght;
        _maxAttempts = maxAttempts;
    }

    public LevelMap GenerateLevel(int width, int height, bool isCircuit, int seed)
    {
        return GenerateLevel(width, height, isCircuit, new Random(seed));
    }

    public LevelMap GenerateLevel(int width, int height, bool isCircuit, Random rng)
    {
        //UnityEngine.Debug.Log("Generating Level");

        LevelMap levelMap = new LevelMap();
        levelMap.startPoint = new Coordinates(width / 2, height / 2);
        levelMap.width = width;
        levelMap.height = height;
        levelMap.circular = isCircuit;
        levelMap.tiles = new int[width, height];

        for (int x = 0; x < width; ++x)
        {
            for (int y = 0; y < height; ++y)
            {
                levelMap.tiles[x, y] = 0; // Initialize all tiles as empty
            }
        }

        levelMap.tiles.At(levelMap.startPoint) = 1; // Set start point as track

        var lastValidPoint = levelMap.startPoint;
        Coordinates possiblePoint;

        for (int i = 0; i < _steps; ++i)
        {
            //UnityEngine.Debug.Log($"Step {i + 1}/{_steps}");

            possiblePoint = TryStep(lastValidPoint, levelMap, rng);

            if (possiblePoint == new Coordinates(-1, -1))
            {
                break;
            }

            lastValidPoint = possiblePoint;
            //UnityEngine.Debug.Log($"Level progress:");
            //UnityEngine.Debug.Log(levelMap.tiles.Print());
        }

        if (isCircuit)
        {
            CircuitFinisher(levelMap, lastValidPoint);
        }
        else
        {
            levelMap.finishPoint = lastValidPoint;
            //UnityEngine.Debug.Log($"Level finished at {levelMap.finishPoint}");
        }

        //UnityEngine.Debug.Log("Level generation complete");
        return levelMap;
    }

    #region Step

    private Coordinates TryStep(Coordinates currentPoint, LevelMap levelMap, Random rng)
    {
        var target = PickTarget(currentPoint, levelMap, rng);
        if (target == new Coordinates(-1, -1))
        {
            //UnityEngine.Debug.Log("Failed to find a valid target");
            return new Coordinates(-1, -1);
        }

        List<Coordinates> modifiedPositions = new List<Coordinates>();
        if (FloodingAlgorithm(currentPoint, target, levelMap.tiles, modifiedPositions))
        {
            BackTrack(target, levelMap.tiles);
            RemovePlaceholders(levelMap.tiles, modifiedPositions);
            //UnityEngine.Debug.Log($"Step succeeded to {target}");
            return target;
        }
        else
        {
            RemovePlaceholders(levelMap.tiles, modifiedPositions);
            //UnityEngine.Debug.Log("Flooding failed to reach target");
            return new Coordinates(-1, -1);
        }
    }

    #endregion Step

    #region Target Selection

    private Coordinates PickTarget(Coordinates lastPoint, LevelMap levelMap, Random rng)
    {
        int count = 0;
        Coordinates target;

        while (count < _maxAttempts)
        {
            int newX = lastPoint.x + rng.Next(-_stepLength, _stepLength + 1);
            int newY = lastPoint.y + rng.Next(-_stepLength, _stepLength + 1);

            if (!levelMap.tiles.InBounds(newX, newY))
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

    private bool TargetCheck(Coordinates current, Coordinates target, LevelMap levelMap)
    {
        if (levelMap.tiles.At(target) == 1)
        {
            return false;
        }

        for (int x = -1; x <= 1; ++x)
        {
            for (int y = -1; y <= 1; ++y)
            {
                int checkX = target.x + x;
                int checkY = target.y + y;
                if (!levelMap.tiles.InBounds(checkX, checkY))
                {
                    continue;
                }
                else if (levelMap.tiles[checkX, checkY] == 1)
                {
                    return false;
                }
            }
        }

        if (levelMap.circular)
        {
            int[,] tilesCopy = levelMap.tiles.Copy();
            List<Coordinates> modifiedPositions = new List<Coordinates>();
            FloodingAlgorithm(current, target, tilesCopy, modifiedPositions);
            BackTrack(target, tilesCopy);
            RemovePlaceholders(tilesCopy, modifiedPositions);
            return FloodingAlgorithm(target, levelMap.startPoint, tilesCopy);
        }
        return true;
    }

    #endregion Target Selection

    #region Circuit Finishing

    private static void CircuitFinisher(LevelMap levelMap, Coordinates lastPoint)
    {
        List<Coordinates> modifiedPositions = new List<Coordinates>();
        if (FloodingAlgorithm(lastPoint, levelMap.startPoint, levelMap.tiles, modifiedPositions))
        {
            BackTrack(levelMap.startPoint, levelMap.tiles);
            RemovePlaceholders(levelMap.tiles, modifiedPositions);
            levelMap.finishPoint = levelMap.startPoint;
            //UnityEngine.Debug.Log($"Circuit finished at {levelMap.finishPoint}");
        }
        else
        {
            throw new Exception("Circuit finishing failed");
        }
    }

    #endregion Circuit Finishing

    #region Flooding and Backtracking

    private struct FloodStep
    {
        public Coordinates position;
        public int turn;

        public FloodStep(Coordinates position, int step)
        {
            this.position = position;
            this.turn = step;
        }
    }

    private static bool FloodingAlgorithm(Coordinates start, Coordinates target, int[,] tiles, List<Coordinates> modifiedPositions = null)
    {
        FloodStep step = new FloodStep(start, 2);
        FloodStep newStep;

        tiles.At(start) = step.turn;

        Queue<FloodStep> queue = new Queue<FloodStep>();
        modifiedPositions ??= new List<Coordinates>();

        queue.Enqueue(step);
        modifiedPositions.Add(step.position);

        //UnityEngine.Debug.Log($"Starting flooding from {start}, {target}");

        while (queue.Count != 0)
        {
            step = queue.Dequeue();

            foreach (var offset in offsets)
            {
                int newX = step.position.x + offset.x;
                int newY = step.position.y + offset.y;
                if (tiles.InBounds(newX, newY))
                {
                    if (newX == target.x && newY == target.y)
                    {
                        //UnityEngine.Debug.Log($"Flooding reached target at {target}");
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
        //UnityEngine.Debug.Log($"Flooding failed to reach target at {target}");
        return false;
    }

    private static void BackTrack(Coordinates start, int[,] tiles)
    {
        var backtrackPoint = start;
        Coordinates newPoint = start;

        var step = tiles.At(start) - 1;
        tiles.At(start) = 1;

        //UnityEngine.Debug.Log($"Starting backtrack from {start}");
        while (1 < step)
        {
            foreach (var offset in offsets)
            {
                int newX = backtrackPoint.x + offset.x;
                int newY = backtrackPoint.y + offset.y;
                if (tiles.InBounds(newX, newY))
                {
                    if (tiles[newX, newY] == step)
                    {
                        newPoint = new Coordinates(newX, newY);
                        tiles.At(newPoint) = 1;
                        RoadSpacer(backtrackPoint, tiles);
                        backtrackPoint = newPoint;
                        break;
                    }
                }
            }
            step--;
        }
        RoadSpacer(backtrackPoint, tiles);
        //UnityEngine.Debug.Log($"Backtrack ended");
    }

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

    private static void RoadSpacer(Coordinates tile, int[,] tiles)
    {
        var count = 0;
        foreach (var offset in offsets)
        {
            int newX = tile.x + offset.x;
            int newY = tile.y + offset.y;
            if (tiles.InBounds(newX, newY) && tiles[newX, newY] == 1)
            {
                count++;
            }
        }

        //UnityEngine.Debug.Log($"Road tile at {tile} has {count} adjacent road tiles");
        //UnityEngine.Debug.Log(tiles.Print());

        if (count > 1)
        {
            foreach (var offset in offsets)
            {
                int newX = tile.x + offset.x;
                int newY = tile.y + offset.y;
                if (tiles.InBounds(newX, newY) && (tiles[newX, newY] > 1 || tiles[newX, newY] == 0))
                {
                    tiles[newX, newY] = -1;
                }
            }
        }
        //UnityEngine.Debug.Log(tiles.Print());
    }

    #endregion Road Spacing
}

public static class Array2DExtensions
{
    public static ref int At(this int[,] array, Coordinates coords)
    {
        return ref array[coords.x, coords.y];
    }

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

    public static bool InBounds(this int[,] array, int x, int y)
    {
        return x >= 0 && x < array.GetLength(0) &&
               y >= 0 && y < array.GetLength(1);
    }

    public static int[,] Copy(this int[,] array)
    {
        int[,] newArray = new int[array.GetLength(0), array.GetLength(1)];
        Array.Copy(array, newArray, array.Length);
        return newArray;
    }

    public static int Max(this int[,] array)
    {
        int max = int.MinValue;
        for (int x = 0; x < array.GetLength(0); ++x)
        {
            for (int y = 0; y < array.GetLength(1); ++y)
            {
                if (array[x, y] > max)
                {
                    max = array[x, y];
                }
            }
        }
        return max;
    }
}