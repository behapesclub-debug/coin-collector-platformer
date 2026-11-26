using UnityEngine;

/// <summary>
/// Опасный объект: шипы, яма (невидимая зона под уровнем) и т.п.
/// При касании игрок теряет жизнь и возвращается на старт/чекпоинт.
///
/// Тут есть ОБА способа поймать касание:
///   - OnTriggerEnter2D   — если у коллайдера включено Is Trigger (шипы, яма);
///   - OnCollisionEnter2D — если коллайдер "твёрдый" (например, опасный блок).
/// </summary>
public class Hazard : MonoBehaviour
{
    [Tooltip("Включено для ямы: срабатывает даже когда игрок мигает (неуязвим)")]
    [SerializeField] private bool ignoreInvulnerability;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHurt(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHurt(collision.gameObject);
    }

    private void TryHurt(GameObject other)
    {
        if (other.TryGetComponent(out PlayerController _) && GameManager.Instance != null)
        {
            GameManager.Instance.PlayerHit(ignoreInvulnerability);
        }
    }
}
