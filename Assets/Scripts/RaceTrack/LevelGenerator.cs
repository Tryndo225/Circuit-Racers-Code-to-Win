using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization;

public static class SeedFactory
{
    private static int _seed = Environment.TickCount;

    public static int Next() => Interlocked.Add(ref _seed, unchecked((int)0x9E3779B9));
}

[Serializable]
public class LevelMap : ISerializable, UnityEngine.ISerializationCallbackReceiver
{
    public string Name;
    public int Width;
    public int Height;
    public bool Circular;
    public Coordinates StartPoint;
    public Coordinates FinishPoint;
    [NonSerialized] public int[,] Tiles; // -2 = checkpoint, -1 = spacer, 0 = grass, 1 = road, 2 and up = placeholder during generation
    [UnityEngine.SerializeField] private int[] tilesFlat;

    public LevelMap()
    {
        Name = "Unnamed";
        Width = 0;
        Height = 0;
        Circular = false;
        StartPoint = new Coordinates(0, 0);
        FinishPoint = new Coordinates(0, 0);
        Tiles = new int[0, 0];
    }

    public LevelMap(SerializationInfo info, StreamingContext context)
    {
        Name = info.GetString("name");
        Width = info.GetInt32("width");
        Height = info.GetInt32("height");
        Circular = info.GetBoolean("circular");
        StartPoint = (Coordinates)info.GetValue("startPoint", typeof(Coordinates));
        FinishPoint = (Coordinates)info.GetValue("finishPoint", typeof(Coordinates));
        tilesFlat = (int[])info.GetValue("tilesFlat", typeof(int[]));
        UnflattenTiles();
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("name", Name);
        info.AddValue("width", Width);
        info.AddValue("height", Height);
        info.AddValue("circular", Circular);
        info.AddValue("startPoint", StartPoint);
        info.AddValue("finishPoint", FinishPoint);
        FlattenTiles();
        info.AddValue("tilesFlat", tilesFlat);
    }

