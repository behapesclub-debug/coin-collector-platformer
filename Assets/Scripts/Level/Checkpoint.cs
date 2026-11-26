using UnityEngine;

/// <summary>
/// Чекпоинт (флажок). Когда игрок его касается — флажок становится зелёным,
/// и после потери жизни игрок появится уже здесь, а не в начале уровня.
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer flag;
    [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.85f, 0.3f);

    private bool isActivated;

    private void Start()
    {
        if (flag != null) flag.color = inactiveColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isActivated) return;
        if (!other.TryGetComponent(out PlayerController _)) return;

        isActivated = true;
        if (flag != null) flag.color = activeColor;
        if (GameManager.Instance != null) GameManager.Instance.SetCheckpoint(transform.position);
    }
}
