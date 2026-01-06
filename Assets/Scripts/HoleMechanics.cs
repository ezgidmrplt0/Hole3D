using UnityEngine;
using System.Collections;
using TMPro;

public class HoleMechanics : MonoBehaviour
{
    // Listeye çevirdik ki hem Zombi hem İnsan girebilsin
    public System.Collections.Generic.List<string> targetTags = new System.Collections.Generic.List<string> { "Zombie", "Human" };

    [Header("UI")]
    public TMP_Text levelText;

    [Header("Animation Settings")]
    public float fallDuration = 0.5f;
    public float sinkDepth = 2f;
    public float minScale = 0.1f;

    // Eski ayarlar temizlendi

    [Header("Level System")]
    public int holeLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 5; // İlk başta 5 tane yiyince büyüsün

    // Görselleri bul (Public yaptık ki editörden de sürükleyebilesin)
    public HoleVisuals visuals;
    
    private Collider[] holeCols;

    private void Start()
    {
        // Deliğin kendi colliderlarını (Siyah kısım, çerçeve vb.) hafızaya al
        holeCols = GetComponentsInChildren<Collider>();

        // Görselleri bulmaya çalış (Otomatik - Daha Güçlü Arama)
        if (visuals == null)
        {
            // 1. Önce çocuklara bak
            visuals = GetComponentInChildren<HoleVisuals>();
            
            // 2. Kendine bak
            if (visuals == null) visuals = GetComponent<HoleVisuals>();
            
            // 3. Sahneye bak (Tek kişilik oyun olduğu için güvenli)
            if (visuals == null) visuals = FindObjectOfType<HoleVisuals>();
        }

        if (visuals == null)
        {
            Debug.LogError("HoleMechanics: HoleVisuals scripti bulunamadı! Lütfen Inspectordan 'visuals' kutucuğuna atama yapın.");
        }
        else
        {
            // Bulduysak barı başlangıç durumuna (0) getir veya mevcut XP durumunu yansıt
            visuals.UpdateLocalProgress((float)currentXP / xpToNextLevel);
        }

        // Texti güncelle
        UpdateLevelText();
    }

    private void UpdateLevelText()
    {
        if (levelText != null) levelText.text = "Lvl " + holeLevel;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Listede var mı kontrol et
        if (targetTags.Contains(other.tag))
        {
            StartCoroutine(PhysicsFall(other.gameObject));
        }
    }

    [Header("Physics Settings")]
    public float suctionForce = 80f; // Daha güçlü çekelim ki sürüklensin
    public Collider fallZoneCollider; // ARTIK BU ALANA GİRİNCE DÜŞECEK!

    IEnumerator PhysicsFall(GameObject victim)
    {
        // --- LEVEL KONTROLÜ ---
        if (victim.CompareTag("Zombie"))
        {
            ZombieAI zombieInfo = victim.GetComponent<ZombieAI>();
            if (zombieInfo != null && holeLevel < zombieInfo.level) yield break; 
        }

        // --- AŞAMA 1: YAKALAMA & SÜRÜKLEME (DRAG) ---
        CharacterAI ai = victim.GetComponent<CharacterAI>();
        if (ai != null) ai.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        Animator anim = victim.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true; 
            rb.constraints = RigidbodyConstraints.FreezeRotation; 
            rb.drag = 2f; 
        }

        // Merkeze Gelene Kadar Çek (FALL ZONE Kontrolü)
        while (victim != null)
        {
            // Belirlenen alanın (Siyahlık) içine girdi mi?
            // Bounds.Contains 3D bakar, bu yüzden sadece X ve Z'ye bakmak daha güvenli olabilir
            // Ama collider yeterince yüksekse sorun olmaz.
            // Daha hassas kontrol: Sadece XZ düzleminde mesafe
            
            bool isInside = false;
            
            if (fallZoneCollider != null)
            {
                // Collider'ın sınırlarına girdiyse
                // Not: Bounds kutu gibidir, yuvarlak delik için SphereCollider kullanıp distance bakmak daha iyi
                // Kullanıcının "Area" dediği şeyi tam karşılamak için ClosestPoint kullanıyoruz
                
                // Basit Yöntem: Bounds Contains (Kutu)
                // isInside = fallZoneCollider.bounds.Contains(victim.transform.position);

                // Gelişmiş Yöntem: Merkezden Uzaklık < Collider Yarıçapı (Eğer yuvarlaksa)
                // Bounds extents x'i yarıçap gibi düşünelim
                float radius = fallZoneCollider.bounds.extents.x;
                float dist = Vector3.Distance(transform.position, victim.transform.position);
                
                // %80 içe girince düşsün (Kenardan taşmayı önlemek için)
                if (dist < radius * 0.9f) isInside = true;
            }
            else
            {
                 // Eğer atama yapılmadıysa varsayılan 1 birim
                 if (Vector3.Distance(transform.position, victim.transform.position) < 1f) isInside = true;
            }

            if (isInside)
            {
                break; // AŞAMA 2'ye (DÜŞÜŞ) geç!
            }

            // Merkeze Çek
            if (rb != null)
            {
                Vector3 directionToHole = (transform.position - victim.transform.position);
                directionToHole.y = 0; 
                rb.AddForce(directionToHole.normalized * suctionForce * Time.deltaTime, ForceMode.VelocityChange);
            }
            
            yield return null;
        }