    private void FlattenTiles()
    {
        if (Tiles == null) return;
        tilesFlat = new int[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                tilesFlat[y * Width + x] = Tiles[x, y];
    }

    private void UnflattenTiles()
    {
        if (Width <= 0 || Height <= 0 || tilesFlat == null) return;
        Tiles = new int[Width, Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                Tiles[x, y] = tilesFlat[y * Width + x];
    }

    public void OnBeforeSerialize()
    {
        FlattenTiles();
    }

    public void OnAfterDeserialize()
    {
        UnflattenTiles();
    }
}

[Serializable]
public struct Coordinates : IEquatable<Coordinates>, ISerializable
{
    public int X;
    public int Y;

    public Coordinates(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public Coordinates(SerializationInfo info, StreamingContext context)
    {
        X = info.GetInt32("x");
        Y = info.GetInt32("y");
    }

    public static Coordinates operator +(Coordinates a, Coordinates b)
    { return new Coordinates(a.X + b.X, a.Y + b.Y); }

    public static Coordinates operator -(Coordinates a, Coordinates b)
    { return new Coordinates(a.X - b.X, a.Y - b.Y); }

    public static Coordinates operator *(Coordinates a, int b)
    { return new Coordinates(a.X * b, a.Y * b); }

    public static Coordinates operator *(int a, Coordinates b)
    { return new Coordinates(a * b.X, a * b.Y); }

    public bool Equals(Coordinates o)
    { return X == o.X && Y == o.Y; }

    public override bool Equals(object obj)
    { return obj is Coordinates o && Equals(o); }

    public override int GetHashCode()
    { return HashCode.Combine(X, Y); }

    public static bool operator ==(Coordinates a, Coordinates b)
    { return a.Equals(b); }

    public static bool operator !=(Coordinates a, Coordinates b)
    { return !a.Equals(b); }

    public override string ToString()
    { return $"({X}, {Y})"; }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("x", X);
        info.AddValue("y", Y);
    }
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
        levelMap.StartPoint = new Coordinates(width / 2, height / 2);
        levelMap.Width = width;
        levelMap.Height = height;
        levelMap.Circular = isCircuit;
        levelMap.Tiles = new int[width, height];

        for (int x = 0; x < width; ++x)
        {
            for (int y = 0; y < height; ++y)
            {
                levelMap.Tiles[x, y] = 0; // Initialize all tiles as empty
            }
        }

        levelMap.Tiles.At(levelMap.StartPoint) = 1; // Set start point as track
        var lastValidPoint = levelMap.StartPoint;

        if (isCircuit)
        {
            lastValidPoint = CircuitStarter(levelMap, rng);
        }

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
            levelMap.FinishPoint = lastValidPoint;
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
        if (FloodingAlgorithm(currentPoint, target, levelMap.Tiles, modifiedPositions))
        {
            BackTrack(target, levelMap.Tiles);
            RemovePlaceholders(levelMap.Tiles, modifiedPositions);
            //UnityEngine.Debug.Log($"Step succeeded to {target}");
            return target;
        }
        else
        {
            RemovePlaceholders(levelMap.Tiles, modifiedPositions);
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
            int newX = lastPoint.X + rng.Next(-_stepLength, _stepLength + 1);
            int newY = lastPoint.Y + rng.Next(-_stepLength, _stepLength + 1);

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

    private bool TargetCheck(Coordinates current, Coordinates target, LevelMap levelMap)
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

        if (levelMap.Circular)
        {
            return FloodingAlgorithm(target, levelMap.FinishPoint, tilesCopy) && check;
        }

        return check;
    }

    #endregion Target Selection

    #region Circuit Starting

    private static Coordinates CircuitStarter(LevelMap levelMap, Random rng)
    {
        var lastValidPoint = levelMap.StartPoint;
        if (rng.Next(0, 2) % 2 == 0)
        {
            levelMap.FinishPoint = new(lastValidPoint.X - 1, lastValidPoint.Y);
            lastValidPoint.X += 1;
        }
        else
        {
            levelMap.FinishPoint = new(lastValidPoint.X, lastValidPoint.Y - 1);
            lastValidPoint.Y += 1;
        }

        levelMap.Tiles.At(levelMap.FinishPoint) = 1; // Set finish point as track
        levelMap.Tiles.At(lastValidPoint) = 1; // Mark start/finish as checkpoint

        RoadSpacer(levelMap.StartPoint, levelMap.Tiles);
        return lastValidPoint;
    }

    #endregion Circuit Starting

    #region Circuit Finishing

    private static void CircuitFinisher(LevelMap levelMap, Coordinates lastPoint)
    {
        UnityEngine.Debug.Log("Finishing Circuit");
        UnityEngine.Debug.Log($"Current map {levelMap.Tiles.Print()}");
        List<Coordinates> modifiedPositions = new List<Coordinates>();
        if (FloodingAlgorithm(lastPoint, levelMap.FinishPoint, levelMap.Tiles, modifiedPositions))
        {
            BackTrack(levelMap.FinishPoint, levelMap.Tiles);
            RemovePlaceholders(levelMap.Tiles, modifiedPositions);
            levelMap.FinishPoint = levelMap.StartPoint;
            UnityEngine.Debug.Log($"Circuit finished at {levelMap.FinishPoint}");
            UnityEngine.Debug.Log($"Final map {levelMap.Tiles.Print()}");
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
                int newX = step.position.X + offset.X;
                int newY = step.position.Y + offset.Y;
                if (tiles.InBounds(newX, newY))
                {
                    if (newX == target.X && newY == target.Y)
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
                int newX = backtrackPoint.X + offset.X;
                int newY = backtrackPoint.Y + offset.Y;
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
            int newX = tile.X + offset.X;
            int newY = tile.Y + offset.Y;
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
                int newX = tile.X + offset.X;
                int newY = tile.Y + offset.Y;
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
    public static ref T At<T>(this T[,] array, Coordinates coords)
    {
        return ref array[coords.X, coords.Y];
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

    public static bool InBounds<T>(this T[,] array, int x, int y)
    {
        return x >= 0 && x < array.GetLength(0) &&
               y >= 0 && y < array.GetLength(1);
    }

    public static T[,] Copy<T>(this T[,] array)
    {
        T[,] newArray = new T[array.GetLength(0), array.GetLength(1)];
        Array.Copy(array, newArray, array.Length);
        return newArray;
    }

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