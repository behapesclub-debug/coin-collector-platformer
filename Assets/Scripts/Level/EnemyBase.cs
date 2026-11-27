using System.Collections;
using UnityEngine;

/// <summary>
/// Абстрактный базовый класс для всех врагов (как Collectable для предметов).
/// Здесь то, что одинаково у ВСЕХ врагов:
///   - игрок прыгнул сверху → враг погибает, игрок отскакивает;
///   - игрок коснулся сбоку или снизу → игрок теряет жизнь;
///   - у игрока звезда → враг погибает от любого касания;
///   - красивая гибель: сплющивание, искры, исчезновение.
/// А КАК враг двигается, решают наследники: Enemy (слизень) ходит по земле,
/// FlyingEnemy (летучая мышь) летает.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Tooltip("С какой скоростью подбрасывает игрока после прыжка на врага")]
    [SerializeField] protected float bounceForce = 12f;
    [Tooltip("Дочерний объект с картинкой — его разворачиваем и сплющиваем")]
    [SerializeField] protected Transform visual;
    [SerializeField] private Color deathSparkColor = new Color(0.8f, 0.6f, 1f);

    protected Rigidbody2D rb;
    protected Collider2D bodyCollider;

    public bool IsDead { get; private set; }

    // "protected virtual" — наследник может дополнить Awake, вызвав base.Awake().
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDead) return;
        if (!collision.gameObject.TryGetComponent(out PlayerController player)) return;

        if (player.IsStarPowered)
        {
            Die();
            return;
        }

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

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        SoundFX.Play(Sfx.Stomp);
        Effects.Burst(transform.position, 10, deathSparkColor);
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
    protected Vector2 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    protected void SetVelocity(Vector2 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }
}
