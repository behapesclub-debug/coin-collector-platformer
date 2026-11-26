using UnityEngine;

/// <summary>
/// Монета. Наследуется от Collectable, поэтому уже умеет
/// "подбираться", покачиваться и лопаться. Сама добавляет только:
///   - сколько монет она даёт (value);
///   - эффект вращения.
/// </summary>
public class Coin : Collectable
{
    [SerializeField] private int value = 1;
    [SerializeField] private float spinSpeed = 4f;

    // "override" — дополняем метод родителя.
    protected override void AnimateIdle()
    {
        base.AnimateIdle(); // сначала выполняем покачивание из Collectable

        if (visual != null)
        {
            // Сжимаем монету по ширине туда-сюда — выглядит как вращение.
            float width = Mathf.Abs(Mathf.Cos(Time.time * spinSpeed + Phase));
            visual.localScale = new Vector3(Mathf.Max(width, 0.15f), 1f, 1f);
        }
    }

    public override void Collect(PlayerController player)
    {
        SoundFX.Play(Sfx.Coin);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoin(value);
        }
    }
}
