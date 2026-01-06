using UnityEngine;

public class ZombieAI : CharacterAI
{
    [Header("Behavior")]
    public string preyTag = "Human";
    public float detectionRange = 15f;
    public float wanderDurationAfterCollision = 1.0f;

    private float wanderTimer = 0f;
    private Vector3 wanderDirection;

    void Update()
    {
        // Eğer çarpışma sonrası serseri modundaysak
        if (wanderTimer > 0)
        {
            wanderTimer -= Time.deltaTime;
            Move(wanderDirection);
            return;
        }

        Transform prey = GetClosestPrey();
        if (prey != null)
        {
            Vector3 chaseDirection = prey.position - transform.position;
            Move(chaseDirection);
        }
        else
        {
            // Hedef yoksa dur veya rastgele gez (şimdilik dur)
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
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
        // Duvara çarparsa kısa süre rastgele bir yöne git (sıkışmayı önle)
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) < 0.5f) // Duvarsa
            {
                // Duvarın normaline göre yansı veya rastgele dön
                wanderDirection = Vector3.Reflect(transform.forward, contact.normal);
                wanderTimer = wanderDurationAfterCollision;
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
