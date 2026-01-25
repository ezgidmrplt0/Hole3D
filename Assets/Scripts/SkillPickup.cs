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
    
    void Start()
    {
        spawnTime = Time.time;
        startPos = transform.position;
        
        // Renderer bul ve renk ayarla
        meshRenderer = GetComponentInChildren<Renderer>();
        ApplyColor();
        
        // Spawn animasyonu
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }
    
    void Update()
    {
        // Floating animasyonu (Yukarı aşağı)
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
    
    void OnTriggerEnter(Collider other)
    {
        // Hole tarafından yutuldu mu?
        if (other.CompareTag("Player") || other.GetComponentInParent<HoleMechanics>() != null)
        {
            // Skill'i aktif et
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.ActivateSkill(skillType);
                
                // Pickup efekti
                PlayPickupEffect();
            }
            
            // Kendini yok et
            Destroy(gameObject);
        }
    }
    
    void PlayPickupEffect()
    {
        // Basit scale-up ve fade efekti
        // İleride particle eklenebilir
        
        // Ses efekti (varsa)
        // AudioManager.Instance?.PlayPickupSound();
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
