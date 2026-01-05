using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Settings")]
    public float movementSpeed = 3.5f;
    public string targetTag = "Human";
    public float updateRate = 0.5f; // Saniyede kaç kez hedef arasın

    private NavMeshAgent agent;
    private Transform currentTarget;
    private float nextCheck;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = movementSpeed;
        animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning("ZombieAI: Animator bileseni bulunamadi!");
    }

    void Update()
    {
        // Hareket animasyonu kontrolu
        if (animator != null)
        {
            // Hiz 0.1'den buyukse kosuyor demektir
            bool isRunning = agent.velocity.magnitude > 0.1f;
            animator.SetBool("IsRun", isRunning);
        }

        if (Time.time > nextCheck)
        {
            nextCheck = Time.time + updateRate;
            FindNearestTarget();
        }

        if (currentTarget != null)
        {
            agent.SetDestination(currentTarget.position);
        }
    }

    void FindNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        if (targets.Length == 0)
        {
            Debug.LogWarning($"ZombieAI: '{targetTag}' etiketli hedef bulunamadi! Sahnedeki Human objelerinin etiketini kontrol edin.");
            return;
        }

        foreach (GameObject t in targets)
        {
            float d = Vector3.Distance(transform.position, t.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = t.transform;
            }
        }

        currentTarget = closest;
        if (currentTarget == null)
            Debug.LogWarning("ZombieAI: Hedef null! (Beklenmedik durum)");
    }
}
