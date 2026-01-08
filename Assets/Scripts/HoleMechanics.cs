using UnityEngine;
using System.Collections;
using TMPro;
using DG.Tweening; // Import DOTween

public class HoleMechanics : MonoBehaviour
{
    // Listeye çevirdik ki hem Zombi hem İnsan girebilsin
    public System.Collections.Generic.List<string> targetTags = new System.Collections.Generic.List<string> { "Zombie", "Human" };

    [Header("UI")]
    public TMP_Text levelText;

    [Header("Animation Settings")]
    public float fallDuration = 0.5f;
    // public float sinkDepth = 2f; // Removed duplicate
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
    public float voidRadius = 1.0f; // Siyah alanın yarıçapı (Scale ile çarpılacak)
    public float sinkDepth = 3f;
    // public float suctionForce; // Kaldırıldı

    IEnumerator PhysicsFall(GameObject victim)
    {
        // --- LEVEL KONTROLÜ ---
        if (victim.CompareTag("Zombie"))
        {
            ZombieAI zombieInfo = victim.GetComponent<ZombieAI>();
            if (zombieInfo != null && holeLevel < zombieInfo.level) yield break; 
        }

        // --- AŞAMA 1: BEKLEME (WAIT FOR VOID) ---
        // Karakterin AI'sını kapatmıyoruz, yürümeye devam etsin.
        // Sadece fiziksel olarak "Boşluğa" (Siyah Alana) girdi mi diye kontrol ediyoruz.

        Transform vTransform = victim.transform;
        
        while (vTransform != null)
        {
            // Mesafeyi ölç (Sadece X-Z düzleminde)
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                          new Vector3(vTransform.position.x, 0, vTransform.position.z));

            // Scale arttıkça radius da artmalı
            float scaledVoidRadius = voidRadius * transform.localScale.x;

            if (dist < scaledVoidRadius)
            {
                break; // DÖNGÜYÜ KIR -> AŞAMA 2'ye (DÜŞÜŞ) GEÇ
            }

            yield return null; 
        }

        if (vTransform == null) yield break;

        // --- AŞAMA 2: DÜŞÜŞ (FALL) ---
        // Artık geri dönüş yok. Kontrolü al ve düşür.

        // 1. AI ve Kontrolcüleri Kapat
        CharacterAI ai = victim.GetComponent<CharacterAI>();
        if (ai != null) ai.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        Animator anim = victim.GetComponent<Animator>();
        if (anim != null) anim.enabled = false; // Ragdoll etkisi için animasyon durmalı

