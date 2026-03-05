using UnityEngine;
using System.Collections.Generic;

public enum MissionType
{
    None,
    EatZombies,
    SaveHumans
}

[System.Serializable]
public struct LevelData
{
    public GameObject mapPrefab; // Prefab of the environment for this level
    public int zombieCount;
    public int humanCount;
    public int levelDuration; // Duration in seconds (0 = use default)
    
    [Header("Special Modes")]
    public bool isHordeLevel; // Eğer true ise, zombiler dip dibe (Horde) olarak spawn olur

    [Header("Mission Settings")]
    public MissionType missionType;
    public int missionTarget;
    public int missionReward;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Levels")]
    public List<LevelData> levels;
    public int currentLevelIndex = 0;

    [Header("Level Settings")]
    public int baseZombieCount = 10;
    public int zombiesPerLevel = 2; // Her level kaç zombi artsın (Düşürüldü: 5 -> 2)
    public int baseHumanCount = 15;  // Başlangıç insan sayısı
    public int humansPerLevel = 2;  // Her level kaç insan artsın (Düşürüldü: 4 -> 2)
    public float minHumanToZombieRatio = 0.75f; // Oran aynen korundu (%75)

    [Header("Dependencies")]
    public SpawnManager spawnManager;

    [Header("Runtime Info")]
    public int currentZombiesEaten = 0;

    public int totalZombiesInLevel = 0;
    
    [Header("Timer Settings")]
    public float defaultLevelTime = 60f;
    public float currentLevelTimeRemaining;
    private float initialLevelTime; // Yıldız hesaplaması için lazım
    private float performanceRemainingTime; // Fever başlamadan önceki asıl süre (Yıldız hesabı için)
    
    [Header("Human Limit Settings")]
    public int totalHumansInLevel = 0; // Level'da toplam kaç insan var
    public int currentHumansRemaining = 0; // Kalan insan sayısı
    public int currentHumansEaten = 0; // Hole tarafından yenilen (fail condition için)
    private float levelStartRealtime = 0f; // Restart sonrası eski callback'lerin fail tetiklemesini engeller
    
    // Normal level index (horde levellar bu sayacı artırmaz)
    public int normalLevelIndex = 0;

    // Event for UI updates
    public System.Action<float> OnProgressUpdated;
    public System.Action<int> OnLevelChanged; // New event for level text update
    public System.Action<int> OnZombieCountChanged; // Event for Zombie Counter UI
    public System.Action<int> OnHumanCountChanged; // New Event for Human Counter UI

    [Header("Mission Tracking")]
    public int currentMissionProgress = 0;
    public bool isMissionCompleted = false;
    public System.Action<MissionType, int, int> OnMissionUpdated; // Type, Current, Target

    private GameObject currentMapInstance;
    public bool isCurrentLevelSpecial = false; // Flag for special level fever logic
    private int targetDisplayCount = -1; // -1 means use real count

    private const string PREF_LEVEL_INDEX = "CurrentLevelIndex";
    private const string PREF_NORMAL_LEVEL_INDEX = "NormalLevelIndex";
    private const string PREF_PROGRESS_MIGRATION_VERSION = "ProgressionMigrationVersion";
    private const int CURRENT_PROGRESS_MIGRATION_VERSION = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // --- INSPECTOR OVERRIDE SAFETY ---
            // Eğer Unity Inspector'da eski değerler (5, 2 vs) kaldıysa kod içinden düzeltiyoruz
            if (baseHumanCount < 15) baseHumanCount = 15;
            if (humansPerLevel < 2) humansPerLevel = 2;
            if (minHumanToZombieRatio < 0.75f) minHumanToZombieRatio = 0.75f;
            
#if UNITY_EDITOR
            Debug.Log($"[LevelManager] Inspector Overrides applied: HumanBase:{baseHumanCount}, Scaling:{humansPerLevel}, Ratio:{minHumanToZombieRatio}");
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Reset indexes at game start
        // Load saved level or default to 0
        currentLevelIndex = PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0);
        normalLevelIndex = PlayerPrefs.GetInt(PREF_NORMAL_LEVEL_INDEX, 0);

        ApplyProgressionMigrationIfNeeded();
        
#if UNITY_EDITOR
        Debug.Log($"[LevelManager] Loaded Progress - Level Index: {currentLevelIndex}, Normal Index: {normalLevelIndex}");
