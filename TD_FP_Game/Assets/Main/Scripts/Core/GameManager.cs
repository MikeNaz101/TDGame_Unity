using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Owns the high-level game state (Playing / Victory / GameOver) and the restart flow.
/// Add this to the GameManager object in the scene. Everything else (Core, Player, WaveManager)
/// reports to it instead of poking Time.timeScale directly.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Victory, GameOver }

    public static GameManager Instance { get; private set; }

    /// <summary>Fired whenever the state changes. Subscribers MUST unsubscribe in OnDestroy.</summary>
    public static event Action<GameState> OnStateChanged;
    /// <summary>Fired on game over with a human readable reason.</summary>
    public static event Action<string> OnGameOver;
    public static event Action OnVictory;

    [Header("Flow")]
    [Tooltip("Freeze the simulation when the game ends.")]
    public bool freezeTimeOnEnd = true;
    [Tooltip("Unscaled seconds to wait after the game ends before the restart key is accepted.")]
    public float restartDelay = 1.5f;
    [Tooltip("Allow pressing R to reload the scene once the game has ended.")]
    public bool allowRestartKey = true;

    [Header("Optional UI")]
    [Tooltip("Panel shown when the player loses.")]
    public GameObject gameOverPanel;
    [Tooltip("Panel shown when the player wins.")]
    public GameObject victoryPanel;
    [Tooltip("Optional text that shows why the game ended.")]
    public TMPro.TMP_Text endReasonText;

    public GameState State { get; private set; } = GameState.Playing;
    public bool IsGameOver => State != GameState.Playing;
    public float TimePlayed { get; private set; }

    private float _endedAtUnscaled;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsGameOver)
        {
            TimePlayed += Time.deltaTime;
            return;
        }

        if (allowRestartKey && Time.unscaledTime - _endedAtUnscaled >= restartDelay && RestartPressed())
        {
            Restart();
        }
    }

    private static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    public void GameOver(string reason)
    {
        if (IsGameOver) return;
        SetState(GameState.GameOver);
        Debug.Log($"GAME OVER: {reason}");

        if (endReasonText != null) endReasonText.text = reason;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        OnGameOver?.Invoke(reason);
        EndGame();
    }

    public void Victory()
    {
        if (IsGameOver) return;
        SetState(GameState.Victory);
        Debug.Log("VICTORY: All waves defeated!");

        if (endReasonText != null) endReasonText.text = "All waves defeated!";
        if (victoryPanel != null) victoryPanel.SetActive(true);
        OnVictory?.Invoke();
        EndGame();
    }

    private void EndGame()
    {
        _endedAtUnscaled = Time.unscaledTime;
        if (freezeTimeOnEnd) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetState(GameState newState)
    {
        if (State == newState) return;
        State = newState;
        OnStateChanged?.Invoke(State);
    }

    /// <summary>Reloads the active scene. Also hooked up to UI buttons.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
