using UnityEngine;

/// <summary>
/// Камера плавно следует за игроком и не вылезает за границы уровня.
/// Вешается на Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("Смещение относительно игрока: чуть выше, чтобы видеть, куда прыгаешь")]
    [SerializeField] private Vector2 offset = new Vector2(2f, 1.5f);
    [Tooltip("Чем меньше, тем резче камера догоняет игрока")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Границы уровня (мировые координаты)")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-0.5f, -12f);
    [SerializeField] private Vector2 maxBounds = new Vector2(100f, 4f);

    private Camera cam;
    private Vector3 currentVelocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target != null) transform.position = GetDesiredPosition(); // сразу встаём на место
    }

    // LateUpdate вызывается ПОСЛЕ всех Update — игрок уже сдвинулся,
    // и камера не будет "дёргаться".
    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref currentVelocity, smoothTime);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 desired = target.position + (Vector3)offset;
        desired.z = transform.position.z; // камера всегда остаётся "перед" сценой

        if (useBounds)
        {
            // Половина видимой области камеры в мировых единицах.
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            desired.x = ClampAxis(desired.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            desired.y = ClampAxis(desired.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        }
        return desired;
    }

    // Если уровень меньше экрана — просто ставим камеру по центру.
    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }
}