#endif
        
        StartLevel();
        StartCoroutine(SafetyCheckLoop());
    }

    private void ApplyProgressionMigrationIfNeeded()
    {
        int migrationVersion = PlayerPrefs.GetInt(PREF_PROGRESS_MIGRATION_VERSION, 0);
        if (migrationVersion >= CURRENT_PROGRESS_MIGRATION_VERSION) return;

        if (currentLevelIndex < 0) currentLevelIndex = 0;
        if (normalLevelIndex < 0) normalLevelIndex = 0;

        bool hasNormalKey = PlayerPrefs.HasKey(PREF_NORMAL_LEVEL_INDEX);

        // Eski sürümlerde index bazen şişebiliyor; normal progression ile uyuşmazsa düzelt.
        if (hasNormalKey)
        {
            int expectedCurrentFromNormal = GetCurrentIndexFromCompletedNormalLevels(normalLevelIndex);
            int tolerance = 5;
            if (currentLevelIndex > expectedCurrentFromNormal + tolerance)
            {
                currentLevelIndex = expectedCurrentFromNormal;
            }
            else if (normalLevelIndex > currentLevelIndex)
            {
                normalLevelIndex = GetCompletedNormalLevelsFromCurrentIndex(currentLevelIndex);
            }
        }
        else
        {
            // Eski kayıtta NormalLevelIndex yoksa mevcut levelden türet.
            normalLevelIndex = GetCompletedNormalLevelsFromCurrentIndex(currentLevelIndex);
        }

        PlayerPrefs.SetInt(PREF_LEVEL_INDEX, currentLevelIndex);
        PlayerPrefs.SetInt(PREF_NORMAL_LEVEL_INDEX, normalLevelIndex);
        PlayerPrefs.SetInt(PREF_PROGRESS_MIGRATION_VERSION, CURRENT_PROGRESS_MIGRATION_VERSION);
        PlayerPrefs.Save();
    }

    private int GetCompletedNormalLevelsFromCurrentIndex(int currentIndex)
    {
        if (currentIndex <= 0) return 0;

        int completedAllLevels = currentIndex;
        int completedSpecialLevels = completedAllLevels / 3;
        int completedNormalLevels = completedAllLevels - completedSpecialLevels;

        return Mathf.Max(0, completedNormalLevels);
    }

    private int GetCurrentIndexFromCompletedNormalLevels(int completedNormalLevels)
    {
        if (completedNormalLevels <= 0) return 0;

        int completedAllLevels = 0;
        int countedNormalLevels = 0;

        while (countedNormalLevels < completedNormalLevels)
        {
            completedAllLevels++;
            if (completedAllLevels % 3 != 0)
            {
                countedNormalLevels++;
            }
        }

        return Mathf.Max(0, completedAllLevels);
    }



    [Header("Special Levels")]
    public GameObject simplePlanePrefab; // Kullanıcı dilerse buraya kendi plane prefabını atabilir

    // --- SAFETY CHECK ---
    // Eğer zombiler bir şekilde (yutulmadan) yok olursa oyun tıkanmasın diye
    // Sahnede hiç zombi kalmadıysa leveli bitir.
    System.Collections.IEnumerator SafetyCheckLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f); // Her 1 saniyede bir kontrol et

            // Eğer Fever Modu zaten çalışıyorsa, safety check yapma (Panel açılmasın diye)
            if (isFeverSequenceActive) continue;

            if (totalZombiesInLevel > 0 && !GameFlowManager.Instance.IsLevelTransitioning)
            {
                // Sahnedeki gerçek zombileri say
                int currentRealCount = GameObject.FindGameObjectsWithTag("Zombie").Length;
                
                // Eğer sahnede hiç zombi kalmadıysa ama biz hala oyun devam ediyor sanıyorsak
                if (currentRealCount == 0)
                {
                    // Belki de "currentZombiesEaten" senkronize olamadı.
                    // Zorla tamamlama yapıyoruz ama FEVER MODE ile uyumlu olmalı.
                    
                    
                    // Count'u eşitle
                    currentZombiesEaten = totalZombiesInLevel;
                    OnZombieCountChanged?.Invoke(0);

                    // Normal tamamlama fonksiyonunu çağır (Bu fonksiyon Fever Mode'u tetikler)
                    CheckLevelComplete();
                    
                    // Eski direkt bitirme kodu KALDIRILDI.
                    // Çünkü o direkt paneli açıyordu.
                }
            }
        }
    }


    private void Update()
    {
        // Timer Logic
        // Fever Modunda da çalışsın (isFeverSequenceActive kontrolünü kaldırdık)
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsGameActive)
        {
            currentLevelTimeRemaining -= Time.deltaTime;
            
            // Update UI
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTimer(currentLevelTimeRemaining);
            }

            // Check Time Up
            if (currentLevelTimeRemaining <= 0)
            {
                currentLevelTimeRemaining = 0; // Clamp
                
                // Fever Moddaysa süre bitince Level Fail OLMASIN (Zaten callback ile bitecek)
                if (!isFeverSequenceActive)
                {
#if UNITY_EDITOR
                    Debug.Log("Time's Up! Level Failed.");
#endif
                    GameFlowManager.Instance.ShowRetry();
                }
            }
        }
    }

    public void StartLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("LevelManager: No levels defined!");
            return;
        }

        // Level/retry başlangıcında kritik sayaçları hemen sıfırla.
        // Mobilde Destroy gecikmesiyle gelen eski callback'ler bu sayede tekrar Lose açamaz.
        levelStartRealtime = Time.realtimeSinceStartup;
        totalHumansInLevel = 0;
        currentHumansRemaining = 0;
        currentHumansEaten = 0;
        currentZombiesEaten = 0;
        
        // Reset Logic
        isFeverSequenceActive = false;
        
        // HOLE RESET - Fever sonrası büyük kalmayı önle
        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        if (hole != null)
        {
            hole.ResetLevelState();
        }
        
        // Skills reset per level
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ResetLevelPurchases(); // Fiyatları güncelle ve skilleri sıfırla
        }

        // --- MISSION RESET ---
        currentMissionProgress = 0;
        isMissionCompleted = false;

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
            // "Eat Everything Mode Hazırlık": Başlangıçta sadece 30 zombi.
            // Hepsini yiyince Fever Mode açılacak ve O ZAMAN 60 tane daha gelecek.
