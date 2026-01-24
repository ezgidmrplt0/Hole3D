using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; // Using DOTween for smooth fading

public class ObstructionFader : MonoBehaviour
{
    [Header("Settings")]
    public float fadeAlpha = 0.1f; // Daha da şeffaf (Neredeyse görünmez)
    public float fadeDuration = 0.2f; // Daha hızlı geçiş
    public LayerMask obstructionMask = -1;
    
    [Header("Contact Fading")]
    public bool fadeOnContact = true; // Temasta şeffaflaşsın mı?
    public float contactRadiusMultiplier = 1.5f; // Deliğin boyutuyla çarpılır (Yakınlık mesafesi)

    [Header("Debug")]
    public bool showDebugLogs = true; // Debug logları aç/kapa

    private Transform cameraTransform;
    private Transform myTransform;
    
    private Dictionary<Renderer, MaterialModeData> fadedRenderers = new Dictionary<Renderer, MaterialModeData>();
    private List<Renderer> hitRenderersThisFrame = new List<Renderer>();

    private class MaterialModeData
    {
        public Material material;
        public float originalAlpha;
        public int originalMode;
        public int originalSrcBlend;
        public int originalDstBlend;
        public int originalZWrite;
        public int originalRenderQueue;
        public bool isURP; // URP tespiti
    }

    private void Start()
    {
        myTransform = transform;
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            if (showDebugLogs) Debug.Log($"[ObstructionFader] ✓ Başlatıldı. Kamera: {cameraTransform.name}");
        }
        else
        {
            Debug.LogError("[ObstructionFader] ✗ Camera.main bulunamadı! Script devre dışı.");
            this.enabled = false;
            return;
        }

        // Başlangıçta obstructionMask ayarlıyorduk ama artık daha dinamik olacağız.
        // Yine de performans için bir temel maske tutabiliriz ama kullanıcı "her şey" dediği için
        // Everything maskesi üzerinden gidip, kod içinde elemek daha garantidir.
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        // 1. KAMERA ENGEL KONTROLÜ (Raycast)
        // Maskeyi kaldırıp, her şeye çarpmasını sağlıyoruz (Everything)
        // Böylece yeni gelen haritanın layerı ne olursa olsun algılanır.
        int layerMask = ~0; 
        
        Vector3 startPos = myTransform.position + Vector3.up * 0.5f;
        Vector3 direction = cameraTransform.position - startPos;
        float distance = direction.magnitude;

        hitRenderersThisFrame.Clear();

