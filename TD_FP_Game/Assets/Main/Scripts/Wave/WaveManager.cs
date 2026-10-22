using System;
using UnityEngine;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    public enum WavePhase { Idle, Preparing, Spawning, Fighting, Finished }

    [Header("Wave Configuration")]
    [Tooltip("List of all wave configuration assets for the level, in order.")]
    public WaveConfig[] waves;

    [Header("Endless Mode")]
    [Tooltip("After the last configured wave, loop back to the first wave with scaled stats instead of ending the game.")]
    public bool loopWavesWhenFinished = true;
    [Tooltip("Enemy health multiplier applied per completed loop (1.25 = +25% each loop).")]
    public float healthMultiplierPerLoop = 1.25f;
    [Tooltip("Enemy count multiplier applied per completed loop.")]
    public float countMultiplierPerLoop = 1.2f;
    [Tooltip("Spawn interval multiplier applied per loop (<1 = faster).")]
    [Range(0.25f, 1f)] public float spawnIntervalMultiplierPerLoop = 0.9f;

    [Header("Rewards")]
    [Tooltip("Scrap given to the player every time a wave is cleared.")]
    public int waveClearScrapBonus = 25;
    [Tooltip("Extra scrap per wave number (wave 3 gives bonus + 3 * this).")]
    public int waveClearScrapBonusPerWave = 5;

    [Header("Spawn Points")]
    [Tooltip("List of transforms where enemies can spawn.")]
    public Transform[] spawnPoints;

    [Header("Core Reference")]
    [Tooltip("The central core/base the enemies are targeting (informational, enemies find it by tag).")]
    public Transform coreTarget;

    // --- Events (HUD etc.) ---
    /// <summary>waveNumber (1-based), seconds of preparation</summary>
    public event Action<int, float> OnPreparationStarted;
    /// <summary>waveNumber (1-based), config</summary>
    public event Action<int, WaveConfig> OnWaveStarted;
    /// <summary>waveNumber (1-based)</summary>
    public event Action<int> OnWaveCleared;
    public event Action OnAllWavesCleared;
    /// <summary>enemies alive</summary>
    public event Action<int> OnEnemyCountChanged;
    public event Action<WavePhase> OnPhaseChanged;

    public WavePhase Phase { get; private set; } = WavePhase.Idle;
    /// <summary>1-based wave number as shown to the player. Keeps counting up across loops.</summary>
    public int CurrentWaveNumber { get; private set; }
    public int LoopCount { get; private set; }
    public int EnemiesAlive { get; private set; }
    public float PreparationTimeRemaining { get; private set; }
    public float CurrentHealthMultiplier => Mathf.Pow(healthMultiplierPerLoop, LoopCount);

    private int _currentWaveIndex = 0;
    private int _nextSpawnPoint = 0;
    private bool _shuttingDown;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        _shuttingDown = true;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (waves != null && waves.Length > 0)
        {
            StartCoroutine(WaveSequenceRoutine());
        }
        else
        {
            Debug.LogError("No Wave Configurations found! Please assign WaveConfig Scriptable Objects.", this);
        }
    }

    IEnumerator WaveSequenceRoutine()
    {
        while (true)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

            WaveConfig currentWave = waves[_currentWaveIndex];
            if (currentWave == null)
            {
                Debug.LogError($"Wave slot {_currentWaveIndex} is empty, skipping.", this);
                yield return null; // never spin without yielding
                if (!AdvanceWaveIndex()) break;
                continue;
            }

            CurrentWaveNumber++;

            // 1. Preparation Phase
            SetPhase(WavePhase.Preparing);
            PreparationTimeRemaining = currentWave.preparationTime;
            OnPreparationStarted?.Invoke(CurrentWaveNumber, PreparationTimeRemaining);
            while (PreparationTimeRemaining > 0f)
            {
                PreparationTimeRemaining -= Time.deltaTime;
                yield return null;
            }
            PreparationTimeRemaining = 0f;

            // 2. Spawn Phase
            SetPhase(WavePhase.Spawning);
            OnWaveStarted?.Invoke(CurrentWaveNumber, currentWave);
            yield return StartCoroutine(SpawnWave(currentWave));

            // 3. Fight Phase: wait until everything (including Queen minions) is dead
            SetPhase(WavePhase.Fighting);
            while (EnemiesAlive > 0)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
                yield return null;
            }

            // 4. Cleared
            OnWaveCleared?.Invoke(CurrentWaveNumber);
            GrantWaveReward();

            if (!AdvanceWaveIndex()) break;
        }

        SetPhase(WavePhase.Finished);
        OnAllWavesCleared?.Invoke();
        if (GameManager.Instance != null) GameManager.Instance.Victory();
        else Debug.Log("All waves defeated! Game Over (Win).");
    }

    /// <summary>Moves to the next wave index. Returns false when the sequence is over.</summary>
    private bool AdvanceWaveIndex()
    {
        _currentWaveIndex++;
        if (_currentWaveIndex < waves.Length) return true;

        if (!loopWavesWhenFinished) return false;
        _currentWaveIndex = 0;
        LoopCount++;
        return true;
    }

    IEnumerator SpawnWave(WaveConfig wave)
    {
        float healthMul = CurrentHealthMultiplier;
        float countMul = Mathf.Pow(countMultiplierPerLoop, LoopCount);
        float intervalMul = Mathf.Pow(spawnIntervalMultiplierPerLoop, LoopCount);

        foreach (var group in wave.enemyGroups)
        {
            if (group == null || group.enemyType == null || group.enemyType.enemyPrefab == null)
            {
                Debug.LogWarning($"{wave.waveName}: enemy group is missing an EnemyData or prefab, skipping.", this);
                continue;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(group.count * countMul));
            float interval = Mathf.Max(0.02f, group.spawnInterval * intervalMul);

            for (int i = 0; i < count; i++)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

                SpawnEnemy(group.enemyType, healthMul);
                yield return new WaitForSeconds(interval);
            }
            yield return new WaitForSeconds(group.spawnPauseInterval);
        }
    }

    private EnemyController SpawnEnemy(EnemyData data, float healthMultiplier)
    {
        GameObject enemyObj = Instantiate(data.enemyPrefab, GetNextSpawnPoint(), Quaternion.identity);
        EnemyController enemy = enemyObj.GetComponent<EnemyController>();
        if (enemy != null)
        {
            // Registers the enemy with us (EnemiesAlive++) synchronously, before its first Update.
            enemy.Initialize(data, this, healthMultiplier);
        }
        else
        {
            Debug.LogError($"Enemy prefab '{data.enemyPrefab.name}' has no EnemyController!", data);
            Destroy(enemyObj);
        }
        return enemy;
    }

    // --- Enemy bookkeeping ---

    /// <summary>Called by EnemyController.Initialize for every enemy, including Queen minions.</summary>
    public void RegisterEnemy(EnemyController enemy)
    {
        if (_shuttingDown) return;
        EnemiesAlive++;
        OnEnemyCountChanged?.Invoke(EnemiesAlive);
    }

    /// <summary>Called by EnemyController when it dies (or is destroyed).</summary>
    public void EnemyDestroyed()
    {
        if (_shuttingDown) return;
        EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
        OnEnemyCountChanged?.Invoke(EnemiesAlive);
    }

    private void GrantWaveReward()
    {
        int reward = waveClearScrapBonus + waveClearScrapBonusPerWave * CurrentWaveNumber;
        if (reward > 0 && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddScrap(reward);
        }
    }

    private void SetPhase(WavePhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
    }

    /// <summary>Round-robins through spawn points so a wave is spread out instead of clumping at random.</summary>
    Vector3 GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("Spawn points are not assigned!", this);
            return transform.position;
        }
        Transform point = spawnPoints[_nextSpawnPoint % spawnPoints.Length];
        _nextSpawnPoint++;
        return point != null ? point.position : transform.position;
    }
}
