using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct LevelData
{
    public GameObject mapPrefab; // Prefab of the environment for this level
    public int zombieCount;
    public int humanCount;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Levels")]
    public List<LevelData> levels;
    public int currentLevelIndex = 0;

    [Header("Dependencies")]
    public SpawnManager spawnManager;

    [Header("Runtime Info")]
    public int currentZombiesEaten = 0;
    public int totalZombiesInLevel = 0;

    // Event for UI updates
    public System.Action<float> OnProgressUpdated;
    public System.Action<int> OnLevelChanged; // New event for level text update

    private GameObject currentMapInstance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Optional: if we reload scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartLevel();
    }

    public void StartLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("LevelManager: No levels defined!");
            return;
        }

        // Loop back if we run out of levels
        if (currentLevelIndex >= levels.Count)
        {
            Debug.Log("All levels completed! Looping back to 0.");
            currentLevelIndex = 0;
        }

        LevelData data = levels[currentLevelIndex];
        
        // Notify level change (1-based index for UI)
        OnLevelChanged?.Invoke(currentLevelIndex + 1);

        // --- MAP SWITCHING LOGIC ---
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
        }

        if (data.mapPrefab != null)
        {
            // Spawn at fixed Y position as per user request
            currentMapInstance = Instantiate(data.mapPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            
            // Notify SpawnManager about the new map
            if (spawnManager != null)
            {
                spawnManager.UpdateSpawnPoints(currentMapInstance.transform);
            }
        }
        else
        {
            Debug.LogWarning($"Level {currentLevelIndex + 1} has no Map Prefab assigned! Make sure to assign one.");
        }
        // ---------------------------

        // Override zombie count based on User Request (1.5x Humans)
        int desiredZombieCount = (int)(data.humanCount * 1.5f);
        
        // Reset Progress
        currentZombiesEaten = 0;
        totalZombiesInLevel = desiredZombieCount;
        NotifyProgress();

        // Setup Scene
        if (spawnManager != null)
        {
            spawnManager.ClearScene();
            spawnManager.SpawnLevel(data.humanCount, desiredZombieCount);
        }
    }

    public void OnZombieEaten()
    {
        currentZombiesEaten++;
        NotifyProgress();

        if (currentZombiesEaten >= totalZombiesInLevel)
        {
            Debug.Log("Level Complete!");
            Invoke(nameof(NextLevel), 2f); // Delay for effect
        }
    }

    private void NextLevel()
    {
        // Reward Player
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCoins(20);
        }

        currentLevelIndex++;
        StartLevel();
    }

    private void NotifyProgress()
    {
        if (totalZombiesInLevel > 0)
        {
            float progress = (float)currentZombiesEaten / totalZombiesInLevel;
            OnProgressUpdated?.Invoke(progress);
        }
    }
}
