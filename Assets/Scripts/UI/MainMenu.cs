using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Главное меню: кнопки "Играть" и "Выход".</summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string firstLevelScene = "Level1";

    private void Start()
    {
        Time.timeScale = 1f;
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
