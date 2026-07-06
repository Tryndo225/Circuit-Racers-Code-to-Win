using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Utility for encoding, decoding, importing, and exporting <see cref="LevelMap"/> data.
/// </summary>
/// <remarks>
/// @ingroup game_data
/// @brief Converts level maps to compact shareable strings and reconstructs level maps from those strings.
///
/// The exported level format stores level metadata and run-length encoded tile data inside a
/// Base64-encoded string. Clipboard helpers use <see cref="GUIUtility.systemCopyBuffer"/> for simple
/// copy/paste sharing.
///
/// Encoded data contains:
/// - Level name.
/// - Level height and width.
/// - Circuit flag.
/// - Start and finish coordinates.
/// - Lap count.
/// - Checkpoint count per lap.
/// - Road tile count.
/// - Day/night track flag.
/// - Flattened tile grid encoded with run-length encoding.
///
/// Tile values are stored as integers matching <see cref="LevelMap.LevelTileTypes"/>.
/// </remarks>
public static class ImportExportManager
{
	/// <summary>
	/// Number of pipe-separated fields in the exported level format.
	/// </summary>
	private const int PartCount = 13;

	/// <summary>
	/// Escapes backslashes and pipe separators in a text value.
	/// </summary>
	/// <param name="text">Text to escape.</param>
	/// <returns>Escaped text, or an empty string when <paramref name="text"/> is null.</returns>
	public static string Escape(string text)
	{
		if (text == null)
		{
			return string.Empty;
		}

		return text.Replace("\\", "\\\\").Replace("|", "\\|");
	}

	/// <summary>
	/// Reverses escaping produced by <see cref="Escape(string)"/>.
	/// </summary>
	/// <param name="text">Escaped text to unescape.</param>
	/// <returns>Unescaped text, or an empty string when <paramref name="text"/> is null.</returns>
	public static string Unescape(string text)
	{
		if (text == null)
			return string.Empty;

		return text.Replace("\\|", "|").Replace("\\\\", "\\");
	}

	/// <summary>
	/// Attempts to decode a shareable level string into a <see cref="LevelMap"/>.
	/// </summary>
	/// <param name="encoded">Base64-encoded level string.</param>
	/// <param name="levelMap">Decoded level map when decoding succeeds; otherwise <c>null</c>.</param>
	/// <returns><c>true</c> if decoding succeeded; otherwise <c>false</c>.</returns>
	/// <remarks>
	/// The method expects a decoded pipe-separated metadata string with thirteen parts. The final part contains
	/// run-length encoded tile data that is reconstructed into <see cref="LevelMap.Tiles"/>.
	/// </remarks>
	public static bool TryDecodeLevel(string encoded, out LevelMap levelMap)
	{
		levelMap = null;

		try
		{
			string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
			string[] parts = decoded.Split('|');

			if (parts.Length != PartCount)
			{
				Debug.LogWarning("[ImportExportManager] Invalid level code format.");
				return false;
			}

			if (!int.TryParse(parts[1], out int height) || !int.TryParse(parts[2], out int width) || !int.TryParse(parts[4], out int startX) || !int.TryParse(parts[5], out int startY) || !int.TryParse(parts[6], out int finishX) ||
				!int.TryParse(parts[7], out int finishY) || !int.TryParse(parts[8], out int laps) || !int.TryParse(parts[9], out int checkpointCountPerLap) || !int.TryParse(parts[10], out int roadTileCount))
			{
				Debug.LogWarning("[ImportExportManager] Invalid level metadata number format.");
				return false;
			}

			if (width <= 0 || height <= 0)
			{
				Debug.LogWarning("[ImportExportManager] Invalid level dimensions.");
				return false;
			}

			LevelMap map = new LevelMap();
			map.Name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
			map.Height = height;
			map.Width = width;
			map.Circuit = parts[3] == "1";
			map.StartPoint = new Coordinates(startX, startY);
			map.FinishPoint = new Coordinates(finishX, finishY);
			map.Laps = laps;
			map.CheckpointCountPerLap = checkpointCountPerLap;
			map.RoadTileCount = roadTileCount;
			map.IsDayTrack = parts[11] == "1";

			if (!TryDecodeFlatTilesRLE(parts[12], map.Width * map.Height, out int[] flatTiles))
			{
				Debug.LogWarning("[ImportExportManager] Failed to decode tile data.");
				return false;
			}

			map.Tiles = LevelMap.GetUnflattenedTiles(flatTiles, map.Height, map.Width);
			levelMap = map;
			return true;
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[ImportExportManager] Failed to parse level metadata: {e.Message}");
			return false;
		}
	}

