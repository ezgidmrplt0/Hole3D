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

    private void Start()
    {
        // Ensure SkillNavigation exists
        if (FindObjectOfType<SkillNavigation>() == null)
        {
            GameObject navObj = new GameObject("SkillNavigationSystem");
            navObj.AddComponent<SkillNavigation>();
#if UNITY_EDITOR
            Debug.Log("[SpawnManager] Auto-created SkillNavigation system.");
#endif
        }
    }

    [Header("Prefabs")]
    [Tooltip("List of Human prefabs to spawn.")]
    public List<GameObject> humanPrefabs;
    [Tooltip("List of Zombie prefabs to spawn.")]
    public List<GameObject> zombiePrefabs;
    
    [Header("Cage & Key Mechanic")]
    public GameObject cagePrefab;
    public GameObject keyPrefab;
    public GameObject cageUnitPrefab; // GM-Grid veya benzeri duvar parçası
    [Tooltip("Cage Unit Prefab kullanıldığında scale çarpanı.")]
    public float cageUnitScale = 3f; 
    public int cageCount = 3;
    public int zombiesPerCage = 3; // Kafes başına kaç zombi (Balance için önemli)
    private List<GameObject> activeCages = new List<GameObject>();



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
        
        // Cleanup Cages & Keys
        foreach(var c in activeCages) if(c != null) Destroy(c);
        activeCages.Clear();
        foreach(var k in FindObjectsOfType<KeyPickup>()) Destroy(k.gameObject);
        
        spawnedPositions.Clear();
    }

    public void UpdateSpawnPoints(Transform mapRoot)
    {
        if (mapRoot == null) return;
        
        // 1. ZORUNLU: Önce zemini bul (Spawn Yüksekliği için kritik)
        DetectGroundY(mapRoot);

        // 2. Try to find explicit containers
        Transform humanContainer = mapRoot.Find("SpawnPoints/Humans");
        Transform zombieContainer = mapRoot.Find("SpawnPoints/Zombies");

        // Clear previous references
        if (humanSpawnPoints == null) humanSpawnPoints = new List<Transform>();
        else humanSpawnPoints.Clear();

        if (zombieSpawnPoints == null) zombieSpawnPoints = new List<Transform>();
        else zombieSpawnPoints.Clear();

        // 3. Populate if found
        if (humanContainer != null)
        {
            foreach (Transform t in humanContainer) humanSpawnPoints.Add(t);
        }
        
        if (zombieContainer != null)
        {
            foreach (Transform t in zombieContainer) zombieSpawnPoints.Add(t);
        }

        // 4. Fallback: Use Map Bounds (Floor) if list is empty
        if (humanSpawnPoints.Count == 0 || zombieSpawnPoints.Count == 0)
        {
#if UNITY_EDITOR
            Debug.Log("SpawnManager: Explicit spawn points not found. Generating dynamic points from Map Bounds...");
#endif
            GenerateDynamicSpawnPoints(mapRoot);
        }
        
#if UNITY_EDITOR
        Debug.Log($"SpawnManager: Initialized with {humanSpawnPoints.Count} Human points and {zombieSpawnPoints.Count} Zombie points. GroundY: {groundY}");
#endif
    }

    private void GenerateDynamicSpawnPoints(Transform mapRoot)
    {
        // --- AKILLI ZEMİN BULMA (MESH TARAMA) ---
        Transform floor = null;
        
        // 1. İsimle Ara
        string[] potentialNames = { "Floor", "Ground", "Plane", "Hole_Compatible_Floor", "Zemin", "Terrain", "Base", "Platform" };
        foreach(var name in potentialNames)
        {
            floor = mapRoot.Find(name);
            if(floor != null) break;
        }

        if (floor == null)
        {
            // Global Ara
            foreach(var name in potentialNames)
            {
                GameObject obj = GameObject.Find(name);
                if(obj != null) 
                {
                    floor = obj.transform;
                    break;
                }
            }
        }
        
        // 2. İsimle Bulunamadıysa -> En Büyük Yatay Mesh'i Bul
        if (floor == null)
        {
            Renderer[] allRenderers = mapRoot.GetComponentsInChildren<Renderer>();
            float maxArea = 0f;
            
            foreach(var r in allRenderers)
            {
                // Sadece yatay genişliği olanları al (Yüksekliği az, genişliği fazla)
                if (r.bounds.size.y < r.bounds.size.x && r.bounds.size.y < r.bounds.size.z)
                {
                    float area = r.bounds.size.x * r.bounds.size.z;
                    if (area > maxArea && area > 25f) // Min 5x5
                    {
                        maxArea = area;
                        floor = r.transform;
                    }
                }
            }
            if (floor != null) Debug.Log($"[SpawnManager] Auto-detected Floor by Size: {floor.name}");
        }

        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20, 1, 20)); // Default fallback
        
        if (floor != null)
        {
            Renderer r = floor.GetComponent<Renderer>();
            if (r != null) bounds = r.bounds;
            else 
            {
                Collider c = floor.GetComponent<Collider>();
                if (c != null) bounds = c.bounds;
            }
            
            // Bounds bulunduysa, zemin yüksekliğini de güncelle (Eğer DetectGroundY bulamadıysa)
            if (!groundYDetected)
            {
                groundY = bounds.max.y; // En üst noktası zemindir
                groundYDetected = true;
#if UNITY_EDITOR
                Debug.Log($"[SpawnManager] GroundY updated from Floor Bounds: {groundY}");
#endif
            }
        }
        else
        {
            Debug.LogWarning("[SpawnManager] Floor/Plane bulunamadı! Varsayılan küçük alan kullanılıyor.");
            bounds = new Bounds(Vector3.zero, new Vector3(20, 1, 20));
        }
        
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

        // --- CAGE & KEY SPAWN ---
        SpawnCagesAndKey();
    }

    private void SpawnCagesAndKey()
    {
        // Temizlik
        // Cleanup Cages & Keys
        foreach(var c in activeCages) if(c != null) Destroy(c);
        activeCages.Clear();
        // Tag "Key" might not exist if forgot to add. Use Type search which is safer if tag is missing.
        // But logic requires tag for other things maybe? No, KeyPickup is script based.
        // Let's replace tag search with Type search to be completely safe and avoid Tag errors if project settings reset.
        foreach(var k in FindObjectsOfType<KeyPickup>()) Destroy(k.gameObject);
        // Deprecated tag search for extra safety
        // var keys = GameObject.FindGameObjectsWithTag("Key"); 


        // 1. KEY SPAWN (1 Tane)
        SpawnKey();

        // 2. CAGE SPAWN (3 Tane)
        for (int i = 0; i < cageCount; i++)
        {
            SpawnCage();
        }
    }

    private void SpawnKey()
    {
        // Kullanıcı isteği: Skill spawn mantığı gibi olsun (Oyuncuya yakın, doğru yükseklikte)
        Vector3 pos = FindSkillSpawnPosition();
        
        // Eğer FindSkillSpawnPosition başarısız olursa (0,0,0 dönerse), fallback yap
        if (pos == Vector3.zero)
        {
             pos = GetRandomPosInBounds(currentSpawnBounds);
             pos.y = (groundYDetected ? groundY : 0) + 1.0f;
        }

        // User requested slight adjustment (0.1f lower)
        pos.y -= 0.1f;

        if (keyPrefab != null)
        {
            GameObject keyObj = Instantiate(keyPrefab, pos, Quaternion.identity);
            keyObj.transform.localScale = Vector3.one * 1.5f; // Kullanıcı isteği: 0.5 yerine 1.5 olsun
#if UNITY_EDITOR
            Debug.Log($"[SpawnManager] Key Spawned at {pos}");
#endif
        }
        else
        {
            Debug.LogWarning("[SpawnManager] Key Prefab is NOT assigned! Please assign a Key Prefab in the inspector.");
        }
    }

    private void SpawnCage()
    {
        Vector3 pos = Vector3.zero;
        bool positionFound = false;
        int attempts = 0;

        while (!positionFound && attempts < 20)
        {
            attempts++;
            pos = GetRandomPosInBounds(currentSpawnBounds);
            pos = GetPositionAroundPoint(pos, 2f); 
            
            if (!CheckValid(pos)) continue;

            // Check distance against other Cages
            bool tooClose = false;
            foreach (var otherCage in activeCages)
            {
                if (otherCage != null)
                {
                    if (Vector3.Distance(pos, otherCage.transform.position) < 6f) // 3x3 cage -> 6 buffer
                    {
                        tooClose = true;
                        break;
                    }
                }
            }
            // Check distance against Key if possible (Safety)
            KeyPickup key = FindObjectOfType<KeyPickup>();
            if (key != null && Vector3.Distance(pos, key.transform.position) < 4f) tooClose = true;

            if (!tooClose)
            {
                positionFound = true;
            }
        }

        if (!positionFound)
        {
             Debug.LogWarning("[SpawnManager] Cage için güvenli yer bulunamadı. Force Spawn yapılıyor.");
             // Force spawn at random bounds position
             pos = GetRandomPosInBounds(currentSpawnBounds);
             pos.y = groundY; // Ground level
        }

        GameObject cageObj = null;

        if (cagePrefab != null)
        {
            cageObj = Instantiate(cagePrefab, pos, Quaternion.identity);
        }
        else
        {
            // Placeholder Cage (Procedural Hollow Box)
            cageObj = new GameObject("Cage_Procedural");
            // Yüksekliği ayarla: Merkez 1.5f yukarıda (yükseklik 3 olacağı için)
            cageObj.transform.position = pos + Vector3.up * 1.5f; 
            
            // Cage Controller
            var controller = cageObj.AddComponent<CageController>();
            controller.cageVisuals = cageObj.transform;

            // --- CAGE VISUAL GENERATION ---
            // Eğer "Unit Prefab" (GM-Grid) atanmışsa, onu kullanalım.
            // Atanmamışsa eski "Transparan Kutu" yöntemine dönelim.
            
            if (cageUnitPrefab != null)
            {
                // -- GM-GRID ILE KAFES --
                // GM-Grid muhtemelen düz bir zemin (Plane gibi).
                // Boyutunu 3x3 olacak şekilde ayarlamamız gerekebilir.
                
                float size = 3f;
                float halfSize = size / 2f;
                // Kalınlık (gridin kalınlığı)
                
                // Dark Gray Material
                Material darkGrayMat = new Material(Shader.Find("Standard"));
                darkGrayMat.color = new Color(0.2f, 0.2f, 0.2f); // Koyu Gri
                darkGrayMat.SetFloat("_Metallic", 0.5f);
                darkGrayMat.SetFloat("_Smoothness", 0.5f);

                // Helper: Instantiate Unit and Rotate with AUTO-CENTER
                void CreateUnit(string name, Vector3 localPos, Vector3 localRot)
                {
                    // 1. Create Wrapper (Anchor) -> Kafes yüzeyinin tam ortası
                    GameObject wrapper = new GameObject(name + "_Anchor");
                    wrapper.transform.SetParent(cageObj.transform);
                    wrapper.transform.localPosition = localPos;
                    wrapper.transform.localEulerAngles = localRot;

                    // 2. Instantiate Unit
                    GameObject unit = Instantiate(cageUnitPrefab, wrapper.transform);
                    unit.name = name + "_Visual";
                    
                    // Reset Transform before setup
                    unit.transform.localPosition = Vector3.zero;
                    unit.transform.localRotation = Quaternion.identity;
                    unit.transform.localScale = Vector3.one * cageUnitScale; 
                    
                    // 3. AUTO-CENTER LOGIC (Pivot düzeltme)
                    // Prefabın pivotu köşedeyse, merkezden kaçık durur. Bunu görsel merkeze göre düzeltelim.
                    Renderer[] rends = unit.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        // Apply Material
                        foreach(var r in rends) r.material = darkGrayMat;

                        // Tüm rendererların bounds'unu birleştir
                        Bounds b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        
                        // Wrapper'ın local uzayında görselin merkezini bul
                        Vector3 centerInWrapper = wrapper.transform.InverseTransformPoint(b.center);
                        
                        // Unit'i ters yönde kaydır ki görsel merkez (0,0,0)'a otursun
                        // Sadece X ve Y ekseninde (Yüzey üzerinde) merkezleyelim, Z (Derinlik/Kalınlık) eksenini bozmayalım mı?
                        // Genelde Grid düzdür, Z'si incedir. Tam merkezlemek en güvenlisi.
                        unit.transform.localPosition = -centerInWrapper;
                    }
                }

                // 6 YÜZ (İÇE BAKACAK ŞEKİLDE)
                // Floor: Aşağıda, yukarı bakıyor (0,0,0)
                CreateUnit("Floor", new Vector3(0, -halfSize, 0), Vector3.zero);
                
                // Ceiling: Yukarıda, aşağı bakıyor (180,0,0)
                CreateUnit("Ceiling", new Vector3(0, halfSize, 0), new Vector3(180, 0, 0));
                
                // Front (Z+): İleri gitmiş, arkaya bakıyor
                // Rotasyon: X ekseninde 90 derece (öne dik)
                CreateUnit("Wall_Front", new Vector3(0, 0, halfSize), new Vector3(90, 0, 0));
                
                // Back (Z-): Geri gitmiş, öne bakıyor
                CreateUnit("Wall_Back", new Vector3(0, 0, -halfSize), new Vector3(-90, 0, 0));
                
                // Left (X-): Sola gitmiş, sağa bakıyor
                CreateUnit("Wall_Left", new Vector3(-halfSize, 0, 0), new Vector3(0, 0, -90));
                
                // Right (X+): Sağa gitmiş, sola bakıyor
                CreateUnit("Wall_Right", new Vector3(halfSize, 0, 0), new Vector3(0, 0, 90));
            }
            else
            {
                // -- ESKİ TRANSPARAN KUTU (FALLBACK) --
                float size = 3f;
                float thickness = 0.1f;
                float halfSize = size / 2f;
                
                Material wallMat = new Material(Shader.Find("Standard"));
                wallMat.color = new Color(0.3f, 0.3f, 0.3f, 0.3f); // Daha az opak
                wallMat.SetFloat("_Mode", 3); // Transparent
                wallMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                wallMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                wallMat.SetInt("_ZWrite", 0);
                wallMat.DisableKeyword("_ALPHATEST_ON");
                wallMat.EnableKeyword("_ALPHABLEND_ON");
                wallMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                wallMat.renderQueue = 3000;

                void CreateWall(string name, Vector3 localPos, Vector3 scale)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = name;
                    wall.transform.SetParent(cageObj.transform);
                    wall.transform.localPosition = localPos;
                    wall.transform.localScale = scale;
                    wall.GetComponent<Renderer>().material = wallMat;
                }

                CreateWall("Floor",   new Vector3(0, -halfSize, 0), new Vector3(size, thickness, size));
                CreateWall("Ceiling", new Vector3(0, halfSize, 0),  new Vector3(size, thickness, size));
                CreateWall("Left",    new Vector3(-halfSize, 0, 0), new Vector3(thickness, size, size));
                CreateWall("Right",   new Vector3(halfSize, 0, 0),  new Vector3(thickness, size, size));
                CreateWall("Front",   new Vector3(0, 0, halfSize),  new Vector3(size, size, thickness));
                CreateWall("Back",    new Vector3(0, 0, -halfSize), new Vector3(size, size, thickness));
            }

            // İçine Zombileri Koy
            controller.trappedZombies = new List<ZombieAI>();
            
            for(int k=0; k<zombiesPerCage; k++)
            {
               if (zombiePrefabs.Count > 0)
               {
                   // Zombileri kafesin zeminine (veya map zeminine) basacak şekilde spawn et
                   // Kafesin içi y = 0 (zemin) seviyesinde olmalı
                   // Cage center y=1.5, floor y=-1.5 (local) => global y=0
                   
                   Vector3 zPos = pos + (Vector3)(Random.insideUnitCircle * 0.8f); // Merkeze yakın
                   zPos.y = (groundYDetected ? groundY : 0) + 0.1f; 
                   
                   GameObject z = Instantiate(zombiePrefabs[0], zPos, Quaternion.identity);
                   var zAI = z.GetComponent<ZombieAI>();
                   if(zAI != null) 
                   {
                       controller.trappedZombies.Add(zAI);
                   }
               }
            }
            
            activeCages.Add(cageObj);
