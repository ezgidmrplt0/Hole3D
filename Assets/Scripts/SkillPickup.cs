using UnityEngine;
using DG.Tweening;

// SkillType enum is defined in SkillManager.cs

/// <summary>
/// Basit Skill Pickup - Prefab olarak hazırla, model child olarak içinde olsun.
/// SpawnManager'da 3 ayrı prefab atanır: MagnetPickup, SpeedPickup, ShieldPickup
/// </summary>
public class SkillPickup : MonoBehaviour
{
    [Header("Skill Settings")]
    [Tooltip("Bu pickup hangi skill'i aktif edecek?")]
    public SkillType skillType;
    
    [Tooltip("Bu pickup alındığında skill kalıcı mı olacak?")]
    public bool isPermanentPickup = false;
    
    [Header("Lifetime")]
    [Tooltip("Alınmazsa kaç saniye sonra kaybolsun")]
    public float lifetime = 45f;
    
    [Header("Animation")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;
    public float rotateSpeed = 90f;
    
    // Private değişkenler
    private float spawnTime;
    private Vector3 startPos;
    private Renderer meshRenderer;
    private Transform holeTransform;
    private bool isBeingSwallowed = false;
    private bool isFalling = false;
    private Rigidbody rb;
    private Collider col;
    
    void Start()
    {
        spawnTime = Time.time;
        
        // Zemine düşür - Raycast ile zemin bul
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.5f, transform.position.z);
        }
        
        startPos = transform.position;
        
        // Tag ayarla
        try { gameObject.tag = "SkillPickup"; } catch { }
        
        // Rigidbody ayarla (varsa kullan, yoksa ekle)
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Collider ayarla (varsa kullan, yoksa ekle)
        col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            sphere.isTrigger = true;
            col = sphere;
        }
        
        // Renderer bul (child'da model varsa onun renderer'ı)
        meshRenderer = GetComponentInChildren<Renderer>();
        
        // Hole referansını bul
        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        if (hole != null)
        {
            holeTransform = hole.transform;
        }
        
        // Spawn animasyonu
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack);
        
        Debug.Log($"[SkillPickup] {skillType} spawned at {transform.position}");
    }
    
    void Update()
    {
        if (isBeingSwallowed) return;
        
        // Hole mesafe kontrolü
        if (holeTransform != null)
        {
            float distXZ = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(holeTransform.position.x, holeTransform.position.z)
            );
            
            float eatRadius = holeTransform.localScale.x * 0.7f;
            
            if (distXZ < eatRadius && !isFalling)
            {
                StartFalling();
            }
        }
        else
        {
            // Hole'u tekrar ara
            HoleMechanics hole = FindObjectOfType<HoleMechanics>();
            if (hole != null) holeTransform = hole.transform;
        }
        
        if (isFalling) return;
        
        // Floating animasyonu
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // Dönme animasyonu
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        
        // Lifetime kontrolü
        float timeAlive = Time.time - spawnTime;
        if (timeAlive > lifetime)
        {
            transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
            isBeingSwallowed = true; // Animasyonu durdur
        }
        
        // Son 5 saniyede yanıp sönme
        if (timeAlive > lifetime - 5f && meshRenderer != null)
        {
            float alpha = Mathf.PingPong(Time.time * 5f, 1f);
            Color c = meshRenderer.material.color;
            c.a = 0.3f + alpha * 0.7f;
            meshRenderer.material.color = c;
        }
    }
    
    void StartFalling()
    {
        isFalling = true;
        isBeingSwallowed = true;
        
        Debug.Log($"[SkillPickup] {skillType} hole'a düşüyor!");
        
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.drag = 0f;
            rb.constraints = RigidbodyConstraints.None;
            
            // Aşağı ve merkeze doğru kuvvet
            Vector3 toHole = (holeTransform.position - transform.position).normalized;
            rb.AddForce(toHole * 3f + Vector3.down * 10f, ForceMode.VelocityChange);
        }
        
        StartCoroutine(FallAndActivate());
    }
    
    System.Collections.IEnumerator FallAndActivate()
    {
        float fallTime = 0f;
        float maxFallTime = 2f;
        
        while (fallTime < maxFallTime)
        {
            fallTime += Time.deltaTime;
            
            // Merkeze çek
            if (holeTransform != null && rb != null)
            {
                Vector3 toCenter = holeTransform.position - transform.position;
                toCenter.y = 0;
                rb.AddForce(toCenter.normalized * 5f, ForceMode.Acceleration);
                rb.AddTorque(Vector3.one * 10f, ForceMode.Acceleration);
            }
            
            // Yeterince düştüyse bitir
            if (holeTransform != null && transform.position.y < holeTransform.position.y - 2f)
            {
                break;
            }
            
            yield return null;
        }
        
        // Skill aktif et
        if (SkillManager.Instance != null)
        {
            // Pickup üzerindeki flag'i kullanarak manager'a bildir
            SkillManager.Instance.ActivateSkill(skillType, isPermanentPickup);
            Debug.Log($"[SkillPickup] {skillType} skill aktif edildi! (Permanent: {isPermanentPickup})");
        }
        
        // Yok ol
        transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
    }
    
    // HoleMechanics tarafından çağrılabilir
    public void OnSwallowStart()
    {
        if (!isFalling)
        {
            StartFalling();
        }
    }
    
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
