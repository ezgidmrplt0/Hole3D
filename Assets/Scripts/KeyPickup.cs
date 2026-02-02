using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    private CageController targetCage;

    public void Setup(CageController cage)
    {
        targetCage = cage;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug Log to understand what is touching the key
        // Debug.Log($"Key Triggered by: {other.name} (Tag: {other.tag})");

        // 1. Component Search (Preferred)
        HoleMechanics hole = other.GetComponentInParent<HoleMechanics>();
        
        // 2. Direct Component Search
        if (hole == null) hole = other.GetComponent<HoleMechanics>();
        
        // 3. Tag Search (Backup)
        if (hole == null)
        {
            if (other.CompareTag("Player") || other.CompareTag("Hole"))
            {
                // If the object is tagged Player but script not found on it, refer to the global instance
                hole = FindObjectOfType<HoleMechanics>();
            }
        }

        if (hole != null)
        {
            CollectKey();
        }
    }
    
    // Alternatif: HolePhysicsHandler (Death Zone) tetiklerse
    // Bu metod dışarıdan da çağrılabilir.
    public void CollectKey()
    {
        Debug.Log("Key Collected!");

        if (targetCage != null)
        {
            targetCage.OpenCage();
        }
        else
        {
            Debug.LogWarning("Key collected but no cage assigned!");
        }

        // Anahtarı yok et
        Destroy(gameObject);
        
        // Ses veya partikül efekti buraya eklenebilir
    }
}