        // 2. Fizik Sistemini Aç
        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true; 
            rb.constraints = RigidbodyConstraints.None; // Dönebilsin
            rb.drag = 0.5f;
            rb.angularVelocity = Random.insideUnitSphere * 5f; // Hafif spin
        }

        // 3. Yerle Çarpışmayı Kes (Sadece ZEMİN ile!)
        // Böylece deliğin içine düşebilir ama kenarlarına (Rim) çarpabilir.
        Collider[] victimCols = victim.GetComponentsInChildren<Collider>();
        
        // "Ground" veya "Default" layer'ındaki yakındaki objeleri bul
        // En güvenlisi basit bir OverlapSphere
        Collider[] nearbyGrounds = Physics.OverlapSphere(vTransform.position, 5f); 
        
        foreach (var envCol in nearbyGrounds)
        {
            // Kendisi değilse ve Deliğin parçası değilse -> Ignore
            if (envCol.transform.root != vTransform.root && !envCol.transform.IsChildOf(this.transform))
            {
                foreach (var vCol in victimCols) Physics.IgnoreCollision(vCol, envCol, true);
            }
        }

        // --- AŞAMA 3: DÜŞÜŞ SİMÜLASYONU & YOK ETME ---
        float timer = 0f;
        while (timer < 3f && vTransform != null) 
        {
            timer += Time.deltaTime;

            if (vTransform.position.y < transform.position.y - sinkDepth)
            {
                break; // Yeterince düştü
            }
            yield return null;
        }

        // Yok Et ve Puan Ver
        if (vTransform != null)
        {
            if (victim.CompareTag("Zombie"))
            {
                currentXP++;
                if (LevelManager.Instance != null) LevelManager.Instance.OnZombieEaten();
            }
            else if (victim.CompareTag("Human"))
            {
                currentXP--;
                if (currentXP < 0) currentXP = 0;
                Debug.Log("Human Eaten! XP Penalty.");
            }

            if (currentXP >= xpToNextLevel)
            {
                LevelUp();
            }
            
            if (visuals != null && xpToNextLevel > 0)
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

        // Büyüme Oranı
        float growthFactor = 1.2f;
        float duration = 0.5f;

        // 1. Fiziği/Kendini Büyüt (Animasyonlu)
        Vector3 targetScale = transform.localScale * growthFactor;
        transform.DOScale(targetScale, duration).SetEase(Ease.OutElastic);
        
        // 2. Görseli Büyüt (Eğer ayrı bir objedeyse)
        if (visuals != null && visuals.transform.parent != transform)
        {
             Vector3 visualTargetScale = visuals.transform.localScale * growthFactor;
             visuals.transform.DOScale(visualTargetScale, duration).SetEase(Ease.OutElastic);
        }
        
        // fallRadius kullanılmıyor, çünkü transform scale edilince collider da büyüyor
        
        UpdateLevelText();

        Debug.Log($"HOLE LEVEL UP! New Level: {holeLevel} | Target Scale: {targetScale.x}");
    }

    private void Update()
    {
        // --- MAGNET LOGIC ---
        if (SkillManager.Instance != null)
        {
            if (SkillManager.Instance.IsMagnetUnlocked)
            {
                ApplyMagnetEffect();
            }

            if (SkillManager.Instance.IsRepellentUnlocked)
            {
                ApplyRepellentEffect();
            }
        }
    }

    [Header("Magnet Settings")]
    public float magnetRadius = 8f; // Increased default
    public float magnetForce = 50f; // Significantly increased

    [Header("Repellent Settings")]
    public float repellentRadius = 3f;
    public float repellentForce = 20f;

    void ApplyRepellentEffect()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, repellentRadius);
        foreach (var col in nearby)
        {
            if (col.CompareTag("Human"))
            {
                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    Vector3 direction = (col.transform.position - transform.position).normalized;
                    direction.y = 0; // Push horizontally
                    
                    // Push AWAY
                    targetRb.AddForce(direction * repellentForce * Time.deltaTime, ForceMode.VelocityChange);
                }
            }
        }
    }

    void ApplyMagnetEffect()
    {
        // 1. Find targets nearby
        Collider[] nearby = Physics.OverlapSphere(transform.position, magnetRadius);
        bool foundAny = false;

        foreach (var col in nearby)
        {
            // ONLY PULL ZOMBIES (Ignore Humans)
            if (col.CompareTag("Zombie"))
            {
                foundAny = true;
                
                // 2. Disable NavMeshAgent so Physics can take over
                UnityEngine.AI.NavMeshAgent agent = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled)
                {
                    agent.enabled = false;
                }

                CharacterAI charAI = col.GetComponent<CharacterAI>();
                if (charAI != null) charAI.enabled = false; // Stop AI movement logic

                // 3. Apply Force with Heavy Damping (To stop orbiting)
                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    targetRb.isKinematic = false; 

                    // -- Physics Tweak --
                    // 1. Apply high drag so they don't overshoot (Orbiting issue)
                    targetRb.drag = 5f; 
                    targetRb.angularDrag = 5f;

                    // 2. Kill lateral velocity (Optional, but helps center them)
                    // Allows them to slide IN, but stops them from sliding PAST
                    // We can just rely on high drag for now, but let's ensure force is consistent.

                    Vector3 direction = (transform.position - col.transform.position).normalized;
                    direction.y = 0; // Keep pull horizontal, gravity handles falling

                    // Pull towards hole center
                    // ForceMode.Force for smooth continuous pull against the drag
                    targetRb.AddForce(direction * magnetForce, ForceMode.Force);
                    
                    // Draw debug line to confirm lock-on
                    Debug.DrawLine(transform.position, col.transform.position, Color.cyan);
                }
            }
        }
        
        // Debug Log only occasionally to avoid spam, or if testing
        // if (foundAny) Debug.Log("Magnet: Pulling targets...");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Void Radius
        Gizmos.color = Color.black;
        float scaledRadius = voidRadius * transform.localScale.x;
        Gizmos.DrawWireSphere(transform.position, scaledRadius);

        if (SkillManager.Instance != null && SkillManager.Instance.IsMagnetUnlocked)
        {
             Gizmos.color = Color.blue;
             Gizmos.DrawWireSphere(transform.position, magnetRadius);
        }
    }
}
