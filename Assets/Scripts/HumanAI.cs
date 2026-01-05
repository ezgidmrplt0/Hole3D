using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class HumanAI : MonoBehaviour
{
    [Header("Settings")]
    public float movementSpeed = 5.0f;
    public string predatorTag = "Zombie";
    public float detectionRange = 10f;
    public float fleeDistance = 5f;
    
    private NavMeshAgent agent;
    private float nextCheck;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = movementSpeed;
        animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning("HumanAI: Animator bileseni bulunamadi!");
    }

    void Update()
    {
        // Hareket animasyonu kontrolu
        if (animator != null)
        {
            bool isRunning = agent.velocity.magnitude > 0.1f;
            animator.SetBool("IsRun", isRunning);
        }

        // Performans için her frame değil, belli aralıklarla kontrol et
        if (Time.time > nextCheck)
        {
            nextCheck = Time.time + 0.2f; 
            CheckPredators();
        }
    }

    void CheckPredators()
    {
        GameObject[] predators = GameObject.FindGameObjectsWithTag(predatorTag);
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        if (predators.Length == 0)
        {
             // Sürekli spam olmasın diye sadece ilk seferde veya debug için açılabilir
             // Debug.LogWarning($"HumanAI: '{predatorTag}' etiketli tehlike bulunamadi!");
             return;
        }

        foreach (GameObject p in predators)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = p.transform;
            }
        }

        if (closest != null && closestDist < detectionRange)
        {
            FleeFrom(closest.position);
        }
    }

    void FleeFrom(Vector3 predatorPos)
    {
        Vector3 runDir = transform.position - predatorPos;
        Vector3 newPos = transform.position + runDir.normalized * fleeDistance;

        NavMeshHit hit;
        // NavMesh üzerinde geçerli bir nokta bul
        if (NavMesh.SamplePosition(newPos, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}
