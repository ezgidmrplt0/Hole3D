using UnityEngine;
using DG.Tweening;

// SkillType enum is defined in SkillManager.cs

public class SkillPickup : MonoBehaviour
{
    [Header("Skill Settings")]
    public SkillType skillType;
    
    [Header("Visuals")]
    public Color magnetColor = Color.blue;
    public Color speedColor = Color.yellow;
    public Color shieldColor = Color.green;
    
    [Header("Lifetime")]
    public float lifetime = 45f; // Alınmazsa kaç saniye sonra kaybolsun
    
    [Header("Animation")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;
    public float rotateSpeed = 90f;
    
    private float spawnTime;
    private Vector3 startPos;
    private Renderer meshRenderer;
    private bool isBeingSwallowed = false; // Hole tarafından yutulmaya başlandı mı?
    
    void Start()
    {
        spawnTime = Time.time;
        
        // Zemine düşür - Raycast ile zemin bul
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            // Zeminin biraz üstüne yerleş
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.5f, transform.position.z);
        }
        
        startPos = transform.position;
        
        // Tag'ı ayarla (opsiyonel - component kontrolü ile de çalışıyor)
        try { gameObject.tag = "SkillPickup"; } catch { /* Tag yoksa sorun değil */ }
        
        // Rigidbody ekle (yoksa)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // Başlangıçta kinematic, hole yutunca açılacak
        rb.useGravity = false;
        
        // Collider ayarla
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            col = sphere;
        }
        col.isTrigger = true;
        
        // Renderer bul ve renk ayarla
        meshRenderer = GetComponentInChildren<Renderer>();
        ApplyColor();
        
        // Hole referansını bul
        holeTransform = FindObjectOfType<HoleMechanics>()?.transform;
        
        // Spawn animasyonu
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        
        Debug.Log($"[SkillPickup] {skillType} spawned at {transform.position}");
    }
    
    private Transform holeTransform;
    private bool isFalling = false;
    
    void Update()
    {
        // Eğer yutulmaya başlandıysa animasyon yapma
        if (isBeingSwallowed) return;
        
        // --- HOLE MESAFE KONTROLÜ ---
        if (holeTransform != null)
        {
            // 2D mesafe (X-Z düzleminde)
            float distXZ = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(holeTransform.position.x, holeTransform.position.z)
            );
            
            // Hole'un scale'ine göre yutma mesafesi
            float eatRadius = holeTransform.localScale.x * 0.7f;
            
            if (distXZ < eatRadius && !isFalling)
            {
                // Düşmeye başla!
                isFalling = true;
                isBeingSwallowed = true;
                Debug.Log($"[SkillPickup] Hole'a düşmeye başladı: {skillType}");
                
                // Rigidbody'yi aktif et - fiziksel düşüş
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.drag = 0f;
                    
                    // Hole'un merkezine doğru hafif çekme kuvveti
                    Vector3 toHole = (holeTransform.position - transform.position).normalized;
                    rb.AddForce(toHole * 2f, ForceMode.VelocityChange);
                }
                
                // Collider'ı trigger olmaktan çıkar ki zemine çarpmasın
                Collider col = GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                
                // 2 saniye sonra skill aktif et ve yok et
                StartCoroutine(FallAndActivate());
            }
        }
        
        // Düşüyorsa floating yapma
        if (isFalling) return;
        
        // Floating animasyonu (Yukarı aşağı) - Zemine göre
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // Dönme animasyonu
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        
        // Lifetime kontrolü
        if (Time.time - spawnTime > lifetime)
        {
            // Kaybolma animasyonu
            transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
        }
        
        // Son 5 saniyede yanıp sönme
        if (Time.time - spawnTime > lifetime - 5f && meshRenderer != null)
        {
            float alpha = Mathf.PingPong(Time.time * 5f, 1f);
            Color c = meshRenderer.material.color;
            c.a = 0.3f + alpha * 0.7f;
            meshRenderer.material.color = c;
        }
    }
    
    System.Collections.IEnumerator FallAndActivate()
    {
        // Düşme süresi - yeterince aşağı inene kadar bekle
        float fallTime = 0f;
        float maxFallTime = 2f;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        
        while (fallTime < maxFallTime)
        {
            fallTime += Time.deltaTime;
            
            // Hole'un merkezine doğru çek
            if (holeTransform != null && rb != null)
            {
                Vector3 toCenter = holeTransform.position - transform.position;
                toCenter.y = 0; // Sadece X-Z düzleminde
                rb.AddForce(toCenter.normalized * 5f * Time.deltaTime, ForceMode.VelocityChange);
                
                // Döndür
                rb.AddTorque(Vector3.one * 10f * Time.deltaTime, ForceMode.VelocityChange);
            }
            
            // Yeterince aşağı düştüyse bitir
            if (transform.position.y < holeTransform.position.y - 3f)
            {
                break;
            }
            
            yield return null;
        }
        
        // Skill'i aktif et
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ActivateSkill(skillType);
            Debug.Log($"[SkillPickup] {skillType} skill aktif edildi!");
        }
        
        // Küçülerek yok ol
        transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
    }
    
    // HoleMechanics tarafından çağrılır - yutulmaya başlandığında animasyonu durdur
    public void OnSwallowStart()
    {
        isBeingSwallowed = true;
        
        // Rigidbody'yi aktif et
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
    
    void ApplyColor()
    {
        if (meshRenderer == null) return;
        
        Color targetColor = skillType switch
        {
            SkillType.Magnet => magnetColor,
            SkillType.Speed => speedColor,
            SkillType.Shield => shieldColor,
            _ => Color.white
        };
        
        meshRenderer.material.color = targetColor;
        
        // Emission da ayarla (Glow efekti için)
        if (meshRenderer.material.HasProperty("_EmissionColor"))
        {
            meshRenderer.material.EnableKeyword("_EMISSION");
            meshRenderer.material.SetColor("_EmissionColor", targetColor * 0.5f);
        }
    }
    
    // Editor'da rengi göster
    void OnDrawGizmos()
    {
        Color gizmoColor = skillType switch
        {
            SkillType.Magnet => Color.blue,
            SkillType.Speed => Color.yellow,
            SkillType.Shield => Color.green,
            _ => Color.white
        };
        
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
