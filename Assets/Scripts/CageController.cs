using UnityEngine;
using System.Collections.Generic;
using DG.Tweening; // Animasyon için (opsiyonel ama şık durur)

public class CageController : MonoBehaviour
{
    // Zombilerin dışarı fırlamasını önlemek için collider referansı
    private Collider cageCollider;
    private List<ZombieAI> trappedZombies = new List<ZombieAI>();
    private bool isOpened = false;

    private void Awake()
    {
        cageCollider = GetComponent<Collider>();
        // Eğer kökte yoksa childlarda ara
        if (cageCollider == null) cageCollider = GetComponentInChildren<Collider>();
    }

    public Vector3 GetGroundCenter()
    {
        // Geriye uyumluluk için (Eğer spawn point bulunamazsa)
        return GetRandomSpawnPosition();
    }

    public Vector3 GetRandomSpawnPosition()
    {
        // 0. ÖNCELİK: "ZombieSpawnPoint" TAG'ine sahip child obje
        foreach(Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.CompareTag("ZombieSpawnPoint"))
            {
                 // Collider varsa onun üst yüzeyinden rastgele nokta seç
                 Collider spawnCol = child.GetComponent<Collider>();
                 if (spawnCol != null)
                 {
                    Bounds b = spawnCol.bounds;
                    return new Vector3(
                        Random.Range(b.min.x, b.max.x),
                        b.max.y, // Collider'ın EN ÜST yüzeyini al (Zemin olduğu varsayımıyla)
                        Random.Range(b.min.z, b.max.z)
                    );
                 }
                 
                 // Collider YOKSA: Noktasal Spawn
                 Vector2 rnd = Random.insideUnitCircle * 1.5f; 
                 return child.position + new Vector3(rnd.x, 0, rnd.y);
            }
        }
        
        // 1. ÖNCELİK: "SpawnPoint" İSİMLİ obje (Fallback)
        Transform manualSpawnPoint = transform.Find("SpawnPoint");
        if (manualSpawnPoint != null)
        {
             Collider spawnCol = manualSpawnPoint.GetComponent<Collider>();
             if (spawnCol != null)
             {
                Bounds b = spawnCol.bounds;
                return new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    b.min.y, 
                    Random.Range(b.min.z, b.max.z)
                );
             }
             
             // Collider yoksa hafif dağıt
             Vector2 rnd = Random.insideUnitCircle * 1.5f;
             return manualSpawnPoint.position + new Vector3(rnd.x, 0, rnd.y);
        }

        // 2. ÖNCELİK: İPTAL EDİLDİ (Trigger kontrolü kaldırıldı)
        // Artık sadece Tag veya 'SpawnPoint' ismiyle çalışıyoruz.
        // Bu sayede kafesin kendi diğer trigger alanları (varsa) spawn noktası sanılmayacak.

        // 3. FALLBACK
        return CalculateAutoCenter();
    }

    private Vector3 CalculateAutoCenter()
    {
        // Tüm çocuklardaki colliderları bul ve ortak bir merkez hesapla
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        
        if (allColliders.Length > 0)
        {
            Bounds combinedBounds = allColliders[0].bounds;
            for (int i = 1; i < allColliders.Length; i++)
            {
                combinedBounds.Encapsulate(allColliders[i].bounds);
            }
            
            Vector3 center = combinedBounds.center;
            return new Vector3(center.x, 0, center.z);
        }
        
        return new Vector3(transform.position.x, 0, transform.position.z);
    }
    
    public void Setup(List<ZombieAI> zombies)
    {
        trappedZombies = zombies;
        
        Vector3 patrolCenter = GetGroundCenter(); 
        
        // 1. ÖNCELİK: "ZombieSpawnPoint" TAG'li obje
        bool foundViaTag = false;
        foreach(Transform child in GetComponentsInChildren<Transform>())
        {
            if (child != transform && child.CompareTag("ZombieSpawnPoint"))
            {
                patrolCenter = child.position;
                foundViaTag = true;
                break;
            }
        }
        
        // 2. İSİM ile ara
        if (!foundViaTag)
        {
            Transform manualSpawnPoint = transform.Find("SpawnPoint");
            if (manualSpawnPoint != null) patrolCenter = manualSpawnPoint.position;
        }
        
        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                // 1. Zombinin gezme alanını kafes merkezi olarak ayarla
                Vector3 targetCenter = new Vector3(patrolCenter.x, zombie.transform.position.y, patrolCenter.z);
                zombie.SetTrapped(true, targetCenter);
                
                // COLLISIONS ENABLED AGAIN:
                // Zombilerin kafes duvarlarına çarpıp içeride kalmasını istiyoruz.
            }
        }
    }

    public void OpenCage()
    {
        if (isOpened) return;
        isOpened = true;
        
        Debug.Log("Cage Opened! Zombies released.");

        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                zombie.SetTrapped(false);
                
                // Opsiyonel: Çarpışmayı geri açmak istersen (ama gerek yok, çıksınlar)
                // Collider zCol = zombie.GetComponent<Collider>();
                // if (zCol != null && cageCollider != null) Physics.IgnoreCollision(cageCollider, zCol, false);
            }
        }
        
        // Kafesi yok et (Yukarı uçurarak)
        transform.DOMoveY(transform.position.y + 10f, 1.5f).SetEase(Ease.InBack).OnComplete(() => {
            Destroy(gameObject);
        });
    }
}
