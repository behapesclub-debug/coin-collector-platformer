using UnityEngine;

/// <summary>
/// Звезда неуязвимости. Ещё один наследник Collectable — как Coin и Heart:
/// покачивание, подбор и исчезновение уже работают, пишем только своё.
/// На несколько секунд игрок переливается радугой: шипы не ранят,
/// а враги погибают от одного касания. В яму падать всё равно нельзя!
/// </summary>
public class Star : Collectable
{
    [Tooltip("Сколько секунд действует звезда")]
    [SerializeField] private float duration = 6f;

    protected override void AnimateIdle()
    {
        base.AnimateIdle(); // покачивание из Collectable
        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f + Phase) * 15f);
        }
    }

    public override void Collect(PlayerController player)
    {
        SoundFX.Play(Sfx.PowerUp);
        Effects.Burst(transform.position, 16, new Color(1f, 0.95f, 0.4f), 6f);
        player.GiveStarPower(duration);
    }
}