	/// <summary>
	/// Encodes a <see cref="LevelMap"/> into a compact shareable string.
	/// </summary>
	/// <param name="levelMap">Level map to encode.</param>
	/// <returns>Base64-encoded level string.</returns>
	/// <remarks>
	/// The tile grid is flattened with <see cref="LevelMap.GetFlatTiles"/>, converted to signed bytes,
	/// encoded with run-length encoding, and then included in the exported metadata string.
	/// </remarks>
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
		sb.Append(levelMap.Circuit ? 1 : 0);
		sb.Append('|');
		sb.Append(levelMap.StartPoint.X);
		sb.Append('|');
		sb.Append(levelMap.StartPoint.Y);
		sb.Append('|');
		sb.Append(levelMap.FinishPoint.X);
		sb.Append('|');
		sb.Append(levelMap.FinishPoint.Y);
		sb.Append('|');
		sb.Append(levelMap.Laps);
		sb.Append('|');
		sb.Append(levelMap.CheckpointCountPerLap);
		sb.Append('|');
		sb.Append(levelMap.RoadTileCount);
		sb.Append('|');
		sb.Append(levelMap.IsDayTrack ? 1 : 0);
		sb.Append('|');
		sb.Append(tilesRLE);

		return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
	}

	/// <summary>
	/// Attempts to decode a level string and add the resulting level to game data.
	/// </summary>
	/// <param name="levelString">Encoded level string to import.</param>
	/// <returns><c>true</c> if the level was decoded and added; otherwise <c>false</c>.</returns>
	public static bool TryImportLevelFromString(string levelString)
	{
		if (!string.IsNullOrEmpty(levelString) && TryDecodeLevel(levelString, out LevelMap levelMap))
		{
			GameDataManager.Instance.AddLevel(levelMap);
			return true;
		}
		else
		{
			Debug.LogWarning("[ImportExportManager] Failed to import level from string.");
			return false;
		}
	}

	/// <summary>
	/// Attempts to decode a level map from the system clipboard.
	/// </summary>
	/// <param name="levelMap">Decoded level map when clipboard import succeeds; otherwise <c>null</c>.</param>
	/// <returns><c>true</c> if the clipboard contained a valid encoded level; otherwise <c>false</c>.</returns>
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

	/// <summary>
	/// Encodes a level map and copies the encoded string to the system clipboard.
	/// </summary>
	/// <param name="levelMap">Level map to export.</param>
	public static void ExportLevelToClipboard(LevelMap levelMap)
	{
		string encoded = EncodeLevel(levelMap);
		GUIUtility.systemCopyBuffer = encoded;
	}

	/// <summary>
	/// Converts an integer array to a signed byte array.
	/// </summary>
	/// <param name="intArray">Integer array to convert.</param>
	/// <returns>Signed byte array containing the converted values.</returns>
	/// <remarks>
	/// This is used before run-length encoding tile values. Tile values are expected to fit into
	/// the <see cref="sbyte"/> range.
	/// </remarks>
	public static sbyte[] ConvertIntArrayToSByteArray(int[] intArray)
	{
		sbyte[] sbyteArray = new sbyte[intArray.Length];
		for (int i = 0; i < intArray.Length; i++)
		{
			sbyteArray[i] = (sbyte)intArray[i];
		}

		return sbyteArray;
	}

	/// <summary>
	/// Encodes a flat tile array with run-length encoding.
	/// </summary>
	/// <param name="flatTiles">Flattened tile values to encode.</param>
	/// <returns>Run-length encoded tile string.</returns>
	/// <remarks>
	/// The produced format stores runs as <c>count:symbol</c> pairs separated by semicolons.
	/// For example, three road tiles followed by two grass tiles would be encoded as
	/// <c>3:1;2:0</c>.
	/// </remarks>
	public static string EncodeFlatTilesRLE(sbyte[] flatTiles)
	{
		if (flatTiles == null || flatTiles.Length == 0)
		{
			return string.Empty;
		}

		StringBuilder sb = new StringBuilder();
		int symbolCounter = 1;
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

	/// <summary>
	/// Attempts to decode run-length encoded tile data into a flat integer tile array.
	/// </summary>
	/// <param name="rle">Run-length encoded tile string.</param>
	/// <param name="expectedLength">Expected number of decoded tile values.</param>
	/// <param name="flatTiles">Decoded flat tile array when decoding succeeds; otherwise <c>null</c>.</param>
	/// <returns><c>true</c> if decoding succeeded and the decoded length matched <paramref name="expectedLength"/>; otherwise <c>false</c>.</returns>
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
				Debug.LogWarning("[ImportExportManager] Invalid RLE format.");
				return false;
			}

			if (!int.TryParse(parts[0], out int count) || !int.TryParse(parts[1], out int symbol))
			{
				Debug.LogWarning("[ImportExportManager] Invalid RLE number format.");
				return false;
			}

			for (int i = 0; i < count; i++)
			{
				if (index >= expectedLength)
				{
					Debug.LogWarning("[ImportExportManager] Decoded length exceeds expected length.");
					return false;
				}

				tempTiles[index++] = symbol;
			}
		}

		if (index != expectedLength)
		{
			Debug.LogWarning("[ImportExportManager] Decoded length does not match expected length.");
			return false;
		}

		flatTiles = tempTiles;
		return true;
	}
}