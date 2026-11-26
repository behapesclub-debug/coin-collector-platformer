using UnityEngine;

/// <summary>Финишный флаг. Коснулся — уровень пройден.</summary>
public class Finish : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController _) && GameManager.Instance != null)
        {
            GameManager.Instance.LevelComplete();
        }
    }
}
