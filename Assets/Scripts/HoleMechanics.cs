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
    public float voidRadius = 1.0f; 
    public float sinkDepth = 3f;
    public float pullForce = 150f; // New strong pull force
    public float rotationSpeed = 360f; // Degrees per second

    IEnumerator PhysicsFall(GameObject victim)
    {
        // --- LEVEL KONTROLÜ ---
        if (victim.CompareTag("Zombie"))
        {
            ZombieAI zombieInfo = victim.GetComponent<ZombieAI>();
            if (zombieInfo != null && holeLevel < zombieInfo.level) yield break; 
        }

        Transform vTransform = victim.transform;
        
        // --- AŞAMA 1: KENAR KONTROLÜ (BEKLEME) ---
        // Obje deliğin merkezine (siyah kısmına) tam girene kadar bekle
        while (vTransform != null)
        {
            // Sadece X-Z düzleminde mesafe (Yükseklik önemsiz)
            float dist = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), 
                                          new Vector2(vTransform.position.x, vTransform.position.z));

            // Scale arttıkça radius artar. Biraz tolerans (0.8f) ekledik ki tam kenarda düşmesin, hafif içeri girince düşsün.
            float scaledVoidRadius = voidRadius * transform.localScale.x * 0.9f;

            if (dist < scaledVoidRadius)
            {
                break; // Düşüş Başlasın!
            }
            yield return null; 
        }

        if (vTransform == null) yield break;

        // --- AŞAMA 2: DÜŞÜŞ BAŞLIYOR ---
        
        // 1. AI ve Kontrolcüleri Kapat
        CharacterAI ai = victim.GetComponent<CharacterAI>();
        if (ai != null) ai.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.velocity = Vector3.zero; // Anlık durdur
            agent.Stop();
            agent.enabled = false;
        }

        Animator anim = victim.GetComponent<Animator>();
        if (anim != null) 
        {
            anim.enabled = false; // Ragdoll aktifleşsin veya donup düşsün.
            // Eğer "Falling" animasyonu varsa o oynatılabilir: anim.SetTrigger("Fall");
        }

        // 2. Fizik Motorunu Devreye Sok
        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb == null) rb = victim.AddComponent<Rigidbody>(); // Yoksa ekle

        rb.isKinematic = false;
        rb.useGravity = true; 
        rb.constraints = RigidbodyConstraints.None;
        rb.drag = 0f; // Hızlı düşsün
        rb.angularDrag = 0.5f;
        
        // Rastgele bir ilk dönüş hızı ver (Tumble)
        rb.angularVelocity = Random.insideUnitSphere * 10f; 

        // 3. Yerle Çarpışmayı Kes (ÖNEMLİ: Obje yerin içinden geçebilmeli)
        Collider[] victimCols = victim.GetComponentsInChildren<Collider>();
        Collider[] nearbyGrounds = Physics.OverlapSphere(vTransform.position, 10f, LayerMask.GetMask("Default", "Ground", "Environment")); 
        
        foreach (var envCol in nearbyGrounds)
        {
            // Kendisi veya Delik değilse çarpışmayı kapat
            if (envCol.transform.root != vTransform.root && !envCol.transform.IsChildOf(this.transform))
            {
                foreach (var vCol in victimCols) Physics.IgnoreCollision(vCol, envCol, true);
            }
        }

        // 4. KÜÇÜLME EFEKTİ (Girdap etkisi)
        // Düşerken küçülerek yok olsun
        vTransform.DOScale(Vector3.zero, 1.0f).SetEase(Ease.InBack);

        // --- AŞAMA 3: AKTİF ÇEKİM GÜCÜ (Physics Loop) ---
        float timer = 0f;
        
        // 3D Çukur için limitleri ayarla
        float bottomLimit = transform.position.y - sinkDepth;
        
        while (timer < 3f && vTransform != null) 
        {
            timer += Time.deltaTime;

            // Merkeze ve Aşağıya Doğru Çek
            Vector3 centerBottom = transform.position + Vector3.down * sinkDepth;
            Vector3 direction = (centerBottom - vTransform.position).normalized;
            
            // Güçlü Çekim
            rb.AddForce(direction * pullForce * Time.deltaTime * 60f, ForceMode.Acceleration);
            rb.AddTorque(Vector3.up * rotationSpeed * Time.deltaTime, ForceMode.Force);

            // Çukurun dibine yaklaştı mı?
            if (vTransform.position.y < bottomLimit + 0.5f) // Dibe yaklaştı
            {
                // Kullanıcının isteği: "0.1 salise sonra yok olsun"
                // Önce küçültelim ki "yok oluş" pop diye olmasın
                vTransform.DOScale(Vector3.zero, 0.1f);
                yield return new WaitForSeconds(0.1f);
                break; // Ve döngü biter -> Destroy çağrılır
            }
            yield return null;
        }

        // --- SONUÇ: YOK ET ve PUAN VER ---
        if (vTransform != null)
        {
            // Puanlama Mantığı
            ProcessEatenObject(victim);

            // Efekt (Varsa partikül vs eklenebilir)
            Destroy(victim);
        }
    }

    void ProcessEatenObject(GameObject victim)
    {
        if (victim.CompareTag("Zombie"))
        {
            currentXP++;
            if (LevelManager.Instance != null) LevelManager.Instance.OnZombieEaten();
        }
        else if (victim.CompareTag("Human"))
        {
            currentXP--; // Ceza
            if (currentXP < 0) currentXP = 0;
            // Debug.Log("Human Eaten! Penalty.");
        }

        if (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
        
        if (visuals != null && xpToNextLevel > 0)
        {
            visuals.UpdateLocalProgress((float)currentXP / xpToNextLevel);
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
    public float repellentRadius = 5f; // Increased from 3f
    public float repellentForce = 80f; // Increased from 20f

    void ApplyRepellentEffect()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, repellentRadius);
        foreach (var col in nearby)
        {
            if (col.CompareTag("Human"))
            {
                // 1. Disable AI so physics can work
                UnityEngine.AI.NavMeshAgent agent = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled) agent.enabled = false;

                CharacterAI charAI = col.GetComponent<CharacterAI>();
                if (charAI != null) charAI.enabled = false;

                // 2. Apply Strong Push Force
                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    targetRb.isKinematic = false;
                    
                    Vector3 direction = (col.transform.position - transform.position).normalized;
                    direction.y = 0; // Push horizontally
                    
                    // ForceMode.VelocityChange for instant punchiness
                    targetRb.AddForce(direction * repellentForce * Time.deltaTime, ForceMode.VelocityChange); // Increased force needs DeltaTime if using high values in Update, or remove DeltaTime for ForceMode.Force
                    
                    Debug.DrawLine(transform.position, col.transform.position, Color.magenta);
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
