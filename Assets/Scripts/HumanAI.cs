using UnityEngine;

public class HumanAI : CharacterAI
{
    [Header("Behavior")]
    public float wanderRadius = 30f;
    public float changeDirectionInterval = 3f;
    public string enemyTag = "Zombie";
    public float fearRadius = 5f;

    // Çarpışma tepkisi için değişkenler
    private float bounceTimer;
    private Vector3 bounceDirection;

    private Vector3 wanderTarget;
    private float timer;

    protected override void Awake()
    {
        base.Awake();
        PickNewWanderTarget();
    }

    void Update()
    {
        // 0. Çarpışma Sonrası Sekme (En yüksek öncelik)
        if (bounceTimer > 0)
        {
            bounceTimer -= Time.deltaTime;
            Move(bounceDirection, false); // Çarpınca YÜRÜYEREK uzaklaş (Panik yapma)
            return;
        }

        // 1. Zombi kontrolü (Kaçış)
        // 1. Zombi kontrolü (Kaçış)
        Transform enemy = GetClosestEnemy();
        if (enemy != null)
        {
            Vector3 fleeDirection = transform.position - enemy.position;
            Move(fleeDirection, true); // Koşarak kaç
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
        if (directionToTarget.magnitude < 0.5f)
        {
            moveDirection = Vector3.zero;
            // Durunca animasyonlar otomatik kapanır (CharacterAI içinde)
        }
        else
        {
            Move(directionToTarget, false); // Yürüyerek gez
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
        // Herhangi bir engele çarparsak (Zemin hariç)
        foreach (ContactPoint contact in collision.contacts)
        {
            // Normalin Y değeri düşükse (Duvar/Kutu/Ağaç vs.)
            if (Mathf.Abs(contact.normal.y) < 0.6f) 
            {
                // 1. "Reflect" (Yansıma) kullanarak daha yumuşak bir dönüş sağla
                // Sadece geri tepmek (contact.normal) yerine, açılı şekilde sek.
                Vector3 reflection = Vector3.Reflect(transform.forward, contact.normal);
                
                bounceDirection = reflection.normalized;
                bounceTimer = 0.5f; // 0.5 saniye boyunca bu yeni yöne git

                // 2. Wander hedefini de bu yeni yöne taşı
                // Böylece bounce bittiğinde tekrar duvara dönmezler.
                wanderTarget = transform.position + bounceDirection * wanderRadius;
                timer = changeDirectionInterval; // Yeni hedefte bir süre kararlı kalsın

                break;
            }
        }
    }
}
