using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всё, что связано с интерфейсом уровня: счётчики, таймер и экраны
/// "Пауза" / "Game Over" / "Победа". GameManager решает, ЧТО показать,
/// а GameUI знает, КАК это показать. Логика отдельно, отображение отдельно.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Счётчики (HUD)")]
    [SerializeField] private Text coinsText;
    [SerializeField] private Text livesText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text timerText;

    [Header("Экран паузы")]
    [SerializeField] private GameObject pausePanel;

    [Header("Экран Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverInfoText;

    [Header("Экран победы")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winTitleText;
    [SerializeField] private Text winInfoText;
    [SerializeField] private GameObject nextLevelButton;
    [SerializeField] private GameObject playAgainButton;

    public void UpdateHud(int coins, int coinsOnLevel, int lives)
    {
        coinsText.text = $"Монеты: {coins} / {coinsOnLevel}";
        livesText.text = "Жизни: " + new string('♥', Mathf.Max(lives, 0)) + $"  ({lives})";
    }

    public void SetLevelName(string levelName)
    {
        levelText.text = levelName;
    }

    public void SetTimer(float seconds)
    {
        if (timerText != null) timerText.text = "Время: " + FormatTime(seconds);
    }

    public void HidePanels()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);
    }

    public void ShowPause(bool visible)
    {
        if (pausePanel != null) pausePanel.SetActive(visible);
    }

    public void ShowGameOver(int coins, int coinsOnLevel)
    {
        gameOverInfoText.text = $"Жизни закончились.\nМонет на этом уровне: {coins} из {coinsOnLevel}";
        gameOverPanel.SetActive(true);
    }

    public void ShowWin(bool hasNextLevel, int coins, int coinsOnLevel, int totalCoins,
        float time, float bestTime, int bestCoins, bool newRecord)
    {
        winTitleText.text = hasNextLevel ? "Уровень пройден!" : "Победа!";

        string info = $"Монеты: {coins} из {coinsOnLevel}     Время: {FormatTime(time)}";
        info += $"\nРекорд: {bestCoins} монет, {FormatTime(bestTime)}";
        if (newRecord) info += "   ★ Новый рекорд!";
        if (!hasNextLevel) info += $"\nВсего монет за игру: {totalCoins}";
        winInfoText.text = info;

        nextLevelButton.SetActive(hasNextLevel);
        playAgainButton.SetActive(!hasNextLevel);
        winPanel.SetActive(true);
    }

    // 83.4 секунды → "1:23.4"
    public static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return $"{minutes}:{rest:00.0}";
    }
}
