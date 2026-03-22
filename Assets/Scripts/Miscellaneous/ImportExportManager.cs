using System;
using System.Text;
using UnityEngine;

public static class ImportExportManager
{
	public static string Escape(string text)
	{
		if (text == null)
		{
			return string.Empty;
		}

		return text.Replace("\\", "\\\\").Replace("|", "\\|");
	}

	public static string Unescape(string text)
	{
		if (text == null)
		{
			return string.Empty;
		}
		return text.Replace("\\|", "|").Replace("\\\\", "\\");
	}

	public static bool TryDecodeLevel(string encoded, out LevelMap levelMap)
	{
		levelMap = null;
		try
		{
			string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
			string[] parts = decoded.Split('|');

			if (parts.Length != 9)
			{
				UnityEngine.Debug.LogWarning("Invalid level code format.");
				return false;
			}

			LevelMap map = new LevelMap();
			map.Name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
			map.Height = int.Parse(parts[1]);
			map.Width = int.Parse(parts[2]);
			map.Circular = parts[3] == "1";
			map.StartPoint = new Coordinates(
				int.Parse(parts[4]),
				int.Parse(parts[5])
			);
			map.FinishPoint = new Coordinates(
				int.Parse(parts[6]),
				int.Parse(parts[7])
			);
			if (!TryDecodeFlatTilesRLE(parts[8], map.Width * map.Height, out int[] flatTiles))
			{
				UnityEngine.Debug.LogWarning("Failed to decode tile data.");
				return false;
			}
			map.Tiles = LevelMap.GetUnflattenedTiles(flatTiles, map.Height, map.Width);
			levelMap = map;
			return true;
		}
		catch (Exception e)
		{
			UnityEngine.Debug.LogWarning($"Failed to parse level metadata: {e.Message}");
			return false;
		}

	}

	public static string EncodeLevel(LevelMap levelMap)
	{
		StringBuilder sb = new StringBuilder();
		int[] flatTiles = levelMap.GetFlatTiles();
		string tilesRLE = EncodeFlatTilesRLE(ConvertIntArrayToSByteArray(flatTiles));

		sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(levelMap.Name ?? string.Empty)));
		sb.Append('|');
		sb.Append(levelMap.Height);
		sb.Append('|');
		sb.Append(levelMap.Width);
		sb.Append('|');
		sb.Append(levelMap.Circular ? 1 : 0);
		sb.Append('|');
		sb.Append(levelMap.StartPoint.X);
		sb.Append('|');
		sb.Append(levelMap.StartPoint.Y);
		sb.Append('|');
		sb.Append(levelMap.FinishPoint.X);
		sb.Append('|');
		sb.Append(levelMap.FinishPoint.Y);
		sb.Append('|');
		sb.Append(tilesRLE);

		return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
	}

	public static bool TryImportLevelFromString(string levelString)
	{
		if (!string.IsNullOrEmpty(levelString) && TryDecodeLevel(levelString, out LevelMap levelMap))
		{
			GameDataManager.Instance.AddLevel(levelMap);
			return true;
		}
		else
		{
			Debug.LogWarning("Failed to import level from string.");
			return false;
		}
	}

	public static bool TryImportLevelFromClipboard(out LevelMap levelMap)
	{
		levelMap = null;
		string clipboard = GUIUtility.systemCopyBuffer;
		if (string.IsNullOrEmpty(clipboard))
		{
			return false;
		}
		return TryDecodeLevel(clipboard, out levelMap);
	}

	public static void ExportLevelToClipboard(LevelMap levelMap)
	{
		string encoded = EncodeLevel(levelMap);
		GUIUtility.systemCopyBuffer = encoded;
	}

	public static sbyte[] ConvertIntArrayToSByteArray(int[] intArray)
	{
		sbyte[] sbyteArray = new sbyte[intArray.Length];
		for (int i = 0; i < intArray.Length; i++)
		{
			sbyteArray[i] = (sbyte)intArray[i];
		}
		return sbyteArray;
	}

	public static string EncodeFlatTilesRLE(sbyte[] flatTiles)
	{
		if (flatTiles == null || flatTiles.Length == 0)
		{
			return string.Empty;
		}

		StringBuilder sb = new StringBuilder();
		sbyte symbolCounter = 1;
		sbyte lastSymbol = flatTiles[0];

		for (int i = 1; i < flatTiles.Length; i++)
		{
			sbyte symbol = flatTiles[i];

			if (symbol == lastSymbol)
			{
				symbolCounter++;
			}
			else
			{
				sb.Append(symbolCounter);
				sb.Append(':');
				sb.Append(lastSymbol);
				sb.Append(';');

				lastSymbol = symbol;
				symbolCounter = 1;
			}
		}

		sb.Append(symbolCounter);
		sb.Append(':');
		sb.Append(lastSymbol);

		return sb.ToString();
	}

	public static bool TryDecodeFlatTilesRLE(string rle, int expectedLength, out int[] flatTiles)
	{
		flatTiles = null;

		if (string.IsNullOrEmpty(rle))
		{
			return false;
		}

		int[] tempTiles = new int[expectedLength];
		string[] pairs = rle.Split(';');
		int index = 0;
		foreach (string pair in pairs)
		{
			string[] parts = pair.Split(':');
			if (parts.Length != 2)
			{
				UnityEngine.Debug.LogWarning("Invalid RLE format.");
				return false;
			}

			int count = int.Parse(parts[0]);
			int symbol = int.Parse(parts[1]);

			for (int i = 0; i < count; i++)
			{
				if (index >= expectedLength)
				{
					UnityEngine.Debug.LogWarning("Decoded length exceeds expected length.");
					return false;
				}
				tempTiles[index++] = symbol;
			}
		}
		if (index != expectedLength)
		{
			UnityEngine.Debug.LogWarning("Decoded length does not match expected length.");
			return false;
		}

		flatTiles = tempTiles;
		return true;
	}
}
