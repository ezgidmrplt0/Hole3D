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
    private Transform currentChaser;

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

        // 1. Zombi kontrolü (Kaçış - Hysteresis ile)
        // Önce kritik mesafedeki (fearRadius) en yakın düşmanı bul
        Transform immediateEnemy = GetClosestEnemy();
        
        if (immediateEnemy != null)
        {
            // Yeni bir tehdit var, onu hedefle
            currentChaser = immediateEnemy;
        }
        else if (currentChaser != null)
        {
            // Kritik mesafede kimse yok, ama eski kovalayanı kontrol et (Buffer Zone)
            float dist = Vector3.Distance(transform.position, currentChaser.position);
            
            // Eğer zombi çok uzaklaştıysa veya öldüyse takibi bırak
            if (dist > fearRadius * 1.5f || !currentChaser.gameObject.activeInHierarchy)
            {
                currentChaser = null;
            }
        }

        // Eğer hala birinden kaçıyorsak
        if (currentChaser != null)
        {
            Vector3 fleeDirection = transform.position - currentChaser.position;
            Move(fleeDirection, true); // Koşarak kaç
            return; // Kaçarken başka bir şey yapma
        }
        
        // Eğer kimse yoksa currentChaser'ı temizle (Garanti olsun)
        if (immediateEnemy == null && currentChaser == null)
        {
             // Wander kısmına geçecek
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
        Vector3 candidatePos = transform.position + Random.insideUnitSphere * wanderRadius;
        candidatePos.y = expectedGroundY; // Yüksekliği koru (Ground Y Reference)

        if (IsOnGround(candidatePos))
        {
            wanderTarget = candidatePos;
        }
        else
        {
             // Geçersizse merkeze doğru güvenli bir nokta seç
             wanderTarget = Vector3.Lerp(transform.position, Vector3.zero, 0.5f);
        }
    }

    private bool IsOnGround(Vector3 pos)
    {
        if (groundLayer.value == 0) return true; 
        return Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, 50f, groundLayer);
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
