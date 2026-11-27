using System.Collections;
using UnityEngine;

/// <summary>
/// Рушащаяся платформа. Встал на неё — она трясётся полсекунды и падает.
/// Через несколько секунд появляется снова. Задерживаться нельзя!
///
/// У платформы нет Rigidbody2D (как у обычной земли), поэтому "падение" —
/// это просто анимация картинки, а коллайдер мы выключаем.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CrumblingPlatform : MonoBehaviour
{
    [Tooltip("Сколько секунд трясётся, прежде чем упасть")]
    [SerializeField] private float shakeTime = 0.5f;
    [Tooltip("Через сколько секунд после падения появляется снова")]
    [SerializeField] private float respawnTime = 3f;
    [Tooltip("Дочерний объект с картинкой")]
    [SerializeField] private Transform visual;

    private BoxCollider2D box;
    private SpriteRenderer[] renderers;
    private Color[] colors;
    private Vector3 visualStart;
    private bool isBusy;
    private readonly Collider2D[] overlapHits = new Collider2D[4];

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
        colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
        if (visual != null) visualStart = visual.localPosition;
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckPlayer(collision);

    private void OnCollisionStay2D(Collision2D collision) => CheckPlayer(collision);

    private void CheckPlayer(Collision2D collision)
    {
        if (isBusy) return;
        if (!collision.gameObject.TryGetComponent(out PlayerController _)) return;

        // Срабатываем, только если игрок стоит СВЕРХУ (его низ не ниже нашего верха).
        // collision.collider — коллайдер игрока, box — наш.
        if (collision.collider.bounds.min.y < box.bounds.max.y - 0.1f) return;

        StartCoroutine(Crumble());
    }

    private IEnumerator Crumble()
    {
        isBusy = true;
        SoundFX.Play(Sfx.Crumble);

        // 1) Трясёмся.
        for (float t = 0f; t < shakeTime; t += Time.deltaTime)
        {
            if (visual != null) visual.localPosition = visualStart + (Vector3)(Random.insideUnitCircle * 0.06f);
            yield return null;
        }

        // 2) Падаем: выключаем коллайдер, картинка ускоряется вниз и тает.
        box.enabled = false;
        Effects.Dust(new Vector2(transform.position.x, transform.position.y - 0.5f), 8, colors.Length > 0 ? colors[0] : Color.white);
        const float fallTime = 0.5f;
        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            if (visual != null) visual.localPosition = visualStart + Vector3.down * (t * t * 8f);
            SetAlpha(1f - t / fallTime);
            yield return null;
        }
        SetAlpha(0f);
        if (visual != null) visual.localPosition = visualStart;

        // 3) Ждём и появляемся снова — но не внутри игрока.
        yield return new WaitForSeconds(respawnTime);
        while (PlayerInside()) yield return null;

        box.enabled = true;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            SetAlpha(t / 0.25f);
            yield return null;
        }
        SetAlpha(1f);
        isBusy = false;
    }

    private bool PlayerInside()
    {
        // Коллайдер выключен, поэтому его границы считаем сами.
        Vector2 center = (Vector2)transform.position + box.offset;
        Vector2 size = Vector2.Scale(box.size, transform.lossyScale);
        var filter = new ContactFilter2D { useTriggers = false };
        int count = Physics2D.OverlapBox(center, size, 0f, filter, overlapHits);
        for (int i = 0; i < count; i++)
        {
            if (overlapHits[i].GetComponent<PlayerController>() != null) return true;
        }
        return false;
    }

    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = colors[i];
            c.a = colors[i].a * alpha;
            renderers[i].color = c;
        }
    }
}
