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
        }
        else
        {
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
        
        foreach (RaycastHit hit in hits)
        {
            // Filtreleme: Zemin mi? Oyuncu mu?
            if (IsIgnored(hit.collider.gameObject)) continue;
            
            Renderer r = hit.collider.GetComponent<Renderer>();
            AddRendererToFrame(r);
        }

        // 2. TEMAS KONTROLÜ (Proximity / Contact)
        if (fadeOnContact)
        {
            float radius = myTransform.lossyScale.x * contactRadiusMultiplier;
            
            // OverlapSphere de Everything maskesiyle
            Collider[] contacts = Physics.OverlapSphere(myTransform.position, radius, layerMask);
            
            foreach (Collider col in contacts)
            {
                if (col.isTrigger) continue; // Triggerları (Coin vs) yoksay
                if (IsIgnored(col.gameObject)) continue;

                Renderer r = col.GetComponent<Renderer>();
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
        
        // 2. UI ve Water (Genelde transparan olmamalı)
        if (obj.layer == LayerMask.NameToLayer("UI")) return true;
        if (obj.layer == LayerMask.NameToLayer("Water")) return true;
        
        // 3. ZEMİN KONTROLÜ (Ground)
        // Layer veya Tag "Ground" / "Floor" ise
        if (obj.layer == LayerMask.NameToLayer("Ground")) return true;
        if (obj.CompareTag("Ground") || obj.CompareTag("Floor")) return true;

        // 4. İSİM KONTROLÜ (Assetlerden gelen isimsiz zeminler için)
        // İsmi "Ground", "Floor", "Terrain" içerenleri zemin say
        string name = obj.name.ToLower();
        if (name.Contains("ground") || name.Contains("floor") || name.Contains("terrain")) return true;
        
        return false;
    }

    private void AddRendererToFrame(Renderer r)
    {
        if (r != null)
        {
            if (!fadedRenderers.ContainsKey(r))
            {
                CacheAndFade(r);
            }
            if (!hitRenderersThisFrame.Contains(r))
            {
                hitRenderersThisFrame.Add(r);
            }
        }
    }

    private void CacheAndFade(Renderer r)
    {
        Material mat = r.material;
        MaterialModeData data = new MaterialModeData();
        data.material = mat;
        
        // Rengi al (URP vs Standard uyumu)
        Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        
        data.originalAlpha = col.a;
        data.originalRenderQueue = mat.renderQueue;
        
        // URP Kontrolü
        data.isURP = mat.HasProperty("_BaseColor");

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