#if UNITY_EDITOR
            Debug.Log("[SpawnManager] Placeholder Cage Created with Zombies.");
#endif
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

#if UNITY_EDITOR
        Debug.Log($"SpawnManager: Horde Center selected at {hordeCenter}");
#endif

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
                        // Pozisyonu groundY'ye zorla
                        Vector3 safePos = new Vector3(finalPos.x, groundY + 0.1f, finalPos.z);
                        
                        Quaternion rot = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                        Instantiate(selectedPrefab, safePos, rot);
                        spawnedPositions.Add(safePos);
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
                forcedPos = ClampToBounds(forcedPos);
                
                // Zorla groundY'ye oturt
                Vector3 safeForced = new Vector3(forcedPos.x, groundY + 0.1f, forcedPos.z);

                Instantiate(selectedPrefab, safeForced, Quaternion.identity);
                spawnedPositions.Add(safeForced);
#if UNITY_EDITOR
                Debug.Log("SpawnManager: Force spawned zombie (crowded area).");
#endif
            }
        }
        
#if UNITY_EDITOR
        Debug.Log($"SpawnManager: Spawning {zombieCount} zombies in HORDE MODE complete.");
#endif
    }

    // Horde modu için daha hafif, sadece diğer zombileri kontrol eden güvenli alan
    private bool IsPositionSafeForHorde(Vector3 pos, float minDist)
    {
         // Yükseklik kontrolü - groundY'den çok yüksekte mi?
         if (pos.y > groundY + 0.5f)
         {
             return false;
         }
         
         // Engel kontrolü
         if (IsPositionBlockedByObstacle(pos))
         {
             return false;
         }
         
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
            // Debug.LogWarning($"SpawnManager: No prefabs assigned for {debugName}!"); // Spam olmasın
            return null;
        }

        GameObject selectedPrefab = prefabs[Random.Range(0, prefabs.Count)];
        
        // --- 1. SPAWN NOKTASI SEÇİMİ ---
        Vector3 targetBasePos = Vector3.zero;
        bool hasSpawnPoints = (spawnPoints != null && spawnPoints.Count > 0);

        // --- 2. YER BULMA (Try find valid) ---
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            if (hasSpawnPoints)
            {
                // Listeden rastgele nokta
                Transform pt = spawnPoints[Random.Range(0, spawnPoints.Count)];
                if (pt != null) targetBasePos = pt.position;
                else targetBasePos = GetRandomPosInBounds(currentSpawnBounds);
            }
            else
            {
                // Liste yoksa Bounds kullan
                targetBasePos = GetRandomPosInBounds(currentSpawnBounds);
            }

            // Etrafında rastgele yer
            Vector3 candidatePos = GetPositionAroundPoint(targetBasePos, spawnRadius);

            if (CheckValid(candidatePos) && IsValidPosition(candidatePos))
            {
                // BAŞARILI!
                return FinalizeSpawn(selectedPrefab, candidatePos);
            }
        }

        // --- 3. FORCE SPAWN (FALLBACK) ---
        // Geçerli yer bulunamadı, ama boş kalmasındansa havada spawn olsun
