using UnityEngine;

public abstract class RaceOverlaySource : MonoBehaviour
{
	public abstract bool TryGetState(out RaceOverlayState state);
}
