using UnityEngine;

public class CharacterAI : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 5f;

    [Header("Obstacle Avoidance")]
    public float detectionRadius = 1.5f;
    public LayerMask obstacleLayer;
    [Tooltip("Layer to check for ground. If raycast down fails, it's an edge.")]
    public LayerMask groundLayer;

    protected Rigidbody rb;
    protected Vector3 moveDirection;
    protected Animator animator;

    protected virtual void Awake()
    {
        // 0. Setup Animator
        animator = GetComponent<Animator>();

        // 1. Remove old NavMeshAgent if present
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            Destroy(navAgent);
        }

        // 2. Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Sadece rotasyonu kilitle. Y ekseni serbest kalsın (deliğe düşebilsinler).
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.useGravity = true;

        // 3. Setup Collider
        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.center = new Vector3(0, 1f, 0);
            collider.radius = 0.5f;
        }
    }

    protected void Move(Vector3 targetDirection)
    {
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

        // Hareketi uygula
        Vector3 velocity = moveDirection * moveSpeed;
        
        // Y eksenini koru (rb.velocity.y)
        velocity.y = rb.velocity.y;
        rb.velocity = velocity;

        // Dönüş
        if (moveDirection.x != 0 || moveDirection.z != 0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDirection.x, 0, moveDirection.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Animasyon Güncelleme
        if (animator != null)
        {
            // Eğer hareket varsa (yön vektörü 0 değilse) Run true olsun
            bool isMoving = moveDirection.magnitude > 0.1f;
            animator.SetBool("Run", isMoving);
        }
    }

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
                Debug.DrawRay(lookAheadPos + Vector3.up, Vector3.down * 3f, Color.magenta); 
                return -forward * 2f; 
            }
        }

        // 2. DUVAR/ENGEL KONTROLÜ
        Ray ray = new Ray(origin, forward);
        if (Physics.Raycast(ray, detectionRadius, obstacleLayer))
        {
            Debug.DrawRay(origin, forward * detectionRadius, Color.red);
            
            // Sağa mı sola mı kaçalım?
            if (!Physics.Raycast(origin, transform.right, detectionRadius, obstacleLayer))
                return transform.right * 2f; // Sağa kaç
            else if (!Physics.Raycast(origin, -transform.right, detectionRadius, obstacleLayer))
                return -transform.right * 2f; // Sola kaç
            
            return -forward; // Geri dön
        }
        
        Debug.DrawRay(origin, forward * detectionRadius, Color.green);
        return Vector3.zero;
    }
}
