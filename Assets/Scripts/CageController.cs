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
        
        // USER REQUEST: Beyaz küplerin collider ayarlarını otomatik yap
        ConfigureCageWalls();
    }

    private void ConfigureCageWalls()
    {
        // Tüm görsel parçaları (duvarları) bul
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            GameObject child = r.gameObject;
            
            // Eğer bu bir efekt veya partikül değilse (Genel Mesh kontrolü)
            if (child.GetComponent<ParticleSystem>() != null) continue;

            Collider col = child.GetComponent<Collider>();
            if (col == null)
            {
                // Mesh'e uygun collider ekle
                col = child.AddComponent<BoxCollider>();
            }
            
            // FIX: Duvarları kalınlaştır (Zombilerin içinden geçmesini önle)
            if (col is BoxCollider box)
            {
                var size = box.size;
                // REVİZE: 1.0f çok kalın oldu, zombiler üstüne tırmanıyor.
                // Sadece çok ince (0'a yakın) olanları 0.1f yapalım yeter.
                size.x = Mathf.Max(size.x, 0.1f);
                size.z = Mathf.Max(size.z, 0.1f);
                box.size = size;
            }
            
            // Trigger KAPALI olsun ki zombiler içinden geçemesin (Fiziksel Engel)
            col.isTrigger = false; 
            
            // Tag ayarı: Duvar olarak işaretle (Opsiyonel, engel tespiti için)
            // Eğer "Untagged" ise "Untagged" kalsın ama layer önemli.
            // Layer'ı "Default" yapalım ki herkes çarpsın.
            child.layer = LayerMask.NameToLayer("Default");
        }
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
                 // Collider YOKSA: Noktasal Spawn
                 // FIX: 1.5f çok genişti, dışarı taşıyorlardı.
                 // 0.4f yaparak merkeze topluyoruz. Fizik motoru onları hafifçe iterek yer açacaktır.
                 Vector2 rnd = Random.insideUnitCircle * 0.4f; 
                 return child.position + new Vector3(rnd.x, 0, rnd.y);
            }
        }
        
        // 1. ÖNCELİK: "SpawnPoint" İSİMLİ obje (Fallback)
        // Eğer çocuklarda bulamadıysak GLOBAL ara (User hiyerarşi dışına koymuş olabilir)
        GameObject globalSpawnPoint = GameObject.FindGameObjectWithTag("ZombieSpawnPoint");
        if (globalSpawnPoint != null)
        {
             // Eğer global bulduysak onu kullan (Mesafeye bakmaksızın, kullanıcı bunu istemiş)
             // Ancak sadece "Cage" e yakınsa mı? Hayır, kullanıcı açıkça tag ekledim dedi.
             Debug.Log($"[CageController] Found global 'ZombieSpawnPoint': {globalSpawnPoint.name}");
             return globalSpawnPoint.transform.position;
        }

        Transform manualSpawnPoint = null;
        // FIX: Inactive (gizli) objeleri de aramak için true gönderiyoruz
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t != this.transform && t.name == "SpawnPoint")
            {
                manualSpawnPoint = t;
                break;
            }
        }
        
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
        Debug.LogWarning($"[CageController] Could not find 'ZombieSpawnPoint' tag or 'SpawnPoint' name in {gameObject.name}. Using Collider Center.");
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
            // GLOBAL ARA (User dışarıya koymuş olabilir)
            GameObject globalSpawnPoint = GameObject.FindGameObjectWithTag("ZombieSpawnPoint");
            if (globalSpawnPoint != null)
            {
                patrolCenter = globalSpawnPoint.transform.position;
                foundViaTag = true;
            }
            else
            {
                // FIX: Recursive search for "SpawnPoint" (Include Hidden)
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t != this.transform && t.name == "SpawnPoint")
                    {
                        patrolCenter = t.position;
                        break;
                    }
                }
            }
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
        
        // FIX: Zombiler birbirini itip titremesin diye kendi aralarında çarpışmayı kapatıyoruz.
        // Böylece "kalabalık" görünüp birbirlerinin içinden hafifçe geçebilirler ama duvara çarpınca dururlar.
        for (int i = 0; i < trappedZombies.Count; i++)
        {
            for (int j = i + 1; j < trappedZombies.Count; j++)
            {
                if (trappedZombies[i] != null && trappedZombies[j] != null)
                {
                    Collider c1 = trappedZombies[i].GetComponent<Collider>();
                    Collider c2 = trappedZombies[j].GetComponent<Collider>();
                    
                    if (c1 != null && c2 != null)
                    {
                        Physics.IgnoreCollision(c1, c2, true);
                    }
                }
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 center = GetGroundCenter();
        Gizmos.DrawSphere(center, 0.3f);
        Gizmos.DrawWireSphere(center, 1f);
    }
}
