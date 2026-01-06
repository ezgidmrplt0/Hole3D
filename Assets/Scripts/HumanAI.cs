using UnityEngine;

public class HumanAI : CharacterAI
{
    [Header("Behavior")]
    public float wanderRadius = 10f;
    public float changeDirectionInterval = 3f;
    public string enemyTag = "Zombie";
    public float fearRadius = 5f;

    private Vector3 wanderTarget;
    private float timer;

    protected override void Awake()
    {
        base.Awake();
        PickNewWanderTarget();
    }

    void Update()
    {
        // 1. Zombi kontrolü (Kaçış)
        Transform enemy = GetClosestEnemy();
        if (enemy != null)
        {
            Vector3 fleeDirection = transform.position - enemy.position;
            Move(fleeDirection);
            return; // Kaçarken başka bir şey yapma
        }

        // 2. Rastgele Gezinme (Wander)
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            PickNewWanderTarget();
            timer = changeDirectionInterval;
        }

        Vector3 directionToTarget = wanderTarget - transform.position;
        // Hedefe çok yakınsa bekle veya yeni hedef seç
        if (directionToTarget.magnitude < 0.5f)
        {
            moveDirection = Vector3.zero;
        }
        else
        {
            Move(directionToTarget);
        }
    }

    private void PickNewWanderTarget()
    {
        Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
        randomDir += transform.position;
        randomDir.y = transform.position.y; // Yüksekliği koru
        wanderTarget = randomDir;
    }

    private Transform GetClosestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, fearRadius);
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(enemyTag))
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
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, fearRadius);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Eğer duvara/engele çarparsak hemen yeni yön seç
        // (Zemin ile çarpışmayı yoksaymak için layer kontrolü yapılabilir veya normaline bakılabilir)
        // Şimdilik basitçe: Normali yukarı (Y) değilse duvardır.
        
        foreach (ContactPoint contact in collision.contacts)
        {
            // Eğer yüzeyin normali yukarı bakmıyorsa (yani duvarsa)
            if (Vector3.Dot(contact.normal, Vector3.up) < 0.5f)
            {
                PickNewWanderTarget();
                break;
            }
        }
    }
}