        // QueryTriggerInteraction.Ignore -> Triggerlara (Coin, XP vb) çarpmasın, sadece katı objelere (duvar, çatı) çarpsın.
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, distance, layerMask, QueryTriggerInteraction.Ignore);
        
        if (showDebugLogs && Time.frameCount % 60 == 0) // Her saniye bir log
        {
            Debug.Log($"[ObstructionFader] Raycast: {hits.Length} obje bulundu. Mesafe: {distance:F1}m");
        }
        
        // Debug çizgisi (Scene view'da görünür)
        Debug.DrawRay(startPos, direction.normalized * distance, hits.Length > 0 ? Color.red : Color.green);
        
        foreach (RaycastHit hit in hits)
        {
            // Filtreleme: Zemin mi? Oyuncu mu?
            if (IsIgnored(hit.collider.gameObject)) 
            {
                if (showDebugLogs && Time.frameCount % 60 == 0)
                    Debug.Log($"[ObstructionFader] → Yoksayıldı (Raycast): {hit.collider.name}");
                continue;
            }
            
            if (showDebugLogs)
                Debug.Log($"[ObstructionFader] ★ Engel bulundu (Raycast): {hit.collider.name}");
            
            Renderer r = GetRendererFromCollider(hit.collider);
            AddRendererToFrame(r);
        }

        // 2. TEMAS KONTROLÜ (Proximity / Contact)
        if (fadeOnContact)
        {
            float radius = myTransform.lossyScale.x * contactRadiusMultiplier;
            
            // OverlapSphere de Everything maskesiyle
            Collider[] contacts = Physics.OverlapSphere(myTransform.position, radius, layerMask);
            
            if (showDebugLogs && Time.frameCount % 60 == 0)
                Debug.Log($"[ObstructionFader] OverlapSphere: {contacts.Length} obje, Radius: {radius:F1}m");
            
            foreach (Collider col in contacts)
            {
                if (col.isTrigger) continue; // Triggerları (Coin vs) yoksay
                if (IsIgnored(col.gameObject)) 
                {
                    if (showDebugLogs && Time.frameCount % 60 == 0)
                        Debug.Log($"[ObstructionFader] → Yoksayıldı (Contact): {col.name}");
                    continue;
                }

                if (showDebugLogs)
                    Debug.Log($"[ObstructionFader] ★ Temas engeli: {col.name}");

                Renderer r = GetRendererFromCollider(col);
                AddRendererToFrame(r);
            }
        }

        // --- RESTORE LOGIC ---
        List<Renderer> toRemove = new List<Renderer>();
        foreach (var kvp in fadedRenderers)
        {
            Renderer r = kvp.Key;
            if (r == null) 
            {
                toRemove.Add(r);
                continue;
            }

            if (!hitRenderersThisFrame.Contains(r))
            {
                Restore(r, kvp.Value);
                toRemove.Add(r);
            }
        }

        foreach (var r in toRemove) fadedRenderers.Remove(r);
    }
    
    private bool IsIgnored(GameObject obj)
    {
        // 1. Oyuncu
        if (obj == gameObject || obj.transform.root == transform.root) return true;
        if (obj.CompareTag("Player")) return true;
        
        // 2. Zombi ve İnsanlar (Karakterler transparan olmasın)
        if (obj.CompareTag("Zombie") || obj.CompareTag("Human")) return true;
        // Karakterlerin çocuk objeleri de olabilir (mesh vs), root'a bak
        Transform root = obj.transform.root;
        if (root.CompareTag("Zombie") || root.CompareTag("Human")) return true;
        
        // 3. UI ve Water (Genelde transparan olmamalı)
        if (obj.layer == LayerMask.NameToLayer("UI")) return true;
        if (obj.layer == LayerMask.NameToLayer("Water")) return true;
        
        // 4. ZEMİN KONTROLÜ - Sadece GERÇEK zeminler (Yatay düz yüzeyler)
        // Ground tag'i yetersiz çünkü LevelManager her şeye Ground atıyor
        // Bunun yerine isim kontrolü + collider yönü kontrolü yapalım
        string nameLower = obj.name.ToLower();
        
        // Sadece "floor" veya "plane" içeren isimleri zemin say (Daha dar kapsam)
        // "ground" kelimesi çok genel, onu kaldırıyoruz
        if (nameLower.Contains("floor") || nameLower.Contains("plane") || nameLower.Contains("terrain"))
        {
            return true;
        }
        
        // 5. Deliğin altındaki zemini yoksay (Y pozisyonuna göre)
        // Eğer obje deliğin altındaysa ve yatay bir yüzeyse, zemin demektir
        if (obj.transform.position.y < myTransform.position.y - 0.5f)
        {
            // Yatay yüzey kontrolü: Collider'ın boyutlarına bak
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 size = col.bounds.size;
                // Yatay/düz bir obje: X ve Z boyutu Y boyutundan çok büyükse
                if (size.x > size.y * 3 && size.z > size.y * 3)
                {
                    if (showDebugLogs) Debug.Log($"[ObstructionFader] Zemin tespit edildi (boyut): {obj.name}");
                    return true;
                }
            }
        }
        
        return false;
    }

    private void AddRendererToFrame(Renderer r)
    {
        if (r != null)
        {
            if (!fadedRenderers.ContainsKey(r))
            {
                if (showDebugLogs)
                    Debug.Log($"[ObstructionFader] ✓ Fade başlatılıyor: {r.name} | Material: {r.material?.name ?? "NULL"}");
                CacheAndFade(r);
            }
            if (!hitRenderersThisFrame.Contains(r))
            {
                hitRenderersThisFrame.Add(r);
            }
        }
    }
    
    // Collider'dan Renderer bulmaya çalış (Çocuklara da bak)
    private Renderer GetRendererFromCollider(Collider col)
    {
        if (col == null) return null;
        
        // 1. Önce kendi üzerinde ara
        Renderer r = col.GetComponent<Renderer>();
        if (r != null) return r;
        
        // 2. Çocuklarda ara (Character modelleri için)
        r = col.GetComponentInChildren<Renderer>();
        if (r != null) return r;
        
        // 3. Parent'ta ara (Bazı prefablarda collider child'da olabilir)
        r = col.GetComponentInParent<Renderer>();
        
        return r;
    }

    private void CacheAndFade(Renderer r)
    {
        Material mat = r.material;
        
        if (mat == null)
        {
            Debug.LogWarning($"[ObstructionFader] ✗ {r.name} için Material NULL!");
            return;
        }
        
        MaterialModeData data = new MaterialModeData();
        data.material = mat;
        
        // Rengi al (URP vs Standard uyumu)
        Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        
        data.originalAlpha = col.a;
        data.originalRenderQueue = mat.renderQueue;
        
        // URP Kontrolü
        data.isURP = mat.HasProperty("_BaseColor");
        
        if (showDebugLogs)
            Debug.Log($"[ObstructionFader] Material Info: {mat.name} | URP: {data.isURP} | Shader: {mat.shader.name} | OriginalAlpha: {data.originalAlpha}");

        if (mat.HasProperty("_Mode")) data.originalMode = (int)mat.GetFloat("_Mode");
        if (mat.HasProperty("_SrcBlend")) data.originalSrcBlend = mat.GetInt("_SrcBlend");
        if (mat.HasProperty("_DstBlend")) data.originalDstBlend = mat.GetInt("_DstBlend");
        if (mat.HasProperty("_ZWrite")) data.originalZWrite = mat.GetInt("_ZWrite");

        fadedRenderers.Add(r, data);

        // Fade Moduna Geç
        SetMaterialToFade(mat, data.isURP);

        // Tween Alpha
        Color targetColor = col;
        targetColor.a = fadeAlpha;
        
        if (showDebugLogs)
            Debug.Log($"[ObstructionFader] Alpha değişiyor: {col.a} → {fadeAlpha}");
        
        if (data.isURP)
            mat.DOColor(targetColor, "_BaseColor", fadeDuration);
        else
            mat.DOColor(targetColor, fadeDuration);
    }

    private void Restore(Renderer r, MaterialModeData data)
    {
        if (r == null || data.material == null) return;

        Material mat = data.material;
        
        // Mevcut rengi al
        Color currentCol = data.isURP ? mat.GetColor("_BaseColor") : mat.color;
        
        // Hedef renk (Orijinal Alpha)
        Color targetColor = currentCol;
        targetColor.a = data.originalAlpha;
        
        // Tween
        if (data.isURP)
        {
            mat.DOColor(targetColor, "_BaseColor", fadeDuration).OnComplete(() => RestoreMaterialMode(mat, data));
        }
        else
        {
            mat.DOColor(targetColor, fadeDuration).OnComplete(() => RestoreMaterialMode(mat, data));
        }
    }

    // --- Shader Utils ---

    private void SetMaterialToFade(Material material, bool isURP)
    {
        // Standart Shader
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 2); // Fade
        }
        
        // URP Surface Type (0: Opaque, 1: Transparent)
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
        }
        else
        {
            // Standard / Legacy Pipeline
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void RestoreMaterialMode(Material material, MaterialModeData data)
    {
        // URP Restore
        if (data.isURP && material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0); // Opaque
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = data.originalRenderQueue;
            return;
        }

        // Standard Restore
        if (!material.HasProperty("_Mode")) return;

        material.SetFloat("_Mode", data.originalMode);
        
        if (data.originalMode == 0) // Opaque
        {
            material.SetOverrideTag("RenderType", "");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = data.originalRenderQueue == -1 ? -1 : data.originalRenderQueue;
        }
    }
}
