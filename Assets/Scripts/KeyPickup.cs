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
        // Hole'un physics handler'ı veya kendisi "Hole" veya "Player" olabilir.
        // Genelde Hole Physics Trigger'ı ile etkileşime girer.
        // Hole Physics Handler'da "Ignore Collision" var ama Trigger çalışır.
        
        // HoleMechanics'in olduğu obje veya physics collider'ı
        HoleMechanics hole = other.GetComponentInParent<HoleMechanics>();
        
        // Eğer parentta bulamazsa direkt çarpana bak (Hole kendisi trigger olabilir)
        if (hole == null) hole = other.GetComponent<HoleMechanics>();

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
