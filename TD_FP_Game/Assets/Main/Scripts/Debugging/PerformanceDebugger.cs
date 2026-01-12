using UnityEngine;
using UnityEngine.Profiling;
using System.IO;
using System.Text;

public class PerformanceDebugger : MonoBehaviour
{
    [Header("Settings")]
    public bool showOnScreen = true;
    public float updateInterval = 0.5f; // Update stats every 0.5s

    [Header("Live Data (Read Only)")]
    public float currentFPS;
    public long totalMemoryAllocated; // In Megabytes
    public int enemyCount;
    public int towerCount;
    public int particleCount;

    private float _timer;
    private string _logPath;
    private StringBuilder _logBuffer = new StringBuilder();

    void Awake()
    {
        // Keep this alive across scenes if you want
        DontDestroyOnLoad(gameObject);

        // Set up the crash log file path
        _logPath = Path.Combine(Application.persistentDataPath, "CrashLog.txt");
        
        // Clear old log on start
        File.WriteAllText(_logPath, $"--- GAME STARTED AT {System.DateTime.Now} ---\n");
        
        // Listen for Unity Errors
        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        
        // Update FPS every frame for smoothness
        currentFPS = 1.0f / Time.unscaledDeltaTime;

        // Update heavy stats on interval
        if (_timer > updateInterval)
        {
            UpdateHeavyStats();
            _timer = 0f;
        }
    }

    void UpdateHeavyStats()
    {
        // 1. MEMORY (Recorder is safer than generic System calls)
        totalMemoryAllocated = Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;

        // 2. OBJECT COUNTS
        // We use your existing Registries to be super efficient (0 lag cost)
        if (EnemyController.ActiveEnemies != null)
            enemyCount = EnemyController.ActiveEnemies.Count;
        else
            enemyCount = 0;

        if (TowerRegistry.ActiveTowers != null)
            towerCount = TowerRegistry.ActiveTowers.Count;
        else
            towerCount = 0;

        // Projectiles are harder since you don't have a registry for them yet.
        // We can do a slow find, but only do it if lag isn't critical.
        // For now, let's just count total game objects (Heavy operation, but useful for debugging)
        // int totalObjects = FindObjectsOfType<GameObject>().Length; 

        // 3. LOGGING HIGH USAGE
        // If memory spikes over 2GB or FPS drops below 15, log it!
        if (totalMemoryAllocated > 2000 || currentFPS < 15)
        {
            LogToFile($"PERFORMANCE SPIKE: FPS: {currentFPS:F1} | Mem: {totalMemoryAllocated}MB | Enemies: {enemyCount}");
        }
    }

    // This function runs whenever Unity throws an error or exception
    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            string logEntry = $"\n[{System.DateTime.Now.ToLongTimeString()}] [CRITICAL {type}]: {logString}\nStack Trace: {stackTrace}\n";
            
            // Append to the file immediately so if it crashes 1ms later, we have the data
            File.AppendAllText(_logPath, logEntry);
        }
    }

    void LogToFile(string message)
    {
        string line = $"[{System.DateTime.Now.ToLongTimeString()}] {message}\n";
        File.AppendAllText(_logPath, line);
    }

    // Draw a simple box on screen
    void OnGUI()
    {
        if (!showOnScreen) return;

        int w = Screen.width, h = Screen.height;
        GUIStyle style = new GUIStyle();
        Rect rect = new Rect(20, 20, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = currentFPS < 30 ? Color.red : Color.green;

        string text = $"FPS: {currentFPS:0.} | MEM: {totalMemoryAllocated} MB\n" +
                      $"Enemies: {enemyCount} | Towers: {towerCount}";
        
        GUI.Label(rect, text, style);
    }
}