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
    
    [Header("Android Fix - Material Swap")]
    [Tooltip("Android build için: Önceden hazırlanmış transparent material kullanır")]
    public bool useMaterialSwap = true; // Android için material swap modu
    
    [Tooltip("Resources klasöründeki transparent material adı (örn: 'FadeMaterial')")]
    public string fadeMaterialName = "FadeMaterial";
    
    // Önceden yüklenmiş fade material (Resources'dan)
    private Material fadeMaterialTemplate;

    private Transform cameraTransform;
    private Transform myTransform;
    
    private Dictionary<Renderer, MaterialModeData> fadedRenderers = new Dictionary<Renderer, MaterialModeData>();
    private List<Renderer> hitRenderersThisFrame = new List<Renderer>();

    private class MaterialModeData
    {
        public Material material;
        public Material[] originalMaterials; // Orijinal materyalleri sakla
        public Material[] fadeMaterials; // Fade için oluşturulan materyaller
        public Color[] originalColors; // Orijinal renkler
        public float originalAlpha;
        public int originalMode;
        public int originalSrcBlend;
        public int originalDstBlend;
        public int originalZWrite;
        public int originalRenderQueue;
        public bool isURP; // URP tespiti
        public bool wasEnabled; // Renderer açık mıydı
        public Renderer renderer; // Renderer referansı
        public Tweener currentTween; // Aktif tween
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

        // ★ ANDROID FIX: Fade Material'ı Resources'dan yükle
        if (useMaterialSwap)
        {
            fadeMaterialTemplate = Resources.Load<Material>(fadeMaterialName);
            if (fadeMaterialTemplate == null)
            {
                Debug.LogWarning($"[ObstructionFader] ⚠ '{fadeMaterialName}' bulunamadı! Resources klasörüne 'FadeMaterial' adında transparent bir material ekleyin. Legacy moda geçiliyor.");
                useMaterialSwap = false;
            }
            else
            {
                if (showDebugLogs) Debug.Log($"[ObstructionFader] ✓ Fade Material yüklendi: {fadeMaterialTemplate.name}");
            }
        }
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

    public void ForceRestoreAll()
    {
        foreach (var kvp in fadedRenderers)
        {
            Renderer r = kvp.Key;
            MaterialModeData data = kvp.Value;
            if (r != null && data != null)
            {
                // ★ ANDROID FIX: Material Swap için instant restore
                if (useMaterialSwap && data.originalMaterials != null)
                {
                    r.materials = data.originalMaterials;
                    
                    // Fade materyallerini temizle
                    if (data.fadeMaterials != null)
                    {
                        foreach (var fadeMat in data.fadeMaterials)
                        {
                            if (fadeMat != null) Destroy(fadeMat);
                        }
                    }
                }
                else
                {
                    // Legacy Restore
                    Restore(r, data); 
                }
            }
        }
        fadedRenderers.Clear();
        hitRenderersThisFrame.Clear();
    }
    
    private bool IsIgnored(GameObject obj)
    {
        // 1. Oyuncu
        if (obj == gameObject || obj.transform.root == transform.root) return true;
        if (obj.CompareTag("Player")) return true;
        
        // 2. Zombi, İnsanlar ve Skill Pickup'lar (Karakterler ve pickup'lar transparan olmasın)
        if (obj.CompareTag("Zombie") || obj.CompareTag("Human") || obj.CompareTag("SkillPickup")) return true;
        // Karakterlerin çocuk objeleri de olabilir (mesh vs), root'a bak
        Transform root = obj.transform.root;
        if (root.CompareTag("Zombie") || root.CompareTag("Human") || root.CompareTag("SkillPickup")) return true;
        
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
        // ★ ANDROID FIX: Material Swap Mode - Önceden hazır transparent material kullan
        if (useMaterialSwap && fadeMaterialTemplate != null)
        {
            MaterialModeData data = new MaterialModeData();
            data.renderer = r;
            data.originalMaterials = r.sharedMaterials; // Orijinalleri sakla
            
            // Her material için fade versiyonu oluştur
            Material[] newMaterials = new Material[r.sharedMaterials.Length];
            data.originalColors = new Color[r.sharedMaterials.Length];
            
            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material originalMat = r.sharedMaterials[i];
                if (originalMat == null) continue;
                
                // Fade material'dan yeni instance oluştur
                Material fadeMat = new Material(fadeMaterialTemplate);
                
                // Orijinal rengi al ve kopyala
                Color originalColor = Color.white;
                if (originalMat.HasProperty("_Color"))
                    originalColor = originalMat.GetColor("_Color");
                else if (originalMat.HasProperty("_BaseColor"))
                    originalColor = originalMat.GetColor("_BaseColor");
                
                data.originalColors[i] = originalColor;
                
                // Orijinal texture'ı kopyala (varsa)
                if (originalMat.HasProperty("_MainTex") && fadeMat.HasProperty("_MainTex"))
                {
                    fadeMat.SetTexture("_MainTex", originalMat.GetTexture("_MainTex"));
                }
                
                // Başlangıç rengi (tam opak)
                Color startColor = originalColor;
                startColor.a = 1f;
                fadeMat.SetColor("_Color", startColor);
                
                newMaterials[i] = fadeMat;
            }
            
            data.fadeMaterials = newMaterials;
            fadedRenderers.Add(r, data);
            
            // Material'ları değiştir
            r.materials = newMaterials;
            
            // Smooth alpha fade
            for (int i = 0; i < newMaterials.Length; i++)
            {
                if (newMaterials[i] == null) continue;
                
                Color fadeTargetColor = data.originalColors[i];
                fadeTargetColor.a = fadeAlpha;
                
                newMaterials[i].DOColor(fadeTargetColor, "_Color", fadeDuration);
            }
            
            if (showDebugLogs)
                Debug.Log($"[ObstructionFader] ★ Material Swap fade başladı: {r.name}");
            
            return;
        }
        
        // --- LEGACY TRANSPARENT MODE (Editor/PC için - Material swap yoksa) ---
        Material mat = r.material;
        
        if (mat == null)
        {
            Debug.LogWarning($"[ObstructionFader] ✗ {r.name} için Material NULL!");
            return;
        }
        
        MaterialModeData dataLegacy = new MaterialModeData();
        dataLegacy.material = mat;
        dataLegacy.renderer = r;
        
        // Rengi al (URP vs Standard uyumu)
        Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        
        dataLegacy.originalAlpha = col.a;
        dataLegacy.originalRenderQueue = mat.renderQueue;
        
        // URP Kontrolü
        dataLegacy.isURP = mat.HasProperty("_BaseColor");
        
        if (showDebugLogs)
            Debug.Log($"[ObstructionFader] Material Info: {mat.name} | URP: {dataLegacy.isURP} | Shader: {mat.shader.name} | OriginalAlpha: {dataLegacy.originalAlpha}");

        if (mat.HasProperty("_Mode")) dataLegacy.originalMode = (int)mat.GetFloat("_Mode");
        if (mat.HasProperty("_SrcBlend")) dataLegacy.originalSrcBlend = mat.GetInt("_SrcBlend");
        if (mat.HasProperty("_DstBlend")) dataLegacy.originalDstBlend = mat.GetInt("_DstBlend");
        if (mat.HasProperty("_ZWrite")) dataLegacy.originalZWrite = mat.GetInt("_ZWrite");

        fadedRenderers.Add(r, dataLegacy);

        // Fade Moduna Geç
        SetMaterialToFade(mat, dataLegacy.isURP);

        // Tween Alpha
        Color targetColor = col;
        targetColor.a = fadeAlpha;
        
        if (showDebugLogs)
            Debug.Log($"[ObstructionFader] Alpha değişiyor: {col.a} → {fadeAlpha}");
        
        if (dataLegacy.isURP)
            mat.DOColor(targetColor, "_BaseColor", fadeDuration);
        else
            mat.DOColor(targetColor, fadeDuration);
    }

    private void Restore(Renderer r, MaterialModeData data)
    {
        if (r == null) return;
        
        // ★ ANDROID FIX: Material Swap Mode - Orijinal materyallere geri dön
        if (useMaterialSwap && data.originalMaterials != null && data.fadeMaterials != null)
        {
            // Önce alpha'yı geri getir, sonra material'ı değiştir
            for (int i = 0; i < data.fadeMaterials.Length; i++)
            {
                if (data.fadeMaterials[i] == null) continue;
                
                Color restoreTargetColor = data.originalColors[i];
                restoreTargetColor.a = 1f; // Tam opak
                
                int index = i;
                data.fadeMaterials[i].DOColor(restoreTargetColor, "_Color", fadeDuration).OnComplete(() =>
                {
                    // Tüm fade'ler bitince orijinal materyallere dön
                    if (index == data.fadeMaterials.Length - 1)
                    {
                        if (r != null && data.originalMaterials != null)
                        {
                            r.materials = data.originalMaterials;
                            
                            // Fade materyallerini temizle
                            foreach (var fadeMat in data.fadeMaterials)
                            {
                                if (fadeMat != null) Destroy(fadeMat);
                            }
                        }
                    }
                });
            }
            
            if (showDebugLogs)
                Debug.Log($"[ObstructionFader] ★ Material Swap restore başladı: {r.name}");
            
            return;
        }
        
        // --- LEGACY TRANSPARENT MODE ---
        if (data.material == null) return;

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
            material.SetFloat("_Surface", 1); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
            
            // ★ AAB BUILD FIX: URP için gerekli keyword'leri etkinleştir
            // Bu keyword'ler olmadan Android build'de şeffaflık çalışmaz!
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); // URP Transparent için gerekli
            
            // Blend Mode ayarla (Alpha)
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0); // 0 = Alpha blend
            }
            
            // Alpha Clipping kapalı olmalı (Fade için)
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0);
                material.DisableKeyword("_ALPHATEST_ON");
            }
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
            
            // ★ AAB BUILD FIX: Transparent keyword'leri kapat
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            
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
