using UnityEngine;

public class CharacterAI : MonoBehaviour
{
    [Header("Movement")]
    public float runSpeed = 5f;
    public float walkSpeed = 2f;
    public float rotationSpeed = 5f;

    [Header("Obstacle Avoidance")]
    public float detectionRadius = 1.5f;
    public LayerMask obstacleLayer;
    [Tooltip("Layer to check for ground. If raycast down fails, it's an edge.")]
    public LayerMask groundLayer;
    
    [Header("AI Settings")]
    public bool enableObstacleAvoidance = true;

    protected Rigidbody rb;
    protected Vector3 moveDirection;
    protected Animator animator;
    
    // Animator param kontrolü
    private bool hasIsRunParam = false;
    protected bool isAnimatorValid = false; // Animasyon sisteminin güvenli olup olmadığını tutar

    protected virtual void Awake()
    {
        // 0. Setup Animator (Akıllı Arama)
        // Bazen çocuk objelerde birden fazla Animator olabilir (aksesuarlar vs.)
        // Doğru olanı (parametreleri içeren ana karakteri) bulmalıyız.
        Animator[] allAnimators = GetComponentsInChildren<Animator>();
        
        // Önce temizle
        isAnimatorValid = false;
        animator = null;

        foreach (var anim in allAnimators)
        {
            if (anim.runtimeAnimatorController == null) continue;

            bool localHasWalk = false;
            bool localHasRun = false;
            bool localHasIsRun = false;
            
            foreach (var param in anim.parameters)
            {
                if (param.name == "walk") localHasWalk = true;
                if (param.name == "run") localHasRun = true;
                if (param.name == "IsRun") localHasIsRun = true;
            }

            // Eğer bu animatörde yürüme/koşma parametreleri varsa, doğru olan budur!
            if (localHasWalk || localHasRun)
            {
                animator = anim;
                isAnimatorValid = true; 
                if (localHasIsRun) hasIsRunParam = true;
                break; 
            }
        }

        // Eğer uyumlu bir animatör bulamadıysak, ilkiyle yetin
        if (!isAnimatorValid && allAnimators.Length > 0)
        {
             animator = allAnimators[0];
        }

        // --- GROUND LAYER GÜVENLİK ---
        // Eğer kullanıcı Ground Layer'ı seçmeyi unuttuysa, "Everything" yaparak her şeye basmasını sağla.
        if (groundLayer == 0 || groundLayer == LayerMask.NameToLayer("Nothing"))
        {
            groundLayer = ~0; // Everything
        }
        
        // --- ZEMIN SEVİYESİNİ OTOMATIK BUL ---
        // Plane'in Y değerini bul (Genelde 0 ama farklı olabilir)
        DetectGroundLevel();


        // 1. Remove old NavMeshAgent if present
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null) Destroy(navAgent);

        // 2. Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        // Freeze Y rotation AND position to prevent climbing
        rb.constraints = RigidbodyConstraints.FreezeRotation; 
        rb.useGravity = true;
        
        // Stabilize Physics - Hafif drag ile tırmanmayı zorlaştır
        rb.drag = 1f; 
        rb.angularDrag = 5f;
        
        // Normal ağırlık (Magnet ile uyumlu)
        rb.mass = 1.5f;


