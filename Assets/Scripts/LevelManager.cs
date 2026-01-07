using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct LevelData
{
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
