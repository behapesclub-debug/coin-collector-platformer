using UnityEngine;

/// <summary>
/// Движущаяся платформа (бонус). Ездит туда-обратно между стартовой
/// точкой и точкой "старт + moveOffset".
///
/// У платформы Rigidbody2D типа Kinematic: гравитация на неё не действует,
/// её никто не может толкнуть, а двигаем мы её сами через MovePosition.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Куда ехать относительно старта (в клетках). (4, 0) — вправо на 4, (0, 4) — вверх на 4")]
    [SerializeField] private Vector2 moveOffset = new Vector2(4f, 0f);
    [Tooltip("Скорость, клеток в секунду")]
    [SerializeField] private float speed = 2f;

    private Rigidbody2D rb;
    private Vector2 startPosition;

    /// <summary>Текущая скорость платформы — игрок прибавляет её к своей, чтобы "ехать" на ней.</summary>
    public Vector2 Velocity { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPosition = rb.position;
    }

    // FixedUpdate вызывается с постоянным шагом (по умолчанию 50 раз в секунду)
    // — вся работа с физикой должна быть здесь.
    private void FixedUpdate()
    {
        float distance = moveOffset.magnitude;
        if (distance < 0.01f) return;

        // PingPong даёт число, которое бегает 0 → 1 → 0 → 1 ...
        float t = Mathf.PingPong(Time.fixedTime * speed / distance, 1f);
        // SmoothStep делает плавный разгон и торможение у краёв.
        t = Mathf.SmoothStep(0f, 1f, t);

        Vector2 target = startPosition + moveOffset * t;
        Velocity = (target - rb.position) / Time.fixedDeltaTime;
        rb.MovePosition(target);
    }

    // Рисует в окне Scene линию, по которой поедет платформа (в игре не видно).
    private void OnDrawGizmos()
    {
        Vector3 from = Application.isPlaying ? (Vector3)startPosition : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, from + (Vector3)moveOffset);
        Gizmos.DrawWireSphere(from + (Vector3)moveOffset, 0.2f);
    }
}
