using UnityEngine;

/// <summary>
/// Mobil cihazlarda performans ve uyumluluk sorunlarını çözen script.
/// Sahneye boş bir GameObject ekleyip bu scripti atayın.
/// </summary>
public class MobileOptimizer : MonoBehaviour
{
    [Header("Mobil Ayarları")]
    [Tooltip("Hedef FPS (30 veya 60)")]
    public int targetFrameRate = 60;
    
    [Tooltip("Fixed Timestep (Fizik güncelleme hızı - düşük değer = daha stabil ama yavaş)")]
    public float fixedTimestep = 0.02f; // 50 FPS fizik
    
    [Tooltip("Mobilde VSync aktif olsun mu?")]
    public bool enableVSync = false;

    private void Awake()
    {
        // Singleton pattern - sadece bir tane olsun
        MobileOptimizer[] optimizers = FindObjectsOfType<MobileOptimizer>();
        if (optimizers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        
        ApplyMobileSettings();
    }

    private void ApplyMobileSettings()
    {
        // 1. FPS Hedefi
        Application.targetFrameRate = targetFrameRate;
        
        // 2. VSync
        QualitySettings.vSyncCount = enableVSync ? 1 : 0;
        
        // 3. Fixed Timestep (Fizik tutarlılığı için kritik!)
        Time.fixedDeltaTime = fixedTimestep;
        
        // 4. Mobil için özel ayarlar
        if (Application.isMobilePlatform)
        {
            // Ekran uyumaz
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            // Düşük kalite ayarları (Performans için)
            // QualitySettings.SetQualityLevel(1, true); // "Low" seviyesi
            
            // Shader warmup - pembe shader sorununu önler
            Shader.WarmupAllShaders();
            
            // Physics ayarları
            Physics.autoSyncTransforms = true; // Transform değişiklikleri anında fizik'e yansısın
            Physics.reuseCollisionCallbacks = true; // GC azaltma
            
            Debug.Log("MobileOptimizer: Mobil ayarları uygulandı.");
        }
        else
        {
            Debug.Log("MobileOptimizer: Editor/PC modunda çalışıyor.");
        }
        
        // 5. DOTween ayarları (Eğer DOTween kullanılıyorsa)
        InitializeDOTween();
    }
    
    private void InitializeDOTween()
    {
        // DOTween'in mobil uyumlu başlatılması
        try
        {
            // DOTween varsayılan ayarlarını optimize et
            DG.Tweening.DOTween.SetTweensCapacity(200, 50);
            
            // Güvenli mod (Hata durumunda crash önleme)
            DG.Tweening.DOTween.useSafeMode = true;
            
            Debug.Log("MobileOptimizer: DOTween ayarları uygulandı.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("MobileOptimizer: DOTween bulunamadı veya hata oluştu: " + e.Message);
        }
    }
    
    // Inspector'dan test için
    [ContextMenu("Mobil Ayarlarını Uygula")]
    private void ApplySettingsManually()
    {
        ApplyMobileSettings();
    }
}
