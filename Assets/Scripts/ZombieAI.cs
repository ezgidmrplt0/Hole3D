using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ZombieAI : CharacterAI
{
    [Header("Level Info")]
    public int level = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 1; // 1 tane yiyince level atlar
    public TMP_Text levelText;
    private Transform camTransform;

    [Header("Behavior")]
    public string preyTag = "Human";
    public float detectionRange = 8f;
    public float runTriggerDistance = 8f; // Bu mesafeden yakındaysa KOŞ, uzaksa YÜRÜ
    public float wanderDurationAfterCollision = 1.0f;

    [Header("Think Tick")]
    public float thinkInterval = 0.2f; // Pahali hedef taramasini aralikli yap

    private float wanderTimer = 0f;
    private Vector3 wanderDirection;

    [Header("Roaming")]
    public float wanderRadius = 5f;
    public float roamInterval = 4f;
    private Vector3 roamTarget;
    private float roamTimer;
    private float thinkTimer;
    private Transform currentPrey;

    [Header("Cage Logic")]
    public bool isTrapped = false;

    public void ReleaseFromCage()
    {
        isTrapped = false;
        // Opsiyonel: Kafesten çıkınca hemen kaçmaya veya saldırmaya başla
        PickNewRoamTarget();
    }

    void Start()
    {
        // --- TAG GÜVENLİĞİ ---
        // Prefab'ın tag'i yanlış ayarlanmış olabilir, zorla düzelt
        if (!gameObject.CompareTag("Zombie"))
        {
            gameObject.tag = "Zombie";
            Debug.LogWarning($"ZombieAI: {gameObject.name} had wrong tag, fixed to 'Zombie'");
        }
        
        camTransform = Camera.main.transform;
        
        // Inspector'dan ne gelirse gelsin, kural olarak 1 insan = 1 level olsun
        xpToNextLevel = 1; 

        // Zombi hızını biraz düşürelim
        runSpeed = 2.5f; 
        walkSpeed = 1.5f;

        // UI Yoksa Otomatik Oluştur
        if (levelText == null)
        {
            CreateLevelUI();
        }

        UpdateLevelText();
        
        PickNewRoamTarget();
        thinkTimer = Random.Range(0f, thinkInterval);
    }

    public void SetLevel(int newLevel)
    {
        level = newLevel;
        
        // Scale'i level ile orantılı artır
        // Level 1: 1.0x
        // Level 2: 1.5x
        // Level 3: 2.0x
        float scaleFactor = 1.0f + ((level - 1) * 0.5f);
        transform.localScale = Vector3.one * scaleFactor;

        UpdateLevelText();
    }

    void CreateLevelUI()
    {
        // Canvas Oluştur (World Space)
        GameObject canvasObj = new GameObject("LevelCanvas");
        canvasObj.transform.SetParent(this.transform);
        canvasObj.transform.localPosition = new Vector3(0, 2.5f, 0); // Kafanın üstü
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        // Canvas Scaler (veya manuel scale) - World Space olduğu için çok küçük olmalı
        RectTransform rect = canvasObj.GetComponent<RectTransform>();

        rect.sizeDelta = new Vector2(5, 2); // Daha geniş yap ki sığsın
        rect.localScale = Vector3.one * 0.05f; 

        // Text Oluştur
        GameObject textObj = new GameObject("LevelText");
        textObj.transform.SetParent(canvasObj.transform);
        textObj.transform.localPosition = Vector3.zero;
        textObj.transform.localRotation = Quaternion.identity;

        levelText = textObj.AddComponent<TextMeshProUGUI>();
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.fontSize = 12; 
        levelText.color = Color.red;
        levelText.fontStyle = FontStyles.Bold;
        levelText.enableWordWrapping = false;
        levelText.overflowMode = TextOverflowModes.Overflow; // Taşarsa da göster
        
        // Rect ayarları
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.localScale = Vector3.one;
    }

    void UpdateLevelText()
    {
        if (levelText != null) 
        {
            if (level <= 1)
            {
                levelText.text = "";
            }
            else
            {
                // Level 2 -> +1
                // Level 3 -> +2
                levelText.text = "+" + (level - 1);
            }
        }
    }

    void Update()
    {
        // Texti her zaman güncelle
        if (levelText != null && camTransform != null)
        {
            levelText.transform.parent.rotation = camTransform.rotation;
        }

        // Eğer Trapped ise, sadece küçük bir alanda dolaşmasına izin ver (Chase yok)
        if (isTrapped)
        {
            PerformRoaming();
            return; // Chase ve diğer logiclere girme
        }

        // Eğer çarpışma sonrası serseri modundaysak
        if (wanderTimer > 0)
        {
            wanderTimer -= Time.deltaTime;
            Move(wanderDirection, false); // Çarpınca YÜRÜYEREK uzaklaş (Sakince)
            return;
        }

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = thinkInterval;
            // Pahali sorgu: yalnizca think tick geldiginde calissin
            currentPrey = GetClosestPrey();
        }

        // Her frame ucuz dogrulama
        if (currentPrey != null)
        {
            if (!currentPrey.gameObject.activeInHierarchy)
            {
                currentPrey = null;
            }
            else
            {
                float distSqr = (currentPrey.position - transform.position).sqrMagnitude;
                float releaseRange = detectionRange * 1.5f;
                if (distSqr > releaseRange * releaseRange) currentPrey = null;
            }
        }

        Transform prey = currentPrey;
        if (prey != null)
        {
            float distanceToPrey = Vector3.Distance(transform.position, prey.position);
            Vector3 chaseDirection = prey.position - transform.position;

            // Eğer mesafe "Run Trigger"dan kısaysa KOŞ, yoksa sinsi sinsi YÜRÜ
            bool shouldRun = distanceToPrey < runTriggerDistance;
            
            Move(chaseDirection, shouldRun);
        }
        else
        {
            // Hedef yoksa RASTGELE GEZ (Yürü)
            PerformRoaming();
        }
    }

    private void PerformRoaming()
    {
        roamTimer -= Time.deltaTime;
        if (roamTimer <= 0)
        {
            PickNewRoamTarget();
            roamTimer = roamInterval;
        }

        Vector3 dir = roamTarget - transform.position;
        if (dir.magnitude < 0.5f)
        {
                // Hedefe varınca bekle
                Move(Vector3.zero, false);
        }
        else
        {
            Move(dir, false); // Boşta gezerken YÜRÜ
        }
    }

    private void PickNewRoamTarget()
    {
        // --- TRAPPED LOGIC ---
        // Kafesteysek uzaklara gitme, olduğun yerde dolan
        if (isTrapped)
        {
            Vector3 randomDir = Random.insideUnitSphere * 2f; // Küçük alan (Kafes içi)
            roamTarget = transform.position + randomDir;
            roamTarget.y = expectedGroundY;
            return;
        }

        // Önce yakın çevrede (25m) insan var mı diye bak (Koku alma duyusu kısıtlandı - Idea 3)
        Collider[] nearbyHumans = Physics.OverlapSphere(transform.position, 25f);
        List<Transform> humansInRange = new List<Transform>();
        
        foreach (var hit in nearbyHumans)
        {
            if (hit.CompareTag(preyTag)) humansInRange.Add(hit.transform);
        }

        Vector3 candidatePos;

        if (humansInRange.Count > 0)
        {
            // Rastgele bir insan seç ve ona doğru git
            Transform targetHuman = humansInRange[Random.Range(0, humansInRange.Count)];
            
            // Tam üstüne gitmesin, o bölgeye gitsin (Sürü psikolojisi)
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            candidatePos = targetHuman.position + randomOffset;
        }
        else
        {
            // Hiç insan kalmadıysa olduğu yerde gezinmeye devam et
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            candidatePos = transform.position + randomDir;
        }

        // --- SAFE CHECK ---
        // Hedef geçerli bir zemin üzerinde mi?
        candidatePos.y = expectedGroundY; // Zemin seviyesi (CharacterAI'den gelir)
        
        if (IsOnGround(candidatePos))
        {
            roamTarget = candidatePos;
        }
        else
        {
            // Değilse merkeze (0,0) doğru bir nokta seç
            roamTarget = Vector3.Lerp(transform.position, Vector3.zero, 0.5f);
        }
    }

    private bool IsOnGround(Vector3 pos)
    {
        if (groundLayer.value == 0) return true; // Layer seçilmemişse her yer zemin
        
        // Yukarıdan aşağı bak
        // 10 birim yukarıdan 50 birim aşağı (Map çok eğimli olabilir)
        return Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, 50f, groundLayer);
    }

    private Transform GetClosestPrey()
    {
        // Performans için her frame tüm sahneyi taramak yerine
        // Sadece yakın çevreyi tarayabiliriz veya global listeden çekeriz.
        // Şimdilik OverlapSphere. Daha optimize: GameManager'dan liste çekmek.
        
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange);
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(preyTag))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit.transform;
                }
            }
        }
        return closest;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 1. YEME MANTIĞI: İnsanla çarpışırsa
        if (collision.gameObject.CompareTag(preyTag))
        {
            // İnsanı yok et (Yendi!)
            Destroy(collision.gameObject);

            // LevelManager'a bildir (İnsan counter'ı düşsün)
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnHumanEatenByZombie();
            }

            // Zombiyi büyüt (Her yemekte azıcık büyüsün, ödül olsun)
            transform.localScale = Vector3.Min(transform.localScale * 1.05f, Vector3.one * 3f);
            
            return; 
        }

        // 2. DUVAR/ENGEL MANTIĞI
        // Duvara veya herhangi bir engele çarparsa
        foreach (ContactPoint contact in collision.contacts)
        {
            // Normalin Y değeri düşükse (Duvar vs.)
            if (Mathf.Abs(contact.normal.y) < 0.6f) 
            {
                // 1. "Reflect" (Yansıma) kullanarak daha yumuşak bir dönüş sağla
                Vector3 reflection = Vector3.Reflect(transform.forward, contact.normal);
                
                wanderDirection = reflection.normalized;
                wanderTimer = 0.5f; // 0.5 saniye boyunca bu yeni yöne git

                // 2. Roam hedefini de bu yeni yöne taşı
                // Böylece bounce bittiğinde (eğer av kovalamıyorsa) tekrar duvara dönmez.
                roamTarget = transform.position + wanderDirection * wanderRadius;
                roamTimer = roamInterval;

                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