        // 3. Setup Collider (Zombilerin içine gömülmemesi için)
        // Invalid position check at startup
        if (IsPositionInvalid(transform.position))
        {
             Debug.LogWarning($"CharacterAI: {gameObject.name} initiated at invalid position {transform.position}. Resetting to zero.");
             transform.position = new Vector3(0, expectedGroundY + 0.5f, 0);
        }

        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider>();
            // Genelde modellerin pivotu ayak olduğu için Center Y = Height/2 olmalı.
            collider.height = 1.8f;
            collider.center = new Vector3(0, 0.9f, 0); // Tam yukarı kaldır
            collider.radius = 0.3f; // Biraz incelt
        }
        else
        {
            // Var olan collider'ı da düzelt
            if (collider.center.y < 0.5f) collider.center = new Vector3(collider.center.x, 0.9f, collider.center.z);
        }

        // --- PHYSICS FIX: Slippery Material ---
        // Karakterlerin birbirine veya duvara sürtünüp "tırmanmasını" (havaya kalkmasını) engeller.
        PhysicMaterial slipperyMat = new PhysicMaterial("SlipperyChar");
        slipperyMat.dynamicFriction = 0f;
        slipperyMat.staticFriction = 0f;
        slipperyMat.bounciness = 0f;
        slipperyMat.frictionCombine = PhysicMaterialCombine.Minimum;
        slipperyMat.bounceCombine = PhysicMaterialCombine.Minimum;
        collider.material = slipperyMat;

        // Daha stabil fizik için ayarlar
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    [Header("Ground Check")]
    [Tooltip("Karakterin olması gereken zemin Y seviyesi (Plane Y değeri)")]
    public float expectedGroundY = 0f;
    [Tooltip("Bu değerden fazla yüksekteyse zorla aşağı indirilir")]
    public float heightTolerance = 0.3f; // Daha katı tolerans
    
    // Maximum allowed height above ground (hard clamp)
    private const float MAX_HEIGHT_ABOVE_GROUND = 1.5f; // 0.5'ten 1.5'e artırıldı (düşmeye izin ver)
    
    // Magnet/Dış kuvvet tarafından çekiliyorsa true
    [HideInInspector]
    public bool isBeingPulled = false;

    protected virtual void LateUpdate()
    {
        // Eğer dış kuvvet (magnet vs) tarafından çekiliyorsa, pozisyon kontrollerini ATLA
        if (isBeingPulled) return;
        
        // 1. Invalid Position Check
        if (IsPositionInvalid(transform.position))
        {
             Debug.LogWarning($"{gameObject.name} panicked (Invalid Position)! Resetting.");
             transform.position = new Vector3(0, expectedGroundY + 0.1f, 0);
             if (rb != null) rb.velocity = Vector3.zero;
        }

        // 2. FALLING CHECK (Yere Düşme Koruması)
        // Eğer karakter haritanın ÇOK altına düşerse (delik vs), onu merkeze ışınla
        // Ama normal düşüşe izin ver
        if (transform.position.y < expectedGroundY - 5f)
        {
            // Çok aşağı düştü! Kurtar.
            transform.position = new Vector3(0, expectedGroundY + 0.5f, 0);
            if (rb != null) 
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        // 3. YÜKSEKTE KALMA KONTROLÜ - SERT CLAMP
        // Karakter asla plane Y'sinin belirli bir miktarından fazla yukarıda olamaz
        float maxAllowedY = expectedGroundY + MAX_HEIGHT_ABOVE_GROUND;
        float currentHeight = transform.position.y;
        
        if (currentHeight > maxAllowedY)
        {
            // Zorla aşağı indir - ANINDA
            Vector3 pos = transform.position;
            pos.y = expectedGroundY + 0.1f; // Doğrudan zemine indir
            transform.position = pos;
            
            // Velocity'yi sıfırla
            if (rb != null)
            {
                Vector3 vel = rb.velocity;
                vel.y = 0;
                rb.velocity = vel;
            }
            
            Debug.Log($"{gameObject.name}: Forced down from {currentHeight} to ground level {expectedGroundY}");
        }
        
        // Eski merkeze itme mantığı kaldırıldı - artık doğrudan zemine indiriyoruz
    }
    
    protected virtual void FixedUpdate()
    {
        // Eğer dış kuvvet tarafından çekiliyorsa, clamp yapma
        if (isBeingPulled) return;
        
        // FixedUpdate'de de yükseklik kontrolü - fizik ile senkronize
        ClampHeightToGround();
    }
    
    private void ClampHeightToGround()
    {
        if (rb == null) return;
        
        float maxAllowedY = expectedGroundY + MAX_HEIGHT_ABOVE_GROUND;
        
        // Eğer karakter çok yüksekteyse
        if (transform.position.y > maxAllowedY)
        {
            // Pozisyonu düzelt - doğrudan zemine indir
            Vector3 pos = transform.position;
            pos.y = expectedGroundY + 0.1f;
            rb.MovePosition(pos);
            
            // Velocity'yi sıfırla
            Vector3 vel = rb.velocity;
            vel.y = 0;
            rb.velocity = vel;
        }
        
        // Eğer karakter yukarı doğru hareket ediyorsa ve zaten yüksekteyse, engelle
        if (transform.position.y > expectedGroundY + heightTolerance && rb.velocity.y > 0.1f)
        {
            Vector3 vel = rb.velocity;
            vel.y = 0;
            rb.velocity = vel;
        }
    }

    private bool IsPositionInvalid(Vector3 pos)
    {
        return pos.sqrMagnitude > 1000000f || float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
    }
    
    private void DetectGroundLevel()
    {
        // KRITIK: Sadece gerçek zemin objelerini ara (Floor, Plane vs.)
        // Raycast'e güvenme çünkü taşlara da çarpabilir!
        
        // 1. Önce sahnede ismi "Floor", "Plane", "Ground" vs olan objeyi ara
        string[] floorNames = { "Floor", "Plane", "Hole_Compatible_Floor", "SimplePlane", "SpecialPlane" };
        
        foreach (string name in floorNames)
        {
            GameObject floor = GameObject.Find(name);
            if (floor != null)
            {
                // Objenin renderer veya collider bounds'undan Y değerini al
                Renderer r = floor.GetComponent<Renderer>();
                if (r != null)
                {
                    // Renderer'ın üst yüzeyini al
                    expectedGroundY = r.bounds.max.y;
                    Debug.Log($"CharacterAI: Ground detected from {name} renderer, Y = {expectedGroundY}");
                    return;
                }
                
                Collider c = floor.GetComponent<Collider>();
                if (c != null)
                {
                    expectedGroundY = c.bounds.max.y;
                    Debug.Log($"CharacterAI: Ground detected from {name} collider, Y = {expectedGroundY}");
                    return;
                }
                
                // Fallback: transform position
                expectedGroundY = floor.transform.position.y;
                Debug.Log($"CharacterAI: Ground detected from {name} transform, Y = {expectedGroundY}");
                return;
            }
        }
        
        // 2. SpawnManager'dan groundY al (eğer varsa)
        if (SpawnManager.Instance != null && SpawnManager.Instance.groundY != 0f)
        {
            expectedGroundY = SpawnManager.Instance.groundY;
            Debug.Log($"CharacterAI: Ground detected from SpawnManager, Y = {expectedGroundY}");
            return;
        }
        
        // 3. Sahnenin en düşük noktasını bul (sadece düz, yatay objeler)
        // Tag'e güvenme! İsmine bak.
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        float lowestFloorY = float.MaxValue;
        bool foundFloor = false;
        
        foreach (var obj in allObjects)
        {
            string objName = obj.name.ToLower();
            // Sadece floor/plane isimleri kabul et
            if (objName.Contains("floor") || objName.Contains("plane"))
            {
                // Taş, kristal vs olmadığından emin ol
                if (objName.Contains("rock") || objName.Contains("stone") || objName.Contains("crystal")) continue;
                
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    float topY = r.bounds.max.y;
                    if (topY < lowestFloorY)
                    {
                        lowestFloorY = topY;
                        foundFloor = true;
                    }
                }
            }
        }
        
        if (foundFloor)
        {
            expectedGroundY = lowestFloorY;
            Debug.Log($"CharacterAI: Ground detected from scene scan, Y = {expectedGroundY}");
            return;
        }
        
        // 4. Son çare: Varsayılan 0 kullan
        expectedGroundY = 0f;
        Debug.LogWarning("CharacterAI: Could not detect ground level, using default Y = 0");
    }

    protected virtual void OnEnable()
    {
        // Sıkışma Kontrolü Başlat (Obje açılıp kapandığında tekrar çalışsın)
        StartCoroutine(CheckStuckRoutine());
    }

    // --- Sıkışma (Stuck) Algılama Değişkenleri ---
    private Vector3 lastStuckPos;
    private bool isEscaping = false;
    private float escapeTimer = 0f;
    private Vector3 escapeDirection;

    private System.Collections.IEnumerator CheckStuckRoutine()
    {
        while (true)
        {
            // Her 0.5 saniyede bir kontrol et
            lastStuckPos = transform.position;
            yield return new WaitForSeconds(0.5f);

            // Eğer "kaçış modunda" değilsek ve hareket etmeye çalışıyorsak (moveDirection dolu)
            if (!isEscaping && moveDirection.magnitude > 0.1f)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastStuckPos);
                
                // Eğer 0.5 saniyede neredeyse hiç ilerleyemediysek (Sıkıştık!)
                if (distanceMoved < 0.2f)
                {
                    // Sıkışma algılandı! Kurtarma moduna geç.
                    StartEscapeManeuver();
                }
            }
        }
    }

    private void StartEscapeManeuver()
    {
        isEscaping = true;
        escapeTimer = 1.5f; // 1.5 saniye boyunca kurtulmaya çalış

        // Rastgele açık bir yön bulmaya çalış
        escapeDirection = -transform.forward; // Varsayılan: Geri git
        
        // 4 bir yana bakıp boş olanı seçmeye çalışalım
        Vector3[] potentialDirs = { -transform.forward, transform.right, -transform.right, transform.forward + transform.right };
        
        foreach (var dir in potentialDirs)
        {
            if (!Physics.Raycast(transform.position + Vector3.up, dir, 2f, obstacleLayer))
            {
                escapeDirection = dir;
                break;
            }
        }
        
        escapeDirection.Normalize();
    }

    protected void Move(Vector3 targetDirection, bool isRunning)
    {
        // Eğer dış kuvvet tarafından çekiliyorsa, kendi hareketimizi yapma
        if (isBeingPulled) return;
        
        // Infinity/NaN Check to prevent confusing errors
        if (float.IsNaN(targetDirection.x) || float.IsNaN(targetDirection.y) || float.IsNaN(targetDirection.z)) return;

        // --- Sıkışma Kurtarma Mantığı ---
        if (isEscaping)
        {
            escapeTimer -= Time.deltaTime;
            if (escapeTimer <= 0)
            {
                isEscaping = false;
            }
            else
            {
                // Kaçış modundaysak, gelen hedefi yoksay ve kaçış yönüne git
                targetDirection = escapeDirection;
                isRunning = false; // Sakin çıkalım
            }
        }

        // Temel hareket yönü
        moveDirection = targetDirection.normalized;

        // Hızı belirle
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Engel kontrolü
        if (enableObstacleAvoidance)
        {
            Vector3 avoidance = AvoidObstacles(moveDirection);
            
            // Eğer engel varsa yönü değiştir
            if (avoidance != Vector3.zero)
            {
                moveDirection += avoidance;
                moveDirection.Normalize();
            }
        }

        // Hareketi uygula
        Vector3 velocity = moveDirection * currentSpeed;
        
        // Y eksenini koru (Fizik motoruna saygı duy)
        if (rb != null)
        {
             velocity.y = rb.velocity.y;
             
             // Check against explosion
             if (Mathf.Abs(velocity.y) > 50f) velocity.y = 50f; // Limit vertical speed
             
             // KRITIK: Eğer zaten yüksekteyse ve yukarı gidiyorsa, engelle
             if (transform.position.y > expectedGroundY + heightTolerance && velocity.y > 0)
             {
                 velocity.y = 0; // Yukarı gitmeyi engelle
             }
        }
        
        // --- CRITICAL SAFETY CHECK ---
        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z) || 
            float.IsInfinity(velocity.x) || float.IsInfinity(velocity.y) || float.IsInfinity(velocity.z))
        {
            Debug.LogWarning($"{gameObject.name}: NaN/Infinity Velocity Detected! Resetting.");
            velocity = Vector3.zero;
        }

        if (rb != null) rb.velocity = velocity;

        // Dönüş (Güvenli)
        // Vector3.zero veya çok küçük vektörler LookRotation'ı bozar
        Vector3 lookDir = new Vector3(moveDirection.x, 0, moveDirection.z);
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Animasyon Güncelleme
        if (animator != null)
        {
            // Kullanıcının istediği 'IsRun' parametresi (Varsa kullan)
            if (hasIsRunParam)
            {
                animator.SetBool("IsRun", isRunning);
            }

            // Trigger Mantığı (State takibi ile)
            string targetState = "idle";
            
            if (moveDirection.magnitude > 0.01f)
            {
                targetState = isRunning ? "run" : "walk";
            }

            if (currentAnimState != targetState)
            {
                currentAnimState = targetState;
                
                if (isAnimatorValid)
                {
                    animator.CrossFadeInFixedTime(targetState, 0.2f);
                }
            }
        }
    }

    private string currentAnimState = "";

    private Vector3 AvoidObstacles(Vector3 desiredDir)
    {
        // Use desired direction for checks, fallback to forward if zero
        Vector3 checkDir = desiredDir.magnitude > 0.1f ? desiredDir : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // 1. UÇURUM KONTROLÜ (Edge Detection) - DAHA HASSAS
        // Hızla orantılı olarak daha uzağa bak
        float lookAheadDist = detectionRadius * 1.5f; 
        Vector3 lookAheadPos = transform.position + checkDir * lookAheadDist;
        
        Ray downRay = new Ray(lookAheadPos + Vector3.up * 2f, Vector3.down);
        
        // Ground Layer kontrolü
        if (groundLayer.value != 0)
        {
            // Aşağıda zemin yoksa? (Daha derin kontrol: 5 birim)
            if (!Physics.Raycast(downRay, 5f, groundLayer))
            {
                // UÇURUM VAR!
                // Geri dön (Merkeze doğru kaçış vektörü de eklenebilir)
                Vector3 toCenter = (Vector3.zero - transform.position).normalized;
                
                // Debug.DrawRay(lookAheadPos, Vector3.up * 5f, Color.red, 1f);
                return (toCenter + (-checkDir)).normalized * 3f; // Güçlü reddetme
            }
        }

        // 2. DUVAR/ENGEL KONTROLÜ
        Ray ray = new Ray(origin, checkDir);
        if (Physics.Raycast(ray, detectionRadius, obstacleLayer))
        {
            // Sağa mı sola mı kaçalım?
            Vector3 right = Vector3.Cross(Vector3.up, checkDir);
            
            if (!Physics.Raycast(origin, right, detectionRadius, obstacleLayer))
                return right * 2f; 
            else if (!Physics.Raycast(origin, -right, detectionRadius, obstacleLayer))
                return -right * 2f; 
            
            return -checkDir; // Geri dön
        }
        
        return Vector3.zero;
    }
}
