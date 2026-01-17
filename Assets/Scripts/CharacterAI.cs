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

        // 1. Remove old NavMeshAgent if present
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null) Destroy(navAgent);

        // 2. Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.constraints = RigidbodyConstraints.FreezeRotation; 
        rb.useGravity = true;

        // 3. Setup Collider
        // Invalid position check at startup
        if (IsPositionInvalid(transform.position))
        {
             Debug.LogWarning($"CharacterAI: {gameObject.name} initiated at invalid position {transform.position}. Resetting to zero.");
             transform.position = new Vector3(0, 5f, 0);
        }

        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.center = new Vector3(0, 1f, 0);
            collider.radius = 0.5f;
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

    protected virtual void LateUpdate()
    {
        // Safety Check: If flying into void/NaN, reset instead of destroy
        if (IsPositionInvalid(transform.position))
        {
             Debug.LogWarning($"{gameObject.name} panicked (Invalid Position)! Resetting to safe point.");
             transform.position = new Vector3(0, 2f, 0);
             if (rb != null) rb.velocity = Vector3.zero;
        }
    }

    private bool IsPositionInvalid(Vector3 pos)
    {
        return pos.sqrMagnitude > 1000000f || float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
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

        // Engel kontrolü
        Vector3 avoidance = AvoidObstacles(moveDirection);
        
        // Eğer engel varsa yönü değiştir
        if (avoidance != Vector3.zero)
        {
            moveDirection += avoidance;
            moveDirection.Normalize();
        }

        // Hızı belirle
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Hareketi uygula
        Vector3 velocity = moveDirection * currentSpeed;
        
        // Y eksenini koru (Fizik motoruna saygı duy)
        if (rb != null)
        {
             velocity.y = rb.velocity.y;
             
             // Check against explosion
             if (Mathf.Abs(velocity.y) > 50f) velocity.y = 50f; // Limit vertical speed
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
        Vector3 forward = transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Yerden biraz yukarıdan

        // 1. UÇURUM KONTROLÜ (Edge Detection)
        Vector3 lookAheadPos = transform.position + forward * detectionRadius;
        Ray downRay = new Ray(lookAheadPos + Vector3.up, Vector3.down);
        
        if (groundLayer.value != 0)
        {
            // 3 birim aşağıda zemin yoksa geri dön
            if (!Physics.Raycast(downRay, 3f, groundLayer))
            {
                // Debug.DrawRay(lookAheadPos + Vector3.up, Vector3.down * 3f, Color.magenta); 
                return -forward * 2f; 
            }
        }

        // 2. DUVAR/ENGEL KONTROLÜ
        Ray ray = new Ray(origin, forward);
        if (Physics.Raycast(ray, detectionRadius, obstacleLayer))
        {
            // Debug.DrawRay(origin, forward * detectionRadius, Color.red);
            
            // Sağa mı sola mı kaçalım?
            if (!Physics.Raycast(origin, transform.right, detectionRadius, obstacleLayer))
                return transform.right * 2f; // Sağa kaç
            else if (!Physics.Raycast(origin, -transform.right, detectionRadius, obstacleLayer))
                return -transform.right * 2f; // Sola kaç
            
            return -forward; // Geri dön
        }
        
        // Debug.DrawRay(origin, forward * detectionRadius, Color.green);
        return Vector3.zero;
    }
}
