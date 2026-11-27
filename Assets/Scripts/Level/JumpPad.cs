using UnityEngine;

/// <summary>
/// Батут (пружина). Наступил — тебя подбрасывает намного выше обычного прыжка.
/// Это не "второй прыжок": игрока подбрасывает предмет, как при отскоке от врага.
///
/// Коллайдер — триггер, поэтому используем OnTriggerEnter2D и OnTriggerStay2D
/// (Stay — на случай, если игрок уже стоит на батуте, когда тот "перезарядился").
/// </summary>
public class JumpPad : MonoBehaviour
{
    [Tooltip("Скорость вверх при подбросе. 21 ≈ 7 клеток вверх")]
    [SerializeField] private float launchSpeed = 21f;
    [Tooltip("Часть батута, которая сжимается (пружина с площадкой)")]
    [SerializeField] private Transform spring;

    private float nextLaunchTime;
    private float bounceTimer;

    private void OnTriggerEnter2D(Collider2D other) => TryLaunch(other);

    private void OnTriggerStay2D(Collider2D other) => TryLaunch(other);

    private void TryLaunch(Collider2D other)
    {
        if (Time.time < nextLaunchTime) return;
        if (!other.TryGetComponent(out PlayerController player)) return;
        if (player.Velocity.y > 1f) return; // пролетаем снизу вверх — не срабатываем

        player.Launch(launchSpeed);
        nextLaunchTime = Time.time + 0.2f;
        bounceTimer = 0.35f;
        SoundFX.Play(Sfx.Spring);
    }

    // Пружина сначала сжимается, потом вытягивается и "дрожит" до нормы.
    private void Update()
    {
        if (spring == null) return;
        float scaleY = 1f;
        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.deltaTime;
            float t = 1f - bounceTimer / 0.35f; // 0 → 1
            scaleY = 1f + Mathf.Sin(t * Mathf.PI * 3f) * 0.5f * (1f - t);
        }
        spring.localScale = new Vector3(1f, scaleY, 1f);
    }
}
