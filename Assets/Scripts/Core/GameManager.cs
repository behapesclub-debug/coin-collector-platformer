using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Мозг" уровня. Хранит монеты и жизни, решает, что делать,
/// когда игрок получил урон, дошёл до финиша или проиграл.
///
/// Остальные скрипты не меняют жизни/монеты сами — они просят GameManager:
///   Coin      → GameManager.Instance.AddCoin(1)
///   Hazard    → GameManager.Instance.PlayerHit()
///   Finish    → GameManager.Instance.LevelComplete()
/// Так вся игровая логика лежит в одном месте.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>
    /// Паттерн "Синглтон": единственный GameManager на сцене доступен
    /// из любого скрипта через GameManager.Instance.
    /// </summary>
    public static GameManager Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameUI ui;

    [Header("Правила")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int maxLives = 5;
    [Tooltip("Сколько секунд игрок мигает и неуязвим после урона")]
    [SerializeField] private float invulnerabilityTime = 1.5f;

    [Header("Уровни")]
    [SerializeField] private string levelName = "Уровень 1";
    [Tooltip("Имя сцены следующего уровня. Пусто = это последний уровень")]
    [SerializeField] private string nextLevelScene = "";
    [SerializeField] private string firstLevelScene = "Level1";
    [SerializeField] private string mainMenuScene = "MainMenu";

    private int coins;
    private int lives;
    private int coinsOnLevel;
    private Vector3 respawnPoint;
    private bool levelEnded;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; // на случай, если пришли с экрана паузы/проигрыша
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        lives = GameProgress.Lives > 0 ? GameProgress.Lives : startingLives;
        coinsOnLevel = FindObjectsByType<Coin>(FindObjectsSortMode.None).Length;
        respawnPoint = player.transform.position; // первая точка возрождения — старт

        ui.HidePanels();
        ui.SetLevelName(levelName);
        RefreshUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoToMainMenu();
        }
    }

    // --- Вызывают предметы и объекты уровня -----------------------------

    public void AddCoin(int amount)
    {
        coins += amount;
        RefreshUI();
    }

    public void AddLife(int amount)
    {
        lives = Mathf.Min(lives + amount, maxLives);
        RefreshUI();
    }

    public void SetCheckpoint(Vector3 position)
    {
        respawnPoint = position;
    }

    /// <summary>Игрок коснулся опасности.</summary>
    public void PlayerHit(bool ignoreInvulnerability = false)
    {
        if (levelEnded) return;
        if (player.IsInvulnerable && !ignoreInvulnerability) return;

        lives--;
        RefreshUI();

        if (lives <= 0)
        {
            GameOver();
        }
        else
        {
            player.Respawn(respawnPoint, invulnerabilityTime);
        }
    }

    public void LevelComplete()
    {
        if (levelEnded) return;
        levelEnded = true;

        // Запоминаем прогресс для следующего уровня.
        GameProgress.TotalCoins += coins;
        GameProgress.Lives = lives;

        player.SetControlsEnabled(false);
        bool hasNextLevel = !string.IsNullOrEmpty(nextLevelScene);
        ui.ShowWin(hasNextLevel, coins, coinsOnLevel, GameProgress.TotalCoins);
        Time.timeScale = 0f; // "замораживаем" игру
    }

    private void GameOver()
    {
        levelEnded = true;
        player.SetControlsEnabled(false);
        ui.ShowGameOver(coins, coinsOnLevel);
        Time.timeScale = 0f;
    }

    private void RefreshUI()
    {
        ui.UpdateHud(coins, coinsOnLevel, lives);
    }

    // --- Вызывают кнопки UI (подключены в Inspector → Button → On Click) ---

    /// <summary>"Начать заново": перезапустить текущий уровень с полными жизнями.</summary>
    public void RestartLevel()
    {
        GameProgress.Lives = startingLives;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadNextLevel()
    {
        SceneManager.LoadScene(nextLevelScene);
    }

    /// <summary>"Играть снова" после победы: всё с нуля, с первого уровня.</summary>
    public void PlayAgain()
    {
        GameProgress.Reset();
        SceneManager.LoadScene(firstLevelScene);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}
