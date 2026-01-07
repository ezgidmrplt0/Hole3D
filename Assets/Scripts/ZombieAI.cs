using UnityEngine;
using TMPro;

public class ZombieAI : CharacterAI
{
    [Header("Level Info")]
    public int level = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 10; // 10 tane yediğinde level atlasın
    public TMP_Text levelText;

    [Header("Behavior")]
    public string preyTag = "Human";
    public float detectionRange = 8f;
    public float runTriggerDistance = 8f; // Bu mesafeden yakındaysa KOŞ, uzaksa YÜRÜ
    public float wanderDurationAfterCollision = 1.0f;

    private float wanderTimer = 0f;
    private Vector3 wanderDirection;

    [Header("Roaming")]
    public float wanderRadius = 5f;
    public float roamInterval = 4f;
    private Vector3 roamTarget;
    private float roamTimer;

    void Start()
    {
        // Zombi hızını biraz düşürelim
        runSpeed = 2.5f; 
        walkSpeed = 1.5f;

        // Level Textini Ayarla
        if (levelText != null) 
        {
            levelText.text = level.ToString(); 
            // Text her zaman kameraya baksın istiyorsan Update'e kod ekleyebiliriz ama şimdilik sabit
        }
        
        PickNewRoamTarget();
    }

    void Update()
    {
        // Eğer çarpışma sonrası serseri modundaysak
        if (wanderTimer > 0)
        {
            wanderTimer -= Time.deltaTime;
            Move(wanderDirection, false); // Çarpınca YÜRÜYEREK uzaklaş (Sakince)
            return;
        }

        Transform prey = GetClosestPrey();
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
                 // rb.velocity = ... zaten Move çağırmazsak durur mu? Hayır CharacterAI Move çağırmazsak eski velocity kalabilir.
                 // CharacterAI yapısı her frame çağrı bekliyor mu? Evet velocity set ediyor.
                 // Eğer çağırmazsak velocity sıfırlanmaz, momentum kalabilir.
                 // En temizi:
                 Move(Vector3.zero, false);
            }
            else
            {
                Move(dir, false); // Boşta gezerken YÜRÜ
            }
        }
    }

    private void PickNewRoamTarget()
    {
        // Önce sahnede insan var mı diye bak (Koku alma duyusu gibi global arama)
        GameObject[] humans = GameObject.FindGameObjectsWithTag(preyTag);

        if (humans.Length > 0)
        {
            // Rastgele bir insan seç ve ona doğru git
            Transform targetHuman = humans[Random.Range(0, humans.Length)].transform;
            
            // Tam üstüne gitmesin, o bölgeye gitsin (Sürü psikolojisi)
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            Vector3 targetPos = targetHuman.position + randomOffset;
            
            targetPos.y = transform.position.y;
            roamTarget = targetPos;
        }
        else
        {
            // Hiç insan kalmadıysa olduğu yerde gezinmeye devam et
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir += transform.position;
            randomDir.y = transform.position.y;
            roamTarget = randomDir;
        }
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

            // XP Kazan
            currentXP++;
            
            // Level Atla (Eşik değeri geçilirse)
            if (currentXP >= xpToNextLevel)
            {
                level++;
                currentXP = 0; // Sıfırla veya devret
                // xpToNextLevel += 5; // İstersen zorluğu artırabilirsin

                if (levelText != null) levelText.text = level.ToString();
                
                // Sadece Level Atladığında Büyüsün (Opsiyonel, yoksa her yediğinde mi büyüsün?)
                // Kullanıcı "her yediğinde büyüsün" demedi, "10 insan yediklerinde level atlasınlar" dedi.
                // Biz yine de her yediğinde hafif büyütelim ama level sadece 10'da bir artsın.
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
