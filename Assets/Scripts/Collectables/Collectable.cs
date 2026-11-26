using UnityEngine;

/// <summary>
/// Абстрактный базовый класс для всех подбираемых предметов.
///
/// "abstract" значит, что сам по себе этот класс на объект не повесить —
/// он только общая основа. Здесь написано то, что одинаково у ВСЕХ предметов:
///   1) обнаружить, что игрок коснулся предмета (OnTriggerEnter2D);
///   2) вызвать Collect();
///   3) красиво покачиваться, а после подбора — "лопнуть" (увеличиться и исчезнуть).
/// А вот ЧТО именно делает предмет (даёт монету, жизнь...) — решают наследники
/// (Coin, Heart), переопределяя метод Collect.
/// </summary>
public abstract class Collectable : MonoBehaviour, ICollectable
{
    [Header("Анимация")]
    [Tooltip("Дочерний объект с картинкой, который будет покачиваться")]
    [SerializeField] protected Transform visual;
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float bobSpeed = 3f;
    [Tooltip("Сколько секунд длится эффект исчезновения")]
    [SerializeField] private float popDuration = 0.25f;

    // Сдвиг фазы, чтобы соседние монеты качались не синхронно.
    protected float Phase { get; private set; }

    private bool isCollected;
    private float popTimer;
    private SpriteRenderer[] renderers;

    protected virtual void Start()
    {
        Phase = transform.position.x * 0.7f;
        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (isCollected) AnimatePop();
        else AnimateIdle();
    }

    // "virtual" — наследник может дополнить эту анимацию (см. Coin.AnimateIdle).
    protected virtual void AnimateIdle()
    {
        if (visual != null)
        {
            float y = Mathf.Sin(Time.time * bobSpeed + Phase) * bobHeight;
            visual.localPosition = new Vector3(0f, y, 0f);
        }
    }

    // Предмет увеличивается и становится прозрачным, потом удаляется.
    private void AnimatePop()
    {
        popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);

        if (visual != null)
        {
            visual.localScale = Vector3.one * (1f + t);
            visual.localPosition = new Vector3(0f, t * 0.5f, 0f);
        }
        foreach (SpriteRenderer sr in renderers)
        {
            Color c = sr.color;
            c.a = 1f - t;
            sr.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }

    // Unity сам вызывает этот метод, когда в наш коллайдер-триггер
    // (Is Trigger = true) кто-то вошёл. Чтобы событие сработало,
    // хотя бы у одного из объектов должен быть Rigidbody2D (он есть у игрока).
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return; // защита от двойного срабатывания

        // TryGetComponent: "есть ли у вошедшего объекта скрипт PlayerController?"
        if (other.TryGetComponent(out PlayerController player))
        {
            isCollected = true;
            GetComponent<Collider2D>().enabled = false;
            Collect(player);
        }
    }

    /// <summary>Каждый наследник обязан сам описать, что он даёт игроку.</summary>
    public abstract void Collect(PlayerController player);
}