#if UNITY_EDITOR
            Debug.Log($"*** SPECIAL LEVEL {actualLevelNumber} *** -> Standard Start (30 Zombies), Mega Fever waiting...");
#endif
            
            // HORDE BANNER GÖSTER
            UIManager uiManager = FindObjectOfType<UIManager>();
            if (uiManager != null)
            {
                uiManager.ShowHordeBanner();
            }
            
            mapToSpawn = simplePlanePrefab; // Varsa prefab, yoksa null
            
            desiredZombieCount = 30; // Start with 30 zombies
            targetDisplayCount = -1; // Show real count (30)
            
            desiredHumanCount = 0;   // No humans
            isHordeMode = false;     // Scattered
            
            // Set Timer for Special Level
            currentLevelTimeRemaining = defaultLevelTime;
            initialLevelTime = currentLevelTimeRemaining; // EKLENDİ: Yıldız hesabı için başlangıç süresini kaydet
            if (UIManager.Instance != null) UIManager.Instance.UpdateTimer(currentLevelTimeRemaining);
            
            isCurrentLevelSpecial = true; // Flag set
        }
        else
        {
            // --- NORMAL LEVEL ---
            isCurrentLevelSpecial = false;
            targetDisplayCount = -1;

            // Normal level için normalLevelIndex kullan
            LevelData data = levels[normalLevelIndex % levels.Count];
            
            // Temel Zombi Sayısı Hesabı (Inspector değerlerini kullan)
            int calculatedZombies = baseZombieCount + (zombiesPerLevel * currentLevelIndex);

            // Horde Level Kontrolü (Inspector'dan işaretli mi?)
            if (data.isHordeLevel)
            {
                // HORDE MODE: Zombi sayısı fazla (2.5 katı), İNSAN YOK (0)
                // Kullanıcı isteği: Horde levelleri hariç %40 kuralı -> Yani Horde'da kural yok (0 insan)
                desiredZombieCount = Mathf.CeilToInt(calculatedZombies * 2.5f);
                desiredHumanCount = 0; 
                isHordeMode = true;
                
#if UNITY_EDITOR
                Debug.Log($"[LevelManager] HORDE LEVEL (Index {currentLevelIndex})! Zombies: {desiredZombieCount}, Humans: 0");
#endif
            }
            else
            {
                // NORMAL MODE: Zombi sayısı normal
                desiredZombieCount = calculatedZombies;
                
                // --- ACCURATE RATIO CALCULATION (Including Cages) ---
                // Kafeslerden gelen gizli zombileri de hesaba katmalıyız
                int totalExpectedZombies = desiredZombieCount;
                if (spawnManager != null)
                {
                    totalExpectedZombies += (spawnManager.cageCount * spawnManager.zombiesPerCage);
                }

                // İNSAN SAYISI GÜNCELLEMESİ: baseHumanCount + (humansPerLevel * currentLevelIndex)
                desiredHumanCount = baseHumanCount + (humansPerLevel * currentLevelIndex);
                
                // --- SAFETY BALANCE RATIO ---
                // Gerçek toplam zombi sayısına göre insan sayısını koru
                int safetyMinHumans = Mathf.CeilToInt(totalExpectedZombies * minHumanToZombieRatio);
                if (desiredHumanCount < safetyMinHumans)
                {
                    desiredHumanCount = safetyMinHumans;
                }

                // En en az 5 kuralı (Oyunun başında boş kalmasın)
                if (desiredHumanCount < 5) desiredHumanCount = 5;
                
                isHordeMode = false;
#if UNITY_EDITOR
                Debug.Log($"[LevelManager] Normal Level (Index {currentLevelIndex}): RequestedZombies: {desiredZombieCount}, TotalZombies(w/Cages): {totalExpectedZombies}, CalculatedHumans: {desiredHumanCount}");
#endif
            }

            mapToSpawn = data.mapPrefab;
            
            // Calculate dynamic duration (Base + Increase per level)
            float calculatedDuration = defaultLevelTime + (currentLevelIndex * 2); 
            
            // Set Timer (Priority: LevelData > Calculated > Default)
            currentLevelTimeRemaining = data.levelDuration > 0 ? data.levelDuration : calculatedDuration;
            initialLevelTime = currentLevelTimeRemaining; // Kaydet
            
            if (UIManager.Instance != null) UIManager.Instance.UpdateTimer(currentLevelTimeRemaining);
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
             }
             else
             {
                 Debug.LogError("HATA: LevelManager -> 'simplePlanePrefab' (Ground) is NULL! Please assign the Scene Object in the inspector.");
             }
        }
        else
        {
            // --- NOT A SPECIAL LEVEL: ENSURE GROUND IS OFF ---
            if (simplePlanePrefab != null)
            {
                simplePlanePrefab.SetActive(false);
            }

            if (mapToSpawn != null)
            {
                // --- NORMAL LEVEL: Create copy from Prefab ---
                currentMapInstance = Instantiate(mapToSpawn, Vector3.zero, mapToSpawn.transform.rotation);
            }
            else
            {
                 Debug.LogWarning($"Level {actualLevelNumber} has no Map Prefab assigned! Running WITHOUT fallback ground.");
            }
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

        // Spawn Enemies
        if (spawnManager != null)
        {
            try
            {
                spawnManager.ClearScene();
                spawnManager.SpawnLevel(desiredHumanCount, desiredZombieCount, isHordeMode);
                
                // --- Start Skill Pickup Spawning ---
                spawnManager.StartSkillSpawning();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelManager] Error during SpawnLevel: {e.Message}\n{e.StackTrace}");
                // Hata olsa bile sayıma devam etmeye çalış
            }
            
            // --- ZOMBIE SAYIMI: Frame sonu bekle ---
            StartCoroutine(CountZombiesAfterFrame(desiredZombieCount));
        }
        else
        {
            // SpawnManager yoksa fallback
            totalZombiesInLevel = desiredZombieCount;
            currentZombiesEaten = 0;
            currentHumansEaten = 0;
            NotifyProgress();
            OnHumanCountChanged?.Invoke(currentHumansEaten);
        }
    }
    
    private System.Collections.IEnumerator CountZombiesAfterFrame(int desiredCount)
    {
        // Frame sonunu bekle - ZombieAI.Start() çalışsın, tag'ler düzgün atansın
        yield return new WaitForEndOfFrame();
        
        // Ek güvenlik: Bir frame daha bekle
        yield return null;
        
        try
        {
            // --- USER REQUEST: Counter depend on ACTUAL spawned count ---
            int realZombieCount = 0;
            
            // Önce tag ile say
            GameObject[] taggedZombies = GameObject.FindGameObjectsWithTag("Zombie");
            realZombieCount = taggedZombies != null ? taggedZombies.Length : 0;
            
            // Yedek: Eğer hiç bulamadıysak, ZombieAI component'i ara
            if (realZombieCount == 0)
            {
                ZombieAI[] zombieComponents = GameObject.FindObjectsOfType<ZombieAI>();
                realZombieCount = zombieComponents != null ? zombieComponents.Length : 0;
                Debug.LogWarning($"LevelManager: No tagged zombies found, using component count: {realZombieCount}");
            }
            
            // --- İNSAN SAYIMI ---
            GameObject[] taggedHumans = GameObject.FindGameObjectsWithTag("Human");
            totalHumansInLevel = taggedHumans != null ? taggedHumans.Length : 0;
            currentHumansRemaining = totalHumansInLevel;
            
#if UNITY_EDITOR
            Debug.Log($"LevelManager: Zombies - Desired {desiredCount}, Actual {realZombieCount}. Humans: {totalHumansInLevel}");
#endif
            
            totalZombiesInLevel = realZombieCount;

            // --- Calculate Ratio for UI ---
            // Eğer hiç spawn olamadıysa (0), ratio patlamasın
            if (totalZombiesInLevel <= 0)
            {
                // Fallback: Hedeflenen sayıyı göster (Bug gizleme)
                totalZombiesInLevel = desiredCount > 0 ? desiredCount : 10;
                Debug.LogWarning("LevelManager: 0 Zombie spawned! Using desired count for UI to prevent lock.");
            }

            if (targetDisplayCount > 0)
            {
                displayRatio = (float)targetDisplayCount / totalZombiesInLevel;
            }
            else
            {
                displayRatio = 1f;
            }

            // Reset Progress
            currentZombiesEaten = 0;
            currentHumansEaten = 0; 
            
            NotifyProgress();
            // Update Human UI - Kalan insan sayısını göster
            OnHumanCountChanged?.Invoke(currentHumansRemaining);

            // --- MISSION INITIAL UI ---
            int levelDataIndex = isCurrentLevelSpecial ? -1 : (normalLevelIndex % levels.Count);
            if (levelDataIndex != -1)
            {
                LevelData data = levels[levelDataIndex];
                if (data.missionType != MissionType.None)
                {
                    OnMissionUpdated?.Invoke(data.missionType, 0, data.missionTarget);
                }
                else
                {
                    OnMissionUpdated?.Invoke(MissionType.None, 0, 0);
                }
            }
            else
            {
                // Special level or -1 index -> Hide mission
                OnMissionUpdated?.Invoke(MissionType.None, 0, 0);
            }
        }
        catch (System.Exception e)
        {
             Debug.LogError($"[LevelManager] Critical Error in CountZombies: {e.Message}");
             // UI'yi en azından sıfırla
             OnZombieCountChanged?.Invoke(0);
        }
    }

    // Zombi tarafından yenilen insan
    public void OnHumanEatenByZombie()
    {
        currentHumansRemaining--;
        if (currentHumansRemaining < 0) currentHumansRemaining = 0;
        
        
        // UI güncelle
        OnHumanCountChanged?.Invoke(currentHumansRemaining);
        
        // --- MISSION PROGRESS: SAVE HUMANS ---
        UpdateMissionProgress(MissionType.SaveHumans);

        // Counter 0'a düştüyse Game Over
        CheckHumanGameOver();
    }

    // Hole tarafından yenilen insan
    public void OnHumanEaten()
    {
        // Fever modunda insan limitinden etkilenme
        if (isFeverSequenceActive) return;

        currentHumansRemaining--;
        if (currentHumansRemaining < 0) currentHumansRemaining = 0;
        
        OnHumanCountChanged?.Invoke(currentHumansRemaining);

        // --- MISSION PROGRESS: SAVE HUMANS ---
        // If an extra human dies, we can't "save" them, but since the requirement is "at least X", 
        // we check the progress at the end of the level.
        // However, we can update the UI here.
        UpdateMissionProgress(MissionType.SaveHumans);

        // Counter 0'a düştüyse Game Over
        CheckHumanGameOver();
    }
    
    private void CheckHumanGameOver()
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsLevelTransitioning) return;

        // Her level/retry başlangıcından sonra kısa bir grace süresi bırak.
        // Eski objelerin geç callback'leri bu sırada fail tetiklemesin.
        if (Time.realtimeSinceStartup - levelStartRealtime < 2.0f) return;

        // Tüm insanlar yendi mi?
        if (currentHumansRemaining <= 0 && totalHumansInLevel > 0)
        {
#if UNITY_EDITOR
            Debug.Log("Game Over! All humans are gone.");
#endif
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ShowRetry();
            }
        }
    }

    public void RestartCurrentLevel()
    {
        // Level indexini artırmadan aynı leveli tekrar başlat
        StartLevel();
    }

    public void OnZombieEaten()
    {
        currentZombiesEaten++;
        NotifyProgress(); // UI güncelle

        // Bu kontrolü SafetyLoop da yapıyor ama anlık tepki için burada da dursun.
        // Ancak > yerine >= kontrolü çoktan yapıldığı için burayı basitleştiriyoruz.
        
        CheckLevelComplete(); // Tek bir yerde kontrol

        // --- MISSION PROGRESS: EAT ZOMBIES ---
        UpdateMissionProgress(MissionType.EatZombies);
    }

    private void UpdateMissionProgress(MissionType type)
    {
        int levelDataIndex = isCurrentLevelSpecial ? -1 : (normalLevelIndex % levels.Count);
        if (levelDataIndex == -1) return;

        LevelData data = levels[levelDataIndex];
        if (data.missionType != type) return;

        if (type == MissionType.EatZombies)
        {
            currentMissionProgress = currentZombiesEaten;
        }
        else if (type == MissionType.SaveHumans)
        {
            // For SaveHumans, progress is how many are currently alive
            currentMissionProgress = currentHumansRemaining;
        }

        if (currentMissionProgress >= data.missionTarget && !isMissionCompleted)
        {
            if (type == MissionType.EatZombies)
            {
                // Instant complete for eating
                isMissionCompleted = true;
#if UNITY_EDITOR
                Debug.Log("Mission Completed: " + data.missionType);
#endif
            }
            // SaveHumans is checked at the end of level
        }

        OnMissionUpdated?.Invoke(data.missionType, currentMissionProgress, data.missionTarget);
    }

    public int GetStarCount()
    {
        if (initialLevelTime <= 0) return 3; // Güvenlik

        // Fever mode timer'ı ezdiği için, fever başlamadan hemen önce kaydettiğimiz performanceRemainingTime'ı kullanıyoruz.
        float ratio = performanceRemainingTime / initialLevelTime;
        
        // Örnek Mantık:
        // %60+ süre kaldıysa -> 3 Yıldız
        // %30+ süre kaldıysa -> 2 Yıldız
        // %0+ süre kaldıysa -> 1 Yıldız
        if (ratio >= 0.6f) return 3;
        if (ratio >= 0.3f) return 2;
        return 1;
    }

    private bool isFeverSequenceActive = false;
    
    // --- FEVER MODE INTEGRATION ---
    private void CheckLevelComplete()
    {
        if (currentZombiesEaten >= totalZombiesInLevel)
        {
             // Already ending?
             if (isFeverSequenceActive || (GameFlowManager.Instance != null && GameFlowManager.Instance.IsLevelTransitioning)) return;

             // Start Fever Sequence
             StartCoroutine(FeverSequenceRoutine());
        }
    }

    private System.Collections.IEnumerator FeverSequenceRoutine()
    {
        isFeverSequenceActive = true;
        
        // --- PERFORMANCE CAPTURE FOR STARS ---
        // Timer Fever modda overwrite edilmeden önce mevcut süreyi yıldız hesabı için kaydet.
        performanceRemainingTime = currentLevelTimeRemaining;

#if UNITY_EDITOR
             Debug.Log($"Level Quota Met! Performance Time: {performanceRemainingTime}. Starting FEVER MODE.");
#endif

        // --- SPECIAL LEVEL LOGIC: EAT EVERYTHING SPAWN ---
        if (isCurrentLevelSpecial)
        {
#if UNITY_EDITOR
             Debug.Log(">>> SPECIAL LEVEL FEVER: Spawning 75 EXTRA Zombies for Eat Everything Mode! <<<");
#endif
             if (spawnManager != null)
             {
                 // 0 İnsan, 75 Zombi, HordeMode=False (Scattered)
                 spawnManager.SpawnLevel(0, 75, false);
                 
                 // Not: Bu yeni zombileri "totalZombiesInLevel"a eklemiyoruz çünkü level zaten bitti sayılıyor.
                 // Sadece görsel ve ekstra puan/haz için varlar.
             }
        }

        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        bool feverStarted = false;

        if (hole != null)
        {
             // 10 saniye Fever Mode
             float feverDuration = 10.0f;
             
             // --- TIMER UPDATE FOR FEVER ---
             // Timer'ı Fever süresine ayarla
             currentLevelTimeRemaining = feverDuration;
             
             hole.ActivateFeverMode(feverDuration, () => 
             {
                 // Callback: Fever bitti, leveli bitir
                 FinishLevel();
             });
             feverStarted = true;
        }

        if (!feverStarted)
        {
             // Hole bulunamazsa direkt bitir
             FinishLevel();
        }
        
        yield return null;
    }

    private void FinishLevel()
    {
#if UNITY_EDITOR
        Debug.Log("Level Complete! (Post-Fever)");
#endif
        
        // Stop skill pickup spawning
        if (spawnManager != null)
        {
            spawnManager.StopSkillSpawning();
        }
            
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ShowLevelComplete();
        }
        else
        {
            // Fallback (UI Manager yoksa eski usül devam)
            CancelInvoke(nameof(NextLevel));
            Invoke(nameof(NextLevel), 2f);
        }
    }


    public void NextLevel()
    {
        // Reward Player
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCoins(20);

            // --- MISSION REWARD ---
            int levelDataIndex = isCurrentLevelSpecial ? -1 : ((currentLevelIndex - 1) % levels.Count); 
            // -1 because currentLevelIndex was already incremented in NextLevel()
            
            // Wait, NextLevel() increments it. So we need the index BEFORE increment.
            // Let's use a better way. 
        }

        // Eğer şu anki level special (horde) DEĞİLSE, normalLevelIndex'i artır
        int actualLevelNumber = currentLevelIndex + 1;
        bool wasSpecialLevel = (actualLevelNumber % 3 == 0);
        
        // --- MISSION FINAL CHECK & REWARD ---
        if (!wasSpecialLevel)
        {
            LevelData data = levels[normalLevelIndex % levels.Count];
            if (data.missionType != MissionType.None)
            {
                bool success = false;
                if (data.missionType == MissionType.EatZombies) success = currentZombiesEaten >= data.missionTarget;
                else if (data.missionType == MissionType.SaveHumans) success = currentHumansRemaining >= data.missionTarget;

                if (success)
                {
#if UNITY_EDITOR
                    Debug.Log($"[Mission] Level Mission Successful! Reward: {data.missionReward}");
#endif
                    if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(data.missionReward);
                }
            }
        }

        if (!wasSpecialLevel)
        {
            normalLevelIndex++;
        }

        currentLevelIndex++;
        
        // Save Progress
        PlayerPrefs.SetInt(PREF_LEVEL_INDEX, currentLevelIndex);
        PlayerPrefs.SetInt(PREF_NORMAL_LEVEL_INDEX, normalLevelIndex);
        PlayerPrefs.Save();

        StartLevel();
    }

    private float displayRatio = 1f; // Ratio for UI counter scaling

    private void NotifyProgress()
    {
        if (totalZombiesInLevel > 0)
        {
            float progress = (float)currentZombiesEaten / totalZombiesInLevel;
            OnProgressUpdated?.Invoke(progress);
            
            // Update Zombie Counter (Remaining Quantity)
            int remainingReal = totalZombiesInLevel - currentZombiesEaten;
            if (remainingReal < 0) remainingReal = 0;
            
            // Apply display ratio for "Fake" count (e.g. 30 real -> 75 display)
            int remainingDisplay = Mathf.CeilToInt(remainingReal * displayRatio);
            
            OnZombieCountChanged?.Invoke(remainingDisplay);
        }
    }
}
