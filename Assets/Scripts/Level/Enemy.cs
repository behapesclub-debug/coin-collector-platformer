using UnityEngine;

/// <summary>
/// Враг-слизень. Ходит туда-сюда и разворачивается у стены или края обрыва.
/// Что происходит при касании игрока (урон, прыжок сверху, звезда),
/// описано в базовом классе EnemyBase — здесь только ходьба.
/// </summary>
public class Enemy : EnemyBase
{
    [SerializeField] private float speed = 2f;

    private ContactFilter2D solidFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    private float direction = -1f; // -1 влево, 1 вправо

    protected override void Awake()
    {
        base.Awake(); // сначала то, что делает EnemyBase (находит Rigidbody2D и коллайдер)
        solidFilter.useTriggers = false; // лучи не замечают монеты, шипы и т.п.
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        // Проверяем, что впереди: стена или обрыв → разворот.
        if (WallAhead() || !GroundAhead()) direction = -direction;

        Vector2 velocity = GetVelocity();
        velocity.x = direction * speed;
        SetVelocity(velocity);

        if (visual != null) visual.localScale = new Vector3(direction, 1f, 1f); // смотрим туда, куда идём
        if (transform.position.y < -50f) Destroy(gameObject); // на всякий случай: упал в бездну
    }

    // Луч вниз чуть впереди врага: есть ли там пол?
    private bool GroundAhead()
    {
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x + direction * (b.extents.x + 0.05f), b.min.y + 0.05f);
        return CastHitsSomething(origin, Vector2.down, 0.4f);
    }

    // Луч вперёд на уровне середины врага: нет ли стены?
    private bool WallAhead()
    {
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x + direction * (b.extents.x + 0.02f), b.center.y);
        return CastHitsSomething(origin, new Vector2(direction, 0f), 0.1f, ignorePlayer: true);
    }

    private bool CastHitsSomething(Vector2 origin, Vector2 dir, float distance, bool ignorePlayer = false)
    {
        int count = Physics2D.Raycast(origin, dir, solidFilter, hits, distance);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == bodyCollider) continue;
            if (ignorePlayer && hits[i].collider.GetComponent<PlayerController>() != null) continue;
            return true;
        }
        return false;
    }
}
