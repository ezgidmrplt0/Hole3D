using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct LevelData
{
    public GameObject mapPrefab; // Prefab of the environment for this level
    public int zombieCount;
    public int humanCount;
    
    [Header("Special Modes")]
    public bool isHordeLevel; // Eğer true ise, zombiler dip dibe (Horde) olarak spawn olur
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
    public System.Action<int> OnZombieCountChanged; // Event for Zombie Counter UI

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

    [Header("Special Levels")]
    public GameObject simplePlanePrefab; // Kullanıcı dilerse buraya kendi plane prefabını atabilir

    public void StartLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("LevelManager: No levels defined!");
            return;
        }

        // --- INFINITE LEVEL LOGIC ---
        int actualLevelNumber = currentLevelIndex + 1;
        
        // Notify level change
        OnLevelChanged?.Invoke(actualLevelNumber);

        // --- LEVEL TYPE DETERMINATION ---
        bool isSpecialHordeLevel = (actualLevelNumber % 3 == 0);
        
        GameObject mapToSpawn = null;
        int desiredZombieCount = 0;
        int desiredHumanCount = 0;
        bool isHordeMode = false;

        if (isSpecialHordeLevel)
        {
            // --- SPECIAL LEVEL (Her 3 Levelde Bir) ---
            // "Sadece Plane olsun, 30 zombi olsun, dip dibe (Horde) olsun"
            Debug.Log($"*** SPECIAL LEVEL {actualLevelNumber} *** -> Horde Mode Active!");
            
            mapToSpawn = simplePlanePrefab; // Varsa prefab, yoksa null (aşağıda createPrimitive yaparız)
            
            desiredZombieCount = 30; // Sabit 30 zombi
            desiredHumanCount = 0;   // İnsan yok
            isHordeMode = true;      // Dip dibe spawn
        }
        else
        {
            // --- NORMAL LEVEL ---
            LevelData data = levels[currentLevelIndex % levels.Count];
            mapToSpawn = data.mapPrefab;

            // Zombi Sayısı (Level * 5)
            desiredZombieCount = actualLevelNumber * 5;
            desiredHumanCount = data.humanCount; // Level datasından gelen insan sayısı
            isHordeMode = data.isHordeLevel; // Level datasında özel horde ayarı varsa
            
            Debug.Log($"Level {actualLevelNumber}: Spawning {desiredZombieCount} Zombies, {desiredHumanCount} Humans.");
        }

        // --- MAP SWITCHING LOGIC ---
        // Önce temizlik: Eğer eski harita varsa yok et veya kapat
        if (currentMapInstance != null)
        {
            // Eğer mevcut harita bizim değişmez sahnemiz ise (Special Plane), onu YOK ETME, sadece KAPAT.
            if (currentMapInstance == simplePlanePrefab)
            {
                currentMapInstance.SetActive(false);
            }
            else
            {
                // Normal bir level kopyası ise tamamen yok et
                Destroy(currentMapInstance);
            }
            currentMapInstance = null;
        }

        // Yeni haritayı oluştur veya aç
        if (isSpecialHordeLevel)
        {
             // --- SPECIAL LEVEL: SAHNEDEKİ OBJEYİ AÇ ---
             if (simplePlanePrefab != null)
             {
                 currentMapInstance = simplePlanePrefab;
                 currentMapInstance.SetActive(true); // Sadece görünür yap
                 
                 // Pozisyonunu ve rotasyonunu elleme, sahnede nasılsa öyle kalsın.
                 Debug.Log("Special Level: Sahnedeki Plan objesi aktif edildi.");
             }
             else
             {
                 Debug.LogError("HATA: LevelManager -> 'Simple Plane Prefab' boş! Lütfen SAHNEDEKİ objeyi buraya sürükleyin.");
             }
        }
        else if (mapToSpawn != null)
        {
            // --- NORMAL LEVEL: PREFABDAN KOPYA OLUŞTUR ---
            // Orijinal Prefab rotasyonunu koru!
            currentMapInstance = Instantiate(mapToSpawn, Vector3.zero, mapToSpawn.transform.rotation);
        }
        else
        {
             Debug.LogWarning($"Level {actualLevelNumber} has no Map Prefab assigned!");
        }

        // --- GÜVENLİK VE AYARLAR ---
        if (currentMapInstance != null)
        {
            // 1. ZORUNLU TAG VE LAYER AYARI
            // Bu kısım mecburi çünkü Tag olmazsa zombiler spawn olmaz.
            // Ama yeni obje EKLEMİYORUZ, sadece mevcut olana etiket basıyoruz.
            if (!currentMapInstance.CompareTag("Ground")) currentMapInstance.tag = "Ground";
            currentMapInstance.layer = LayerMask.NameToLayer("Default");

            // Alt objeleri de etiketle (Renderer'ı olanları)
            foreach (Transform child in currentMapInstance.GetComponentsInChildren<Transform>())
            {
                if (child.GetComponent<Collider>() != null)
                {
                    child.tag = "Ground";
                }
            }
        }
        
        // --- SETUP SPAWN MANAGER ---
        // Notify SpawnManager about the new map (bounds calculation)
        if (spawnManager != null && currentMapInstance != null)
        {
            spawnManager.UpdateSpawnPoints(currentMapInstance.transform);
        }

        // Reset Progress
        currentZombiesEaten = 0;
        totalZombiesInLevel = desiredZombieCount;
        NotifyProgress();

        // Spawn Enemies
        if (spawnManager != null)
        {
            spawnManager.ClearScene();
            spawnManager.SpawnLevel(desiredHumanCount, desiredZombieCount, isHordeMode);
        }
    }

    public void OnZombieEaten()
    {
        currentZombiesEaten++;
        NotifyProgress();

        if (currentZombiesEaten >= totalZombiesInLevel)
        {
            Debug.Log("Level Complete!");
            // Invoke'u kaldırdık, UI kontrolüne devrettik
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ShowLevelComplete();
            }
            else
            {
                // Fallback (UI Manager yoksa eski usül devam)
                Invoke(nameof(NextLevel), 2f);
            }
        }
    }

    public void NextLevel()
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
            
            // Update Zombie Counter (Remaining Quantity)
            int remaining = totalZombiesInLevel - currentZombiesEaten;
            if (remaining < 0) remaining = 0;
            OnZombieCountChanged?.Invoke(remaining);
        }
    }
}
