using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ParalaxScale
{
    public Transform Background;
    public Vector2 Smoothing;

    public ParalaxScale(Transform background, Vector2 smoothing)
    {
        Background = background;
        Smoothing = smoothing;
    }

    public ParalaxScale(Transform background) : this(background, new Vector2(0.5f, 0.5f))
    {
    }

    public static ParalaxScale Default => new ParalaxScale(null, new Vector2(0.5f, 0.5f));

    public static implicit operator ParalaxScale(Transform background) => new ParalaxScale(background);
}

public class ParalaxEffect : MonoBehaviour
{
    [SerializeField] private List<ParalaxScale> backgrounds;
    [SerializeField, ReadOnly] private Transform player;
    [SerializeField, ReadOnly] private Transform mainCamera;
    [SerializeField] private bool targetPlayer = true;
    [SerializeField, ShowIf(nameof(targetPlayer), false)] private Transform target = null;

    private void OnValidate()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        mainCamera = Camera.main?.transform;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        mainCamera = Camera.main?.transform;
    }

    private void Update()
    {
        var lerpTarget = targetPlayer ? player : target;
        Vector3 originalPosition = mainCamera.position;
        if (lerpTarget != null)
        {
            for (int i = 0; i < backgrounds.Count; i++)
            {
                var targetPositionX = Mathf.Lerp(originalPosition.x, lerpTarget.position.x, backgrounds[i].Smoothing.x);
                var targetPositionY = Mathf.Lerp(originalPosition.y, lerpTarget.position.y, backgrounds[i].Smoothing.y);
                backgrounds[i].Background.position = new Vector3(targetPositionX, targetPositionY, backgrounds[i].Background.position.z);
            }
        }
    }
}