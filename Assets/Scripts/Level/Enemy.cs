using System.Collections;
using UnityEngine;

/// <summary>
/// Враг-слизень. Ходит туда-сюда и разворачивается у стены или края обрыва.
///   - Коснулся игрока сбоку → игрок теряет жизнь.
///   - Игрок прыгнул сверху  → враг расплющен, а игрок отскакивает вверх.
///
/// Здесь используется OnCollisionEnter2D: у врага и у игрока "твёрдые"
/// коллайдеры, и Unity сообщает о столкновении.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private float speed = 2f;
    [Tooltip("С какой скоростью подбрасывает игрока после прыжка на врага")]
    [SerializeField] private float bounceForce = 12f;
    [SerializeField] private Transform visual;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private ContactFilter2D solidFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    private float direction = -1f; // -1 влево, 1 вправо
    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        solidFilter.useTriggers = false; // лучи не замечают монеты, шипы и т.п.
    }

    private void FixedUpdate()
    {
        if (isDead) return;

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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (!collision.gameObject.TryGetComponent(out PlayerController player)) return;

        // Игрок заметно выше центра врага → значит, прыгнул сверху.
        bool fromAbove = player.transform.position.y > transform.position.y + 0.4f;
        if (fromAbove)
        {
            player.Bounce(bounceForce);
            Die();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerHit();
        }
    }

    private void Die()
    {
        isDead = true;
        SoundFX.Play(Sfx.Stomp);
        bodyCollider.enabled = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        SetVelocity(Vector2.zero);
        StartCoroutine(SquashAndDisappear());
    }

    private IEnumerator SquashAndDisappear()
    {
        if (visual != null)
        {
            visual.localScale = new Vector3(1.3f, 0.3f, 1f);
            visual.localPosition = new Vector3(0f, -0.2f, 0f);
        }
        yield return new WaitForSeconds(0.4f);
        Destroy(gameObject);
    }

    // linearVelocity в Unity 6, velocity в старых версиях.
    private Vector2 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetVelocity(Vector2 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }
}
