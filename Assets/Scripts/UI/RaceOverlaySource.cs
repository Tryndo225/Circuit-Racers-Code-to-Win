using UnityEngine;

/// <summary>
/// Abstract source of race overlay state for <see cref="RaceOverlay"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Decouples the race HUD from the concrete system that provides timing and progress data.
///
/// Implementations can provide overlay data from different contexts, such as normal gameplay,
/// replay playback, or another race-state provider. The overlay only depends on
/// <see cref="TryGetState(out RaceOverlayState)"/> and does not need direct access to
/// gameplay-specific managers.
/// </remarks>
public abstract class RaceOverlaySource : MonoBehaviour
{
	/// <summary>
	/// Attempts to retrieve the current race overlay state.
	/// </summary>
	/// <param name="state">
	/// Current overlay state when data is available; otherwise an implementation-defined default value.
	/// </param>
	/// <returns>
	/// <c>true</c> if valid state data was provided; otherwise <c>false</c>.
	/// </returns>
	public abstract bool TryGetState(out RaceOverlayState state);
}