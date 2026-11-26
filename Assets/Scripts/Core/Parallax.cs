using UnityEngine;

/// <summary>
/// Параллакс — эффект глубины: дальние объекты (горы, облака) двигаются
/// медленнее ближних. Скрипт сдвигает свой слой вслед за камерой на долю
/// её движения:
///   factor = 0   — слой стоит на месте, как обычный объект уровня;
///   factor = 1   — слой "приклеен" к камере, как небо (очень далеко).
///
/// [DefaultExecutionOrder(100)] — выполняться ПОСЛЕ остальных скриптов
/// (в т.ч. после CameraFollow), чтобы фон не дрожал.
/// </summary>
[DefaultExecutionOrder(100)]
public class Parallax : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float factor = 0.5f;

    private Transform cam;
    private Vector3 startPosition;
    private float cameraStartX;

    private void Start()
    {
        if (Camera.main == null) { enabled = false; return; }
        cam = Camera.main.transform;
        startPosition = transform.position;
        cameraStartX = cam.position.x;
    }

    private void LateUpdate()
    {
        float dx = (cam.position.x - cameraStartX) * factor;
        transform.position = startPosition + new Vector3(dx, 0f, 0f);
    }
}
