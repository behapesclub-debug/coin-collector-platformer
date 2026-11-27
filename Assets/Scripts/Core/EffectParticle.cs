using UnityEngine;

/// <summary>
/// Одна частица эффекта (её создаёт Effects). Летит, падает под своей
/// "гравитацией", уменьшается, становится прозрачной и удаляет себя.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EffectParticle : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private Color color;
    private float life;
    private float age;
    private float gravity;
    private float size;

    public void Init(Vector2 startVelocity, float lifeTime, float gravityStrength, Color startColor, float startSize)
    {
        sr = GetComponent<SpriteRenderer>();
        velocity = startVelocity;
        life = Mathf.Max(lifeTime, 0.01f);
        gravity = gravityStrength;
        color = startColor;
        size = startSize;
        sr.color = color;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        float t = age / life; // 0 → 1 за время жизни
        transform.localScale = Vector3.one * size * (1f - 0.6f * t);
        Color c = color;
        c.a = color.a * (1f - t);
        sr.color = c;
    }
}
