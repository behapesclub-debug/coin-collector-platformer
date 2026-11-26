using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Главное меню: кнопки "Играть" и "Выход" + строка с рекордами.</summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string firstLevelScene = "Level1";
    [SerializeField] private string[] levelScenes = { "Level1", "Level2" };
    [SerializeField] private Text recordsText;

    private void Start()
    {
        Time.timeScale = 1f;
        ShowRecords();
    }

    // Рекорды сохраняет GameManager в PlayerPrefs при прохождении уровня.
    private void ShowRecords()
    {
        if (recordsText == null) return;

        string text = "";
        for (int i = 0; i < levelScenes.Length; i++)
        {
            string scene = levelScenes[i];
            if (!PlayerPrefs.HasKey("BestTime_" + scene)) continue;

            float bestTime = PlayerPrefs.GetFloat("BestTime_" + scene);
            int bestCoins = PlayerPrefs.GetInt("BestCoins_" + scene);
            text += $"Уровень {i + 1}: {bestCoins} монет, {GameUI.FormatTime(bestTime)}     ";
        }
        recordsText.text = text.Length > 0 ? "Рекорды —  " + text : "";
    }

    public void Play()
    {
        GameProgress.Reset(); // новая игра — жизни и монеты с нуля
        SceneManager.LoadScene(firstLevelScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        // В редакторе Application.Quit() ничего не делает, поэтому просто выключаем Play.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
