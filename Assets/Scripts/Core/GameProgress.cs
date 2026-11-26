/// <summary>
/// Прогресс между уровнями.
///
/// При загрузке новой сцены Unity удаляет ВСЕ объекты старой сцены
/// (вместе с GameManager). А вот static-поля принадлежат самому классу,
/// а не объекту, поэтому переживают смену сцены. Так мы переносим
/// жизни и монеты с уровня 1 на уровень 2.
/// </summary>
public static class GameProgress
{
    /// <summary>Жизни, с которыми начнётся следующий уровень (0 = взять стартовые).</summary>
    public static int Lives;

    /// <summary>Сколько монет собрано на всех ПРОЙДЕННЫХ уровнях.</summary>
    public static int TotalCoins;

    public static void Reset()
    {
        Lives = 0;
        TotalCoins = 0;
    }
}