#if UNITY_EDITOR
        Debug.Log($"[SpawnManager] Force Spawning {debugName} (Safe spot not found).");
#endif
        
        Vector3 forcePos;
        if (hasSpawnPoints)
        {
             Transform pt = spawnPoints[Random.Range(0, spawnPoints.Count)];
             forcePos = pt != null ? pt.position : currentSpawnBounds.center;
        }
        else
        {
            forcePos = GetRandomPosInBounds(currentSpawnBounds);
        }
        
        // Havadan bırak (Safe)
        forcePos.y = (groundYDetected ? groundY : forcePos.y) + 2.0f;
        
        // Sınır içinde tut
        forcePos = ClampToBounds(forcePos);
        
        return FinalizeSpawn(selectedPrefab, forcePos);
    }

    private GameObject FinalizeSpawn(GameObject prefab, Vector3 pos)
    {
        // Yüksekliği groundY seviyesine (+0.1f) sabitlemeye çalış, ama çok yüksekteyse (force spawn) elleme
        if (pos.y < groundY + 1.0f)
        {
            pos.y = groundY + 0.1f;
        }

        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        GameObject instance = Instantiate(prefab, pos, randomRotation);
        
        spawnedPositions.Add(pos);
        return instance;
    }

    private bool IsValidPosition(Vector3 position)
    {
        if (!CheckValid(position)) return false;
        
        // 0. KRITIK: Yükseklik kontrolü - groundY'den çok yüksekte mi?
        // Toleransı artırdık (0.5f -> 2.5f) çünkü zemin biraz eğimli olabilir veya obje pivotu farklı olabilir.
        float maxAllowedY = groundY + 2.5f; 
        if (position.y > maxAllowedY)
        {
            // Debug için log ekleyelim (Sadece editörde aşırı spam olmasın diye kapalı tutabilirsiniz)
            // Debug.LogWarning($"Spawn Rejected Height: {position.y} > {maxAllowedY} (GroundY: {groundY})");
            return false; // Çok yüksekte, geçersiz
        }

        // 1. Engel Kontrolü (Obstacle Layer)
        Vector3 checkPos = position + Vector3.up * (collisionCheckRadius + 0.2f);
        
        if (obstacleLayer.value != 0 && Physics.CheckSphere(checkPos, collisionCheckRadius, obstacleLayer))
        {
            return false;
        }
        
        // 1.5 Engel altında mı kontrolü
        if (IsPositionBlockedByObstacle(position))
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
        // KRITIK: Karakterler ASLA groundY'nin üzerinde spawn olmamalı
        // Bu yüzden raycast kullanmak yerine doğrudan groundY kullanıyoruz
        
        float spawnY = groundYDetected ? groundY : 0f;
        
        // Pozisyonda engel var mı kontrol et
        Vector3 candidateResult = new Vector3(targetPos.x, spawnY + spawnHeightOffset + 0.1f, targetPos.z);
        
        // Engel kontrolü - bu pozisyonda bir engel (taş, kristal vs) var mı?
        if (IsPositionBlockedByObstacle(candidateResult))
        {
            // Engel var, bu pozisyon geçersiz
            return Vector3.negativeInfinity;
        }
        
        return CheckValid(candidateResult) ? candidateResult : Vector3.negativeInfinity;
    }
    
    // Pozisyonda engel olup olmadığını kontrol et
    private bool IsPositionBlockedByObstacle(Vector3 pos)
    {
        // Yukarıdan aşağıya raycast at
        Vector3 rayStart = pos + Vector3.up * 5f;
        RaycastHit hit;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f))
        {
            // Eğer çarptığımız şey groundY'den yüksekteyse, bu bir engeldir
            float hitY = hit.point.y;
            float tolerance = 0.3f;
            
            if (hitY > groundY + tolerance)
            {
                // Bu bir engel (taş, kristal, bina vs)
                return true;
            }
            
            // Ayrıca "Ground" tag'i olmayan ve yüksek olan objeleri de engelle
            if (!hit.collider.CompareTag("Ground") && hitY > groundY + tolerance)
            {
                return true;
            }
        }
        
        // Sphere check ile de engel kontrolü yap
        if (obstacleLayer.value != 0)
        {
            Collider[] obstacles = Physics.OverlapSphere(pos + Vector3.up * 0.5f, 0.5f, obstacleLayer);
            if (obstacles.Length > 0)
            {
                return true;
            }
        }
        
        return false;
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
        // YENİ STRATEJİ: GÖRSEL TABANLI (Renderer) TESPİT (En Güvenilir)
        // NavMesh bazen bake edilmemiş olabilir veya görünmez bir plane üzerinde olabilir.
        // En mantıklısı, oyuncunun gördüğü o "büyük zeminin" yüksekliğini almaktır.

        // 1. Sahnedeki tüm Renderer'ları al (Prefab içindekiler dahil)
        // mapRoot varsa ondan, yoksa sahneden
        Renderer[] renderers = mapRoot != null ? mapRoot.GetComponentsInChildren<Renderer>() : FindObjectsOfType<Renderer>();
        
        float maxSurfaceArea = 0f;
        float bestGroundY = 0f; // Default
        bool foundVisualGround = false;

        foreach (var r in renderers)
        {
            // Sadece mesh renderer (Particle, Trail vs hariç)
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            
            // Bounds büyüklüğü
            Vector3 size = r.bounds.size;
            
            // Zemin olma kriteri: Geniş (X, Z) ama nispeten ince (Y) veya sadece çok geniş
            // Alan hesabı (Yatay)
            float area = size.x * size.z;
            
            // Min alan filtresi (Küçük taşlar, kutular zemin değildir)
            if (area < 25f) continue; // 5x5'ten küçükse geç

            // "En büyük" yüzeyi zemin kabul et
            if (area > maxSurfaceArea)
            {
                // Y değeri olarak objenin en üst noktasını al (üzerine basalım diye)
                // Ama çok yüksek objeler (duvar gibi) olmasın. 
                // Zemin genelde yassıdır: size.y < size.x ve size.y < size.z
                if (size.y < size.x * 0.5f && size.y < size.z * 0.5f) // Yassı obje kontrolü
                {
                    maxSurfaceArea = area;
                    bestGroundY = r.bounds.max.y; // En tepe noktası
                    foundVisualGround = true;
                    // Debug.Log($"[SpawnManager] Aday Zemin: {r.name}, Alan: {area}, Y: {bestGroundY}");
                }
            }
        }

        if (foundVisualGround)
        {
            groundY = bestGroundY;
            groundYDetected = true;
            Debug.Log($"[SpawnManager] GÖRSEL Zemin bulundu (En Büyük Yüzey): Y = {groundY}");
            return;
        }

        // 2. Eğer görsel bulunamadıysa İsimle Ara
        string[] floorNames = { "Floor", "Plane", "Ground", "Hole_Compatible_Floor", "Zemin", "Terrain", "Base", "Platform", "Snow", "Ice" };
        foreach (string name in floorNames)
        {
            Transform floor = mapRoot != null ? mapRoot.Find(name) : null;
            if (floor == null) floor = GameObject.Find(name)?.transform;
            
            if (floor != null)
            {
                groundY = floor.position.y;
                groundYDetected = true;
                Debug.Log($"[SpawnManager] Zemin bulundu (İsimle): {name} | Y = {groundY}");
                return;
            }
        }

        // 3. O da yoksa yine NavMesh dene (Son çare)
        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, 50f, 0), out navHit, 200f, UnityEngine.AI.NavMesh.AllAreas))
        {
            groundY = navHit.position.y;
            groundYDetected = true;
            Debug.Log($"[SpawnManager] Zemin bulundu (NavMesh): Y = {groundY}");
            return;
        }
        
        // 4. Fallback
        groundY = 0f;
        Debug.LogWarning("[SpawnManager] Zemin tespit edilemedi! Varsayılan Y=0 kullanılıyor.");
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
        // Kullanıcı isteği: Otomatik spawn yerine butonla spawn istendi.
        // skillSpawningEnabled = true;
        // ScheduleNextSkillSpawn(); 
        Debug.Log("[SpawnManager] Auto skill spawning disabled (User controls via buttons).");
    }
    
    // BUTONLA ÇAĞRILACAK YENİ METOT
    public void SpawnSkillImmediately(SkillType type, bool isPermanent)
    {
        GameObject prefabToSpawn = null;

        switch (type)
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

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[SpawnManager] {type} Prefab atanmamış!");
            return;
        }

        // Oyuncunun yakınına yer bul
        Vector3 spawnPos = FindSkillSpawnPosition();

        // Spawn et
        GameObject pickup = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        // Ayarlarını yap
        SkillPickup skillComponent = pickup.GetComponent<SkillPickup>();
        if (skillComponent != null)
        {
            skillComponent.skillType = type;
            skillComponent.isPermanentPickup = isPermanent; // Kalıcılık ayarı
        }
        
        activeSkillPickups.Add(pickup);
        Debug.Log($"[SpawnManager] Skill INSTANTLY spawned: {type} at {spawnPos} (Permanent: {isPermanent})");
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
    }
    
    void Update()
    {
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
        // Sonraki spawnlar normal aralıkta (15-30sn)
        nextSkillSpawnTime = Time.time + Random.Range(skillSpawnMinInterval, skillSpawnMaxInterval);
    }
    
    void TrySpawnSkillPickup()
    {
        // Max limite ulaşıldı mı?
        if (activeSkillPickups.Count >= maxSkillPickupsOnMap)
        {
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
            Debug.LogWarning($"[SpawnManager] {randomSkill} Prefab atanmamış! Lütfen Inspector'dan atayın.");
            return;
        }
        
        // Spawn pozisyonu bul
        Vector3 spawnPos = FindSkillSpawnPosition();
        
        // Spawn!
        GameObject pickup = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        
        // Skill tipini garantiye al
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
        // --- OYUNCUNUN YAKININA SPAWN ET ---
        HoleMechanics player = FindObjectOfType<HoleMechanics>();
        Vector3 centerPos = Vector3.zero;
        
        if (player != null)
        {
            centerPos = player.transform.position;
        }
        else
        {
            Debug.LogWarning("[SpawnManager] HoleMechanics (Player) bulunamadı! Harita merkezi kullanılıyor.");
            if (currentSpawnBounds.size.sqrMagnitude > 0.1f) centerPos = currentSpawnBounds.center;
        }

        int maxAttempts = 20;
        float minDistance = 3f; 
        float maxDistance = 8f; 
        
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidatePos;
            
            // Oyuncu etrafında rastgele nokta
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minDistance, maxDistance);
            
            // Yön hesabı
            candidatePos = centerPos + new Vector3(randomDir.x, 0f, randomDir.y) * randomDist;
            
            // Bounds içinde mi?
            candidatePos = ClampToBounds(candidatePos);
            
            // Yüksekliği ayarla (zeminden 3.5m yukarı - daha yüksekten düşsün ki görünsün)
            candidatePos.y = groundY + 3.5f;
            
            // Engel kontrolü
            Vector3 groundCheckPos = new Vector3(candidatePos.x, groundY + 0.5f, candidatePos.z);
            if (!IsPositionBlockedByObstacle(groundCheckPos))
            {
                // Diğer pickup'lara çok yakın mı?
                bool tooClose = false;
                foreach (var existingPickup in activeSkillPickups)
                {
                    if (existingPickup != null && Vector3.Distance(existingPickup.transform.position, candidatePos) < 2f)
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
        
        // FALLBACK: Eğer 20 denemede yer bulamazsa, ZORLA spawn et (Görülsün)
        // Oyuncunun biraz ilerisine, havadan at
        Debug.Log("[SpawnManager] Uygun boş yer bulunamadı, FALLBACK pozisyona spawn ediliyor.");
        Vector3 fallbackPos = centerPos + Vector3.forward * 4f + Vector3.up * 4f;
        
        // Bounds dışına çıkmasın yine de
        fallbackPos = ClampToBounds(fallbackPos);
        fallbackPos.y = groundY + 4.5f; // Yükseklik korunsun
        
        return fallbackPos;
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
    
    /// <summary>
    /// SkillManager tarafından çağrılır - Satın alınan skill'i spawn eder
    /// </summary>
    public void SpawnSkillPickupForMarket(SkillType skillType)
    {
        GameObject prefabToSpawn = skillType switch
        {
            SkillType.Magnet => magnetPickupPrefab,
            SkillType.Speed => speedPickupPrefab,
            SkillType.Shield => shieldPickupPrefab,
            _ => null
        };
        
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[SpawnManager] Market: {skillType} prefab bulunamadı! Skill doğrudan aktif ediliyor.");
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.ActivateSkill(skillType);
            }
            return;
        }
        
        // Hole yakınında spawn et
        Vector3 spawnPos = FindSkillSpawnPositionNearHole();
        
        GameObject pickup = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        
        SkillPickup skillComponent = pickup.GetComponent<SkillPickup>();
        if (skillComponent != null)
        {
            skillComponent.skillType = skillType;
        }
        
        activeSkillPickups.Add(pickup);
        Debug.Log($"[SpawnManager] Market Skill Pickup spawned: {skillType} at {spawnPos}");
    }
    
    /// <summary>
    /// Hole yakınında spawn pozisyonu bul (Market satın alımları için)
    /// </summary>
    private Vector3 FindSkillSpawnPositionNearHole()
    {
        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        Vector3 basePos = hole != null ? hole.transform.position : Vector3.zero;
        
        // Hole'un 3-5 birim ilerisinde rastgele pozisyon
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
            Vector3 candidatePos = basePos + new Vector3(randomOffset.x, 0, randomOffset.y);
            
            // Y değerini zemine ayarla (Görünür olması için 1.5f yukarı)
            candidatePos.y = groundY + 1.5f;
            
            // Engel kontrolü
            if (!IsPositionBlockedByObstacle(candidatePos))
            {
                return candidatePos;
            }
        }
        
        // Fallback: Hole'un biraz önünde
        return basePos + new Vector3(4f, groundY + 1.5f, 0f);
    }
}
