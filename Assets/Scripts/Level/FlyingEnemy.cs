using UnityEngine;

/// <summary>
/// Летучая мышь. Летает туда-обратно и покачивается вверх-вниз, машет крыльями.
/// Земля ей не нужна, поэтому Rigidbody2D у неё Kinematic, как у движущейся платформы:
/// гравитации нет, мы сами двигаем её через MovePosition.
/// Урон, прыжок сверху и звезда работают так же, как у слизня (см. EnemyBase).
/// </summary>
public class FlyingEnemy : EnemyBase
{
    [Tooltip("Куда летать относительно старта (в клетках)")]
    [SerializeField] private Vector2 moveOffset = new Vector2(3f, 0f);
    [Tooltip("Скорость, клеток в секунду")]
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private float bobHeight = 0.35f;
    [SerializeField] private float bobSpeed = 4f;

    [Header("Крылья (машут)")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;

    private Vector2 startPosition;

    protected override void Awake()
    {
        base.Awake();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPosition = rb.position;
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        // Тот же приём, что у MovingPlatform: PingPong гоняет t между 0 и 1.
        float distance = moveOffset.magnitude;
        float t = distance > 0.01f ? Mathf.PingPong(Time.fixedTime * speed / distance, 1f) : 0f;
        t = Mathf.SmoothStep(0f, 1f, t);

        Vector2 target = startPosition + moveOffset * t;
        target.y += Mathf.Sin(Time.fixedTime * bobSpeed) * bobHeight; // покачивание

        float dx = target.x - rb.position.x;
        if (visual != null && Mathf.Abs(dx) > 0.001f) visual.localScale = new Vector3(Mathf.Sign(dx), 1f, 1f);

        rb.MovePosition(target);
    }

    private void Update()
    {
        if (IsDead) return;
        float angle = Mathf.Sin(Time.time * 18f) * 35f;
        if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void OnDrawGizmos()
    {
        Vector3 from = Application.isPlaying ? (Vector3)startPosition : transform.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(from, from + (Vector3)moveOffset);
    }
}