        if (victim == null) yield break;

        // --- AŞAMA 2: DÜŞÜŞ (FALL) ---
        // Artık deliğin "boşluk" trigger'ının içinde!

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None; // Artık yuvarlansın (Tumble)
            rb.drag = 0.5f; // Hızlı düşsün
        }

        // ÇARPIŞMA İPTALİ (ZEMİNİ DELME)
        // Dikkat: Sadece ZEMİNİ (veya etraftaki objeleri) ignore etmeliyiz.
        // Deliğin kendi çerçevesini (Rim) ignore ETMEMELİYİZ ki ona çarpıp sekelbilsinler.
        
        Collider[] victimCols = victim.GetComponentsInChildren<Collider>();
        Collider[] nearbyCols = Physics.OverlapSphere(victim.transform.position, 3f);
        
        foreach (var envCol in nearbyCols)
        {
            // 1. Triggerlar ile işimiz yok
            if (envCol.isTrigger) continue;
            
            // 2. Kurbanın kendisi ise geç
            if (envCol.transform.root == victim.transform) continue;

            // 3. ÖNEMLİ: Bu obje Deliğin bir parçası mı? (Rim, Çerçeve vb.)
            // Eğer deliğin parçasıysa IGNORE ETME! Çarpsın.
            if (envCol.transform.IsChildOf(this.transform)) continue;

            // Kalanlar muhtemelen Zemindir veya diğer engellerdir -> IGNORE ET
            foreach (var vCol in victimCols) Physics.IgnoreCollision(vCol, envCol, true);
        }

        // Düşüş Simülasyonu
        float timer = 0f;
        while (timer < 2f) 
        {
            timer += Time.deltaTime;

            if (rb != null)
            {
                // Artık çekmeye gerek yok, yerçekimi halletsin
                // Ama sağa sola takılmasın diye hafifçe merkezin "X,Z"sine itebiliriz
                // rb.AddForce(Vector3.down * 20f * Time.deltaTime, ForceMode.VelocityChange);
                
                // Ekstra yerçekimi
                rb.AddForce(Physics.gravity * 2f, ForceMode.Acceleration);
            }

            // Yeterince düştü mü?
            if (victim.transform.position.y < transform.position.y - sinkDepth)
            {
                break; 
            }

            yield return null;
        }

        // Yok Etme
        if (victim != null)
        {
            // --- LEVEL UP & XP SİSTEMİ ---
            
            if (victim.CompareTag("Zombie"))
            {
                // Zombi yedik: Ödül!
                currentXP++;
                if (LevelManager.Instance != null) LevelManager.Instance.OnZombieEaten();
            }
            else if (victim.CompareTag("Human"))
            {
                // İnsan yedik: Ceza!
                // Yanlışlıkla insan yersen barın gerilesin.
                currentXP--;
                if (currentXP < 0) currentXP = 0; // 0'ın altına düşmesin
                
                // İstersen kırmızı yanıp sönme efekti ekle
                Debug.Log("Human Eaten! XP Penalty.");
            }

            // Level Atla
            if (currentXP >= xpToNextLevel)
            {
                LevelUp();
            }
            
            // UI Güncelle (HoleVisuals üzerinden)
            // progress = (float)currentXP / xpToNextLevel;
            // Bunu yapabilmek için HoleVisuals'a erişim lazım veya Event fırlatmalı.
            // En temizi LevelManager üzerinden event göndermek ama şimdilik doğrudan Image referansı alabiliriz.
            if (visuals != null)
            {
                visuals.UpdateLocalProgress((float)currentXP / xpToNextLevel);
            }

            Destroy(victim);
        }
    }

    void LevelUp()
    {
        holeLevel++;
        currentXP = 0; 
        xpToNextLevel = (int)(xpToNextLevel * 1.5f); 

        // Büyüme Efekti
        transform.localScale *= 1.2f; 
        // fallRadius kullanılmıyor, çünkü transform scale edilince collider da büyüyor
        
        UpdateLevelText();

        Debug.Log($"HOLE LEVEL UP! New Level: {holeLevel} | Scale: {transform.localScale.x}");
    }

    private void OnDrawGizmosSelected()
    {
        if (fallZoneCollider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(fallZoneCollider.bounds.center, fallZoneCollider.bounds.size);
        }
    }
}
