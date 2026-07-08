#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for editing <see cref="TrackPieceRule"/> entries in the Unity Inspector.
/// </summary>
/// <remarks>
/// @ingroup level_gen
/// @brief Provides a compact Rule-Tile-like editor for track-piece placement rules.
///
/// The drawer presents the regular rule fields, such as name, prefab, position offset,
/// rotation, and pattern size, on the left side. On the right side it draws a clickable
/// square pattern grid. Each grid cell cycles through the supported pattern states:
/// empty, track, and checkpoint.
///
/// This editor-only class affects only how <see cref="TrackPieceRule"/> is displayed
/// in the Inspector. It does not change the runtime placement logic directly.
/// </remarks>
[CustomPropertyDrawer(typeof(TrackPieceRule))]
public class TrackPieceRuleDrawer : PropertyDrawer
{
	/// <summary>
	/// Spacing in pixels between cells in the pattern grid.
	/// </summary>
	private const float CellGap = 2f;

	/// <summary>
	/// Returns the required vertical height for drawing one <see cref="TrackPieceRule"/>.
	/// </summary>
	/// <param name="property">Serialized <see cref="TrackPieceRule"/> property to draw.</param>
	/// <param name="label">Inspector label assigned to the property.</param>
	/// <returns>Height in pixels required by the custom drawer.</returns>
	/// <remarks>
	/// Collapsed rules only need one line for the foldout. Expanded rules reserve enough
	/// space for both the left-side fields and the right-side pattern grid.
	/// </remarks>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		if (!property.isExpanded)
			return EditorGUIUtility.singleLineHeight + 2f;

		SerializedProperty sizeProperty = property.FindPropertyRelative("Size");

		int size = Mathf.Max(3, sizeProperty.intValue);
		if (size % 2 == 0)
			size += 1;

		float cellSize = GetCellSize(size);
		float gridHeight = size * cellSize + (size - 1) * CellGap;
		float fieldHeight = EditorGUIUtility.singleLineHeight * 5f + EditorGUIUtility.standardVerticalSpacing * 5f;

