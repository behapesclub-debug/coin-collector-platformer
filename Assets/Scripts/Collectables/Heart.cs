/// <summary>
/// Сердечко — даёт дополнительную жизнь.
/// Пример того, как легко добавить НОВЫЙ предмет: наследуемся от
/// Collectable и пишем только метод Collect. Всё остальное уже работает.
/// </summary>
public class Heart : Collectable
{
    [UnityEngine.SerializeField] private int lives = 1;

    public override void Collect(PlayerController player)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddLife(lives);
        }
    }
}
