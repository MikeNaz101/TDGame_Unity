using UnityEngine;

public class Game_UI_Manager : MonoBehaviour
{

    public static Game_UI_Manager Instance;


    [SerializeField] private GameObject pauseUI;
    [SerializeField] private GameObject gameOverUI;
    [HideInInspector] public bool isPaused = false;
    [HideInInspector] public bool isGameOver = false;
    void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        pauseUI.SetActive(false);
        gameOverUI.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isGameOver)
            {
                return;
            }

            if (!isPaused)
            {
                PauseGame();
            } else {
                ResumeGame();
            }
        }
    }
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0;
        pauseUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseUI.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void GameOver()
    {
        isGameOver = true;
        
        // 1. Show the UI
        if (gameOverUI != null) gameOverUI.SetActive(true);
        
        // 2. Unlock Cursor so player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Pause the Game
        Time.timeScale = 0f;
    }

}