		return EditorGUIUtility.singleLineHeight + 6f + Mathf.Max(gridHeight, fieldHeight) + 6f;
	}

	/// <summary>
	/// Draws one <see cref="TrackPieceRule"/> in the Inspector.
	/// </summary>
	/// <param name="position">Rectangle allocated by Unity for this property.</param>
	/// <param name="property">Serialized <see cref="TrackPieceRule"/> property to draw.</param>
	/// <param name="label">Inspector label assigned to the property.</param>
	/// <remarks>
	/// The drawer displays a foldout header using the rule name when available. When expanded,
	/// the left side contains the editable rule data, while the right side contains the visual
	/// pattern grid. The pattern array is automatically resized to match the selected square
	/// pattern size.
	/// </remarks>
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		SerializedProperty nameProperty = property.FindPropertyRelative("Name");
		SerializedProperty prefabProperty = property.FindPropertyRelative("Prefab");
		SerializedProperty positionOffsetProperty = property.FindPropertyRelative("PositionOffset");
		SerializedProperty rotationProperty = property.FindPropertyRelative("RotationEuler");
		SerializedProperty sizeProperty = property.FindPropertyRelative("Size");
		SerializedProperty patternProperty = property.FindPropertyRelative("Pattern");

		string title = string.IsNullOrWhiteSpace(nameProperty.stringValue)
			? label.text
			: nameProperty.stringValue;

		Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
		property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

		if (!property.isExpanded)
		{
			EditorGUI.EndProperty();
			return;
		}

		sizeProperty.intValue = Mathf.Max(3, sizeProperty.intValue);

		if (sizeProperty.intValue % 2 == 0)
			sizeProperty.intValue += 1;

		int size = sizeProperty.intValue;
		int requiredLength = size * size;

		if (patternProperty.arraySize != requiredLength)
			patternProperty.arraySize = requiredLength;

		float y = position.y + EditorGUIUtility.singleLineHeight + 4f;
		float line = EditorGUIUtility.singleLineHeight;
		float spacing = EditorGUIUtility.standardVerticalSpacing;

		float cellSize = GetCellSize(size);
		float gridWidth = size * cellSize + (size - 1) * CellGap;

		float leftWidth = position.width - gridWidth - 14f;
		leftWidth = Mathf.Max(leftWidth, 180f);

		Rect nameRect = new Rect(position.x, y, leftWidth, line);
		EditorGUI.PropertyField(nameRect, nameProperty);

		y += line + spacing;

		Rect prefabRect = new Rect(position.x, y, leftWidth, line);
		EditorGUI.PropertyField(prefabRect, prefabProperty);

		y += line + spacing;

		Rect posRect = new Rect(position.x, y, leftWidth, line);
		EditorGUI.PropertyField(posRect, positionOffsetProperty);

		y += line + spacing;

		Rect rotRect = new Rect(position.x, y, leftWidth, line);
		EditorGUI.PropertyField(rotRect, rotationProperty);

		y += line + spacing;

		Rect sizeRect = new Rect(position.x, y, leftWidth, line);
		EditorGUI.PropertyField(sizeRect, sizeProperty);

		Rect gridRect = new Rect(
			position.x + position.width - gridWidth,
			position.y + EditorGUIUtility.singleLineHeight + 4f,
			gridWidth,
			gridWidth
		);

		DrawPatternGrid(gridRect, patternProperty, size, cellSize);

		EditorGUI.EndProperty();
	}

	/// <summary>
	/// Draws the clickable visual pattern grid.
	/// </summary>
	/// <param name="rect">Rectangle in which the grid should be drawn.</param>
	/// <param name="patternProperty">Serialized pattern array belonging to the current rule.</param>
	/// <param name="size">Width and height of the square pattern grid.</param>
	/// <param name="cellSize">Size of one pattern cell in pixels.</param>
	/// <remarks>
	/// Each button represents one flattened pattern cell. Clicking a button cycles the cell
	/// through the available <see cref="TrackPatternCell"/> values.
	/// </remarks>
	private static void DrawPatternGrid(Rect rect, SerializedProperty patternProperty, int size, float cellSize)
	{
		for (int row = 0; row < size; row++)
		{
			for (int col = 0; col < size; col++)
			{
				int index = row * size + col;

				Rect cellRect = new Rect(
					rect.x + col * (cellSize + CellGap),
					rect.y + row * (cellSize + CellGap),
					cellSize,
					cellSize
				);

				SerializedProperty cellProperty = patternProperty.GetArrayElementAtIndex(index);
				TrackPatternCell value = (TrackPatternCell)cellProperty.enumValueIndex;

				if (GUI.Button(cellRect, GetCellLabel(value)))
				{
					cellProperty.enumValueIndex = (int)GetNextCell(value);
				}
			}
		}
	}

	/// <summary>
	/// Returns the short label shown inside a pattern cell button.
	/// </summary>
	/// <param name="value">Pattern cell value to display.</param>
	/// <returns>Text label representing the cell value.</returns>
	/// <remarks>
	/// The labels match the compact pattern encoding used by the runtime matcher:
	/// <c>1</c> for track, <c>C</c> for checkpoint, and <c>×</c> for empty.
	/// </remarks>
	private static string GetCellLabel(TrackPatternCell value)
	{
		return value switch
		{
			TrackPatternCell.Track => "1",
			TrackPatternCell.Checkpoint => "C",
			_ => "×"
		};
	}

	/// <summary>
	/// Returns the next cell state used when a pattern grid button is clicked.
	/// </summary>
	/// <param name="value">Current pattern cell value.</param>
	/// <returns>Next pattern cell value in the edit cycle.</returns>
	/// <remarks>
	/// The edit cycle is: empty, track, checkpoint, and then back to empty.
	/// </remarks>
	private static TrackPatternCell GetNextCell(TrackPatternCell value)
	{
		return value switch
		{
			TrackPatternCell.Empty => TrackPatternCell.Track,
			TrackPatternCell.Track => TrackPatternCell.Checkpoint,
			_ => TrackPatternCell.Empty
		};
	}

	/// <summary>
	/// Returns the visual size of one pattern grid cell.
	/// </summary>
	/// <param name="size">Pattern grid size.</param>
	/// <returns>Cell size in pixels.</returns>
	/// <remarks>
	/// Smaller cells are used for larger patterns so that 5x5 rules remain compact in the Inspector.
	/// </remarks>
	private static float GetCellSize(int size)
	{
		return size <= 3 ? 24f : 17f;
	}
}
#endif