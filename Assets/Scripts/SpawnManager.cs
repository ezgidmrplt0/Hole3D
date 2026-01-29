using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Prefabs")]
    [Tooltip("List of Human prefabs to spawn.")]
    public List<GameObject> humanPrefabs;
    [Tooltip("List of Zombie prefabs to spawn.")]
    public List<GameObject> zombiePrefabs;



    [Header("Spawn Points")]
    [Tooltip("Drag empty GameObjects here to define where Humans spawn.")]
    public List<Transform> humanSpawnPoints;
    [Tooltip("Drag empty GameObjects here to define where Zombies spawn.")]
    public List<Transform> zombieSpawnPoints;

    [Header("Spawn Settings")]
    [Tooltip("Number of humans to spawn.")]
    public int humanCount = 10;
    [Tooltip("Number of zombies to spawn.")]
    public int zombieCount = 20;
    [Tooltip("Radius around the spawn point to place characters.")]
    public float spawnRadius = 6f;
    
    [Header("Ground Detection")]
    [Tooltip("Zemin Y seviyesi (otomatik bulunur)")]
    public float groundY = 0f;
    private bool groundYDetected = false;
    private Bounds currentSpawnBounds; // Spawn sınırları

    [Header("Raycast & Ground")]
    [Tooltip("Y offset for the raycast start position.")]
    public float raycastHeight = 10f;
    [Tooltip("Layer mask to detect ground.")]
    public LayerMask groundLayer;
    [Tooltip("Offset to add to the ground height when spawning.")]
    public float spawnHeightOffset = 0f;

    [Header("Collision Check")]
    [Tooltip("Layer mask for obstacles to avoid spawning inside.")]
    public LayerMask obstacleLayer;
    [Tooltip("Radius to check for existing objects around spawn point.")]
    public float collisionCheckRadius = 1f;
    [Tooltip("Minimum distance between spawned characters.")]
    public float minSpawnDistance = 1.5f;
    [Tooltip("Maximum attempts to find a valid position per character.")]
    public int maxSpawnAttempts = 30;

    private List<Vector3> spawnedPositions = new List<Vector3>();

    // Public method called by LevelManager
    public void ClearScene()
    {
        // Find all existing characters and destroy them
        var humans = GameObject.FindGameObjectsWithTag("Human");
        foreach (var h in humans) Destroy(h);

        var zombies = GameObject.FindGameObjectsWithTag("Zombie");
        foreach (var z in zombies) Destroy(z);
        
        spawnedPositions.Clear();
    }

    public void UpdateSpawnPoints(Transform mapRoot)
    {
        if (mapRoot == null) return;
        
        // Zemin seviyesini bul
        DetectGroundY(mapRoot);

        // 1. Try to find explicit containers
        Transform humanContainer = mapRoot.Find("SpawnPoints/Humans");
        Transform zombieContainer = mapRoot.Find("SpawnPoints/Zombies");

        // Clear previous references
        if (humanSpawnPoints == null) humanSpawnPoints = new List<Transform>();
        else humanSpawnPoints.Clear();

        if (zombieSpawnPoints == null) zombieSpawnPoints = new List<Transform>();
        else zombieSpawnPoints.Clear();

        // 2. Populate if found
        if (humanContainer != null)
        {
            foreach (Transform t in humanContainer) humanSpawnPoints.Add(t);
        }
        
        if (zombieContainer != null)
        {
            foreach (Transform t in zombieContainer) zombieSpawnPoints.Add(t);
        }

        // 3. Fallback: Use Map Bounds (Floor) if list is empty
        if (humanSpawnPoints.Count == 0 || zombieSpawnPoints.Count == 0)
        {
            Debug.Log("SpawnManager: Explicit spawn points not found. Generating dynamic points from Map Bounds...");
            GenerateDynamicSpawnPoints(mapRoot);
        }
        
        Debug.Log($"SpawnManager: Initialized with {humanSpawnPoints.Count} Human points and {zombieSpawnPoints.Count} Zombie points.");
    }

    private void GenerateDynamicSpawnPoints(Transform mapRoot)
    {
        // Try to find "Floor" or "Ground" or "Plane"
        Transform floor = mapRoot.Find("Floor");
        if (floor == null) floor = mapRoot.Find("Ground");
        if (floor == null) floor = mapRoot.Find("Plane");
        if (floor == null) floor = mapRoot.Find("Hole_Compatible_Floor");
        
        // Global arama
        if (floor == null)
        {
            GameObject floorObj = GameObject.Find("Floor");
            if (floorObj == null) floorObj = GameObject.Find("Plane");
            if (floorObj == null) floorObj = GameObject.Find("Hole_Compatible_Floor");
            if (floorObj != null) floor = floorObj.transform;
        }
        
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20, 1, 20)); // Default fallback
        
        if (floor != null)
        {
            // Floor/Plane bulundu - sadece onun bounds'unu kullan
            Renderer r = floor.GetComponent<Renderer>();
            if (r != null) 
            {
                bounds = r.bounds;
                Debug.Log($"[SpawnManager] Floor bounds kullanılıyor: Center={bounds.center}, Size={bounds.size}");
            }
            else 
            {
                Collider c = floor.GetComponent<Collider>();
                if (c != null) 
                {
                    bounds = c.bounds;
                    Debug.Log($"[SpawnManager] Floor collider bounds kullanılıyor: Center={bounds.center}, Size={bounds.size}");
                }
            }
        }
        else
        {
            // Floor bulunamadı - uyarı ver ve küçük alan kullan
            Debug.LogWarning("[SpawnManager] Floor/Plane bulunamadı! Varsayılan küçük alan kullanılıyor.");
            bounds = new Bounds(Vector3.zero, new Vector3(20, 1, 20));
        }
        
        // Bounds'u kaydet (spawn sırasında sınır kontrolü için)
        currentSpawnBounds = bounds;

        // Create temporary spawn points
        GameObject dynamicRoot = new GameObject("DynamicSpawnPoints_Temp");
        dynamicRoot.transform.SetParent(mapRoot);
        
        // Generate X points
        int pointsToGenerate = 10;
        
        for (int i = 0; i < pointsToGenerate; i++)
        {
            // Human Point
            GameObject hInfo = new GameObject($"HumanSpawn_{i}");
            hInfo.transform.SetParent(dynamicRoot.transform);
            hInfo.transform.position = GetRandomPosInBounds(bounds);
            humanSpawnPoints.Add(hInfo.transform);

            // Zombie Point
            GameObject zInfo = new GameObject($"ZombieSpawn_{i}");
            zInfo.transform.SetParent(dynamicRoot.transform);
            zInfo.transform.position = GetRandomPosInBounds(bounds);
            zombieSpawnPoints.Add(zInfo.transform);
        }
    }

    private Vector3 GetRandomPosInBounds(Bounds b)
    {
        // Bounds'u %20 küçült (kenarlardan uzak dur)
        float shrinkFactor = 0.8f;
        
        float halfExtentX = b.extents.x * shrinkFactor;
        float halfExtentZ = b.extents.z * shrinkFactor;
        
        float x = Random.Range(b.center.x - halfExtentX, b.center.x + halfExtentX);
        float z = Random.Range(b.center.z - halfExtentZ, b.center.z + halfExtentZ);
        
        // groundY bulunduysa onu kullan, yoksa bounds'un minimum Y değerini kullan
        float y = groundYDetected ? groundY : b.min.y;
        return new Vector3(x, y + 0.5f, z);
    }

    // Public getter for map bounds (Used by Hole Movement Limits)
    public Bounds GetMapBounds()
    {
        return currentSpawnBounds;
    }

    public void SpawnLevel(int humans, int zombies, bool isHordeMode = false)
    {
        humanCount = humans;
        zombieCount = zombies;
        
        // Spawn Humans (Always normal)
        for (int i = 0; i < humanCount; i++)
        {
            SpawnRandomPrefab(humanPrefabs, humanSpawnPoints, "Human");
        }

        // Spawn Zombies
        if (isHordeMode)
        {
            SpawnZombiesClustered();
        }
        else
        {
            // Normal Spawn
            for (int i = 0; i < zombieCount; i++)
            {
                GameObject newZombie = SpawnRandomPrefab(zombiePrefabs, zombieSpawnPoints, "Zombie");
                
                // --- LEVEL ASSIGNMENT ---
                // Eğer oyun leveli 3'ten büyükse, level ilerledikçe artan oranda güçlü zombi gelsin
                if (newZombie != null)
                {
                    int gameLevel = 1;
                    if (LevelManager.Instance != null) gameLevel = LevelManager.Instance.currentLevelIndex + 1;

                    if (gameLevel > 3)
                    {
                        // Formül: Level 4'te %15 başla, her levelde %5 artır. Max %70.
                        // Örn: Lvl 4 -> %15, Lvl 10 -> %45, Lvl 20 -> %70
                        float chance = 0.15f + ((gameLevel - 3) * 0.05f);
                        chance = Mathf.Clamp(chance, 0f, 0.7f);

                        if (Random.value < chance)
                        {
                            // Level ne kadar yüksekse, zombinin Level 3 olma şansı da artsın
                            // Basitçe: 2 ile (GameLevel/5 + 2) arasında. 
                            // Ancak şimdilik sadece 2 ve 3 var.
                            // Çok ileride belki Level 4 zombiler de gelir.
                            
                            int maxZombieLvl = 3;
                            if (gameLevel > 10) maxZombieLvl = 4; // Level 10'dan sonra devasa lvl 4 zombiler
                            
                            int randomLevel = Random.Range(2, maxZombieLvl + 1); 
                            
                            ZombieAI zAI = newZombie.GetComponent<ZombieAI>();
                            if (zAI != null) zAI.SetLevel(randomLevel);
                        }
                    }
                }
            }
        }
    }

    private void SpawnZombiesClustered()
    {
        if (zombiePrefabs == null || zombiePrefabs.Count == 0) 
        {
            Debug.LogWarning("SpawnManager: No Zombie Prefabs assigned!");
            return;
        }

        Vector3 hordeCenter = Vector3.zero;

        // 1. Merkez Noktası Belirle - Bounds'un merkezini kullan
        if (currentSpawnBounds.size.sqrMagnitude > 0.1f)
        {
            // Bounds merkezini kullan (en güvenli)
            hordeCenter = new Vector3(currentSpawnBounds.center.x, groundY, currentSpawnBounds.center.z);
        }
        else if (zombieSpawnPoints != null && zombieSpawnPoints.Count > 0)
        {
            Transform t = zombieSpawnPoints[Random.Range(0, zombieSpawnPoints.Count)];
            if (t != null) hordeCenter = t.position;
        }
        else
        {
            // Spawn point yoksa haritanın direkt ortasını al
            Debug.Log("SpawnManager: No spawn points for Horde. Using origin.");
            hordeCenter = new Vector3(0, groundY, 0);
        }

        Debug.Log($"SpawnManager: Horde Center selected at {hordeCenter}");

        // Ayarlar: Horde radius'u bounds'a göre ayarla
        float maxRadius = Mathf.Min(currentSpawnBounds.extents.x, currentSpawnBounds.extents.z) * 0.6f;
        float hordeRadius = Mathf.Max(8f, maxRadius); // En az 8, ama bounds'tan büyük olmasın
        float minSeparation = 0.8f; 
        int attemptsPerZombie = 30;

        for (int i = 0; i < zombieCount; i++)
        {
            GameObject selectedPrefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Count)];
            bool spawned = false;

            for (int attempt = 0; attempt < attemptsPerZombie; attempt++)
            {
                // Daire içinde rastgele nokta
                Vector2 rnd = Random.insideUnitCircle * hordeRadius;
                Vector3 candidatePos = hordeCenter + new Vector3(rnd.x, 0, rnd.y);
                
                // Sınır kontrolü
                candidatePos = ClampToBounds(candidatePos);

                // Yüksekliği ayarla (Raycast veya Fallback)
                Vector3 finalPos = GetPositionAroundPoint(candidatePos, 0.1f); 
                // Not: GetPositionAroundPoint zaten yükseklik ayarlıyor ve 'CheckValid' yapıyor.

                if (CheckValid(finalPos))
                {
                    // Şurada zombi var mı diye bak (Basit mesafe kontrolü)
                    if (IsPositionSafeForHorde(finalPos, minSeparation))
                    {
                        Quaternion rot = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                        Instantiate(selectedPrefab, finalPos, rot);
                        spawnedPositions.Add(finalPos);
                        spawned = true;
                        break;
                    }
                }
            }

            // Eğer normal yolla yer bulamazsak ZORLA SPAWN ET (Fallback)
            // Zombisiz kalmaktansa iç içe girmesi iyidir.
            if (!spawned)
            {
                Vector2 rnd = Random.insideUnitCircle * hordeRadius;
                Vector3 forcedPos = hordeCenter + new Vector3(rnd.x, 0, rnd.y);
                Vector3 finalForced = GetPositionAroundPoint(forcedPos, 0f);
                
                if (!CheckValid(finalForced)) finalForced = forcedPos; // Son çare

                Instantiate(selectedPrefab, finalForced, Quaternion.identity);
                spawnedPositions.Add(finalForced);
                Debug.Log("SpawnManager: Force spawned zombie (crowded area).");
            }
        }
        
        Debug.Log($"SpawnManager: Spawning {zombieCount} zombies in HORDE MODE complete.");
    }

    // Horde modu için daha hafif, sadece diğer zombileri kontrol eden güvenli alan
    private bool IsPositionSafeForHorde(Vector3 pos, float minDist)
    {
         // Sadece diğer spawnlanmış objelere bak, duvarlara vs çok takılma (Horde kaosu için)
         foreach (Vector3 spawnedPos in spawnedPositions)
         {
             if (Vector3.Distance(pos, spawnedPos) < minDist) return false;
         }
         return true;
    }

    /* REMOVED OLD SpawnCharacters to avoid duplication, logic moved to SpawnLevel */

    private GameObject SpawnRandomPrefab(List<GameObject> prefabs, List<Transform> spawnPoints, string debugName)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning($"SpawnManager: No prefabs assigned for {debugName}!");
            return null;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
             Debug.LogWarning($"SpawnManager: No spawn points assigned for {debugName}! Please assign them in the Inspector.");
             return null;
        }

        GameObject selectedPrefab = prefabs[Random.Range(0, prefabs.Count)];
        
        // Try finding a valid position multiple times
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Listeden rastgele bir nokta seç
            Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            
            if (randomPoint == null) continue;

            // O noktanın etrafında (spawnRadius kadar) rastgele bir yer bul
            // Böylece hepsi üst üste binmez
            Vector3 candidatePos = GetPositionAroundPoint(randomPoint.position, spawnRadius);

            if (CheckValid(candidatePos))
            {
                if (IsValidPosition(candidatePos))
                {
                    Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                    GameObject instance = Instantiate(selectedPrefab, candidatePos, randomRotation);
                    spawnedPositions.Add(candidatePos); // Kaydet
                    return instance; // Spawn successful, return obj
                }
            }
        }

        Debug.LogWarning($"SpawnManager: Could not find valid position for {debugName} after {maxSpawnAttempts} attempts.");
        return null;
    }

    private bool IsValidPosition(Vector3 position)
    {
        if (!CheckValid(position)) return false;

        // 1. Engel Kontrolü (Obstacle Layer)
        Vector3 checkPos = position + Vector3.up * (collisionCheckRadius + 0.2f);
        
        if (obstacleLayer.value != 0 && Physics.CheckSphere(checkPos, collisionCheckRadius, obstacleLayer))
        {
            return false;
        }

        // 2. Diğer karakterlere mesafe kontrolü
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetPositionAroundPoint(Vector3 centerPoint, float radius)
    {
        // Safety check for centerPoint
        if (!CheckValid(centerPoint))
        {
            Debug.LogError("SpawnManager: CenterPoint is invalid (Infinity/NaN)! Skipping.");
            return Vector3.negativeInfinity;
        }

        // Rastgele bir ofset al (Daire içinde)
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 targetPos = centerPoint + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // --- SINIR KONTROLÜ ---
        // Pozisyonu spawn bounds içinde tut
        targetPos = ClampToBounds(targetPos);

        // --- ZEMIN SEVİYESİNE OTURT ---
        // Eğer groundY tespit edilmişse, doğrudan onu kullan
        if (groundYDetected)
        {
            // Karakter yüksekliği için +0.1f offset (ayaklar zemine bassın)
            Vector3 result = new Vector3(targetPos.x, groundY + spawnHeightOffset + 0.1f, targetPos.z);
            return CheckValid(result) ? result : Vector3.negativeInfinity;
        }
        
        // --- FALLBACK: RAYCAST ILE ZEMINE OTURT ---
        // Yüksekten aşağıya bak
        Vector3 rayStart = new Vector3(targetPos.x, centerPoint.y + raycastHeight, targetPos.z);
        
        // Geçici Maske Mantığı: Eğer Ground Layer ayarlanmamışsa, her şeyi zemin kabul et (~0)
        LayerMask activeMask = groundLayer;
        if (activeMask.value == 0) activeMask = ~0; // Everything

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, activeMask))
        {
            // Zemin bulundu! Tam üstüne koy.
            Vector3 result = hit.point + Vector3.up * (spawnHeightOffset + 0.1f);
            return CheckValid(result) ? result : Vector3.negativeInfinity;
        }
        else
        {
             // --- FALLBACK (ZEMİN BULUNAMADI) ---
             // groundY varsa onu kullan, yoksa centerPoint.y kullan
             float fallbackY = groundYDetected ? groundY : centerPoint.y;
             Vector3 fallbackResult = new Vector3(targetPos.x, fallbackY + spawnHeightOffset + 0.1f, targetPos.z);
             return CheckValid(fallbackResult) ? fallbackResult : Vector3.negativeInfinity;
        }
    }
    
    private Vector3 ClampToBounds(Vector3 pos)
    {
        // Eğer bounds ayarlanmamışsa (size 0), pozisyonu olduğu gibi döndür
        if (currentSpawnBounds.size.sqrMagnitude < 0.1f) return pos;
        
        // %80 küçültülmüş bounds içinde tut
        float shrink = 0.8f;
        float halfX = currentSpawnBounds.extents.x * shrink;
        float halfZ = currentSpawnBounds.extents.z * shrink;
        
        float clampedX = Mathf.Clamp(pos.x, currentSpawnBounds.center.x - halfX, currentSpawnBounds.center.x + halfX);
        float clampedZ = Mathf.Clamp(pos.z, currentSpawnBounds.center.z - halfZ, currentSpawnBounds.center.z + halfZ);
        
        return new Vector3(clampedX, pos.y, clampedZ);
    }

    private bool CheckValid(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || 
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
        {
            return false;
        }
        return true;
    }
    
    private void DetectGroundY(Transform mapRoot)
    {
        // 1. Önce "Floor", "Plane", "Ground" isimli objeyi ara
        string[] floorNames = { "Floor", "Plane", "Ground", "Hole_Compatible_Floor" };
        
        foreach (string name in floorNames)
        {
            Transform floor = mapRoot.Find(name);
            if (floor != null)
            {
                groundY = floor.position.y;
                groundYDetected = true;
                Debug.Log($"[SpawnManager] Zemin bulundu: {name} | Y = {groundY}");
                return;
            }
        }
        
        // 2. Sahnede global ara
        foreach (string name in floorNames)
        {
            GameObject floor = GameObject.Find(name);
            if (floor != null)
            {
                groundY = floor.transform.position.y;
                groundYDetected = true;
                Debug.Log($"[SpawnManager] Zemin bulundu (Global): {name} | Y = {groundY}");
                return;
            }
        }
        
        // 3. Ground tag'li obje ara
        GameObject[] groundObjects = GameObject.FindGameObjectsWithTag("Ground");
        if (groundObjects.Length > 0)
        {
            // En düşük Y değerine sahip olanı seç (Gerçek zemin)
            float lowestY = float.MaxValue;
            foreach (var go in groundObjects)
            {
                if (go.transform.position.y < lowestY)
                {
                    lowestY = go.transform.position.y;
                }
            }
            groundY = lowestY;
            groundYDetected = true;
            Debug.Log($"[SpawnManager] Zemin bulundu (Tag): Y = {groundY}");
            return;
        }
        
        // 4. Bulunamadı - Raycast ile dene
        RaycastHit hit;
        if (Physics.Raycast(Vector3.up * 50f, Vector3.down, out hit, 100f))
        {
            groundY = hit.point.y;
            groundYDetected = true;
            Debug.Log($"[SpawnManager] Zemin bulundu (Raycast): Y = {groundY}");
            return;
        }
        
        // 5. Hiçbir şey bulunamadı
        groundY = 0f;
        groundYDetected = false;
        Debug.LogWarning("[SpawnManager] Zemin bulunamadı! Varsayılan Y = 0 kullanılıyor.");
    }

    // ========== SKILL PICKUP SPAWN SYSTEM ==========
    [Header("Skill Pickup Settings")]
    [Tooltip("Magnet Skill Prefab")]
    public GameObject magnetPickupPrefab;
    [Tooltip("Speed Skill Prefab")]
    public GameObject speedPickupPrefab;
    [Tooltip("Shield Skill Prefab")]
    public GameObject shieldPickupPrefab;
    
    [Tooltip("Minimum spawn aralığı (saniye)")]
    public float skillSpawnMinInterval = 15f;
    [Tooltip("Maximum spawn aralığı (saniye)")]
    public float skillSpawnMaxInterval = 30f;
    [Tooltip("Aynı anda mapte olabilecek max skill sayısı")]
    public int maxSkillPickupsOnMap = 2;
    
    private float nextSkillSpawnTime;
    private List<GameObject> activeSkillPickups = new List<GameObject>();
    private bool skillSpawningEnabled = false;
    
    public void StartSkillSpawning()
    {
        skillSpawningEnabled = true;
        ScheduleNextSkillSpawn();
        Debug.Log("[SpawnManager] Skill pickup spawning started.");
    }
    
    public void StopSkillSpawning()
    {
        skillSpawningEnabled = false;
        
        // Mevcut pickup'ları temizle
        foreach (var pickup in activeSkillPickups)
        {
            if (pickup != null) Destroy(pickup);
        }
        activeSkillPickups.Clear();
        Debug.Log("[SpawnManager] Skill pickup spawning stopped.");
    }
    
    void Update()
    {
        // Skill spawn kontrolü
        if (skillSpawningEnabled && Time.time >= nextSkillSpawnTime)
        {
            TrySpawnSkillPickup();
            ScheduleNextSkillSpawn();
        }
        
        // Null referansları temizle (yutulmuş veya timeout olmuş pickup'lar)
        activeSkillPickups.RemoveAll(p => p == null);
    }
    
    void ScheduleNextSkillSpawn()
    {
        nextSkillSpawnTime = Time.time + Random.Range(skillSpawnMinInterval, skillSpawnMaxInterval);
    }
    
    void TrySpawnSkillPickup()
    {
        // Max limite ulaşıldı mı?
        if (activeSkillPickups.Count >= maxSkillPickupsOnMap)
        {
            Debug.Log("[SpawnManager] Max skill pickups on map, skipping spawn.");
            return;
        }
        
        // Rastgele skill tipi seç
        SkillType randomSkill = (SkillType)Random.Range(0, 3);
        GameObject prefabToSpawn = null;

        switch (randomSkill)
        {
            case SkillType.Magnet:
                prefabToSpawn = magnetPickupPrefab;
                break;
            case SkillType.Speed:
                prefabToSpawn = speedPickupPrefab;
                break;
            case SkillType.Shield:
                prefabToSpawn = shieldPickupPrefab;
                break;
        }
        
        // Prefab var mı?
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[SpawnManager] {randomSkill} Prefab atanmamış! Atlıyor.");
            return;
        }
        
        // Spawn pozisyonu bul
        Vector3 spawnPos = FindSkillSpawnPosition();
        
        if (spawnPos == Vector3.zero)
        {
            Debug.LogWarning("[SpawnManager] Skill pickup için uygun pozisyon bulunamadı.");
            return;
        }
        
        // Spawn!
        GameObject pickup = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        
        // Skill tipini garantiye al (Prefab üzerinde ayarlı olsa bile)
        SkillPickup skillComponent = pickup.GetComponent<SkillPickup>();
        if (skillComponent != null)
        {
            skillComponent.skillType = randomSkill;
        }
        
        activeSkillPickups.Add(pickup);
        Debug.Log($"[SpawnManager] Skill Pickup spawned: {randomSkill} at {spawnPos}");
    }
    
    Vector3 FindSkillSpawnPosition()
    {
        int maxAttempts = 20;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidatePos;
            
            // Bounds varsa kullan
            if (currentSpawnBounds.size.sqrMagnitude > 0.1f)
            {
                candidatePos = GetRandomPosInBounds(currentSpawnBounds);
            }
            else
            {
                // Fallback: Rastgele pozisyon
                candidatePos = new Vector3(Random.Range(-15f, 15f), groundY + 1f, Random.Range(-15f, 15f));
            }
            
            // Yüksekliği ayarla (zeminden 2m yukarı - SkillPickup kendi raycast ile düşecek)
            candidatePos.y = groundY + 2f;
            
            // Engel kontrolü
            if (!Physics.CheckSphere(candidatePos, 1f, obstacleLayer))
            {
                // Diğer pickup'lara yakın mı?
                bool tooClose = false;
                foreach (var existingPickup in activeSkillPickups)
                {
                    if (existingPickup != null && Vector3.Distance(existingPickup.transform.position, candidatePos) < 5f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    return candidatePos;
                }
            }
        }
        
        return Vector3.zero; // Bulunamadı
    }

    private void OnDrawGizmosSelected()
    {
        // Spawn noktalarını çiz
        if (humanSpawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var p in humanSpawnPoints)
            {
                if(p != null) Gizmos.DrawWireSphere(p.position, 3f);
            }
        }

        if (zombieSpawnPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (var p in zombieSpawnPoints)
            {
               if(p != null) Gizmos.DrawWireSphere(p.position, 3f);
            }
        }
        
        // Aktif skill pickup'ları çiz
        if (activeSkillPickups != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var p in activeSkillPickups)
            {
                if(p != null) Gizmos.DrawWireSphere(p.transform.position, 1f);
            }
        }
    }
}
