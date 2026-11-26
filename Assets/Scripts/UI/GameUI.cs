using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всё, что связано с интерфейсом уровня: счётчики и экраны
/// "Game Over" / "Победа". GameManager решает, ЧТО показать,
/// а GameUI знает, КАК это показать. Логика отдельно, отображение отдельно.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Счётчики (HUD)")]
    [SerializeField] private Text coinsText;
    [SerializeField] private Text livesText;
    [SerializeField] private Text levelText;

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

    public void HidePanels()
    {
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);
    }

    public void ShowGameOver(int coins, int coinsOnLevel)
    {
        gameOverInfoText.text = $"Жизни закончились.\nМонет на этом уровне: {coins} из {coinsOnLevel}";
        gameOverPanel.SetActive(true);
    }

    public void ShowWin(bool hasNextLevel, int coins, int coinsOnLevel, int totalCoins)
    {
        winTitleText.text = hasNextLevel ? "Уровень пройден!" : "Победа!";

        string info = $"Собрано монет: {coins} из {coinsOnLevel}";
        if (!hasNextLevel) info += $"\nВсего за игру: {totalCoins}";
        winInfoText.text = info;

        nextLevelButton.SetActive(hasNextLevel);
        playAgainButton.SetActive(!hasNextLevel);
        winPanel.SetActive(true);
    }
}
