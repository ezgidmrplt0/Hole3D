using UnityEngine;
using TMPro; // Standard for text in modern Unity

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI levelText;
    
    [Header("Zombie Counter")]
    public GameObject zombieCounterPanel;
    public TextMeshProUGUI zombieCounterText;
    
    [Header("Human Counter")]
    public TextMeshProUGUI humanCounterText;
    
    [Header("Horde Banner")]
    public GameObject hordeBannerPanel;
    public TextMeshProUGUI hordeBannerText;
    public float hordeBannerDuration = 2f;

    private void Start()
    {
        // Subscribe to events
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged += UpdateCoinText;
            // Force update initial value
            UpdateCoinText(EconomyManager.Instance.CurrentCoins);
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged += UpdateLevelText;
            LevelManager.Instance.OnZombieCountChanged += UpdateZombieCounter; // Subscribe to Zombie Count
            
            // Force update initial value (add 1 because index is 0-based)
            UpdateLevelText(LevelManager.Instance.currentLevelIndex + 1);
            
            // Initial Zombie Count update will happen when LevelManager calls StartLevel -> NotifyProgress
            // But if we missed it (Start order), we should manually check
             if (LevelManager.Instance.totalZombiesInLevel > 0)
            {
                UpdateZombieCounter(LevelManager.Instance.totalZombiesInLevel - LevelManager.Instance.currentZombiesEaten);
            }

            // Subscribe to Human Count
            LevelManager.Instance.OnHumanCountChanged += UpdateHumanCounter;
            UpdateHumanCounter(LevelManager.Instance.currentHumansEaten);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged -= UpdateCoinText;
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged -= UpdateLevelText;
            LevelManager.Instance.OnZombieCountChanged -= UpdateZombieCounter;
            LevelManager.Instance.OnHumanCountChanged -= UpdateHumanCounter;
        }
    }

    private void UpdateCoinText(int amount)
    {
        if (coinText != null)
        {
            coinText.text = amount.ToString();
        }
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = "LEVEL " + level;
        }
    }
    
    private void UpdateZombieCounter(int count)
    {
        if (zombieCounterText != null)
        {
            zombieCounterText.text = count.ToString();
        }
        
        // Opsiyonel: Eğer 0 olursa paneli gizle veya efekt yap
        // if (count <= 0 && zombieCounterPanel != null) zombieCounterPanel.SetActive(false);
        // Opsiyonel: Eğer 0 olursa paneli gizle veya efekt yap
        // if (count <= 0 && zombieCounterPanel != null) zombieCounterPanel.SetActive(false);
    }

    private void UpdateHumanCounter(int remainingHumans)
    {
        if (humanCounterText != null)
        {
            // Artık doğrudan kalan insan sayısını gösteriyoruz
            humanCounterText.text = remainingHumans.ToString();
        }
    }
    
    // --- HORDE BANNER ---
    public void ShowHordeBanner()
    {
        StartCoroutine(HordeBannerRoutine());
    }
    
    private System.Collections.IEnumerator HordeBannerRoutine()
    {
        // Eğer panel yoksa, dinamik oluştur
        if (hordeBannerPanel == null)
        {
            CreateHordeBannerDynamically();
        }
        
        if (hordeBannerPanel != null)
        {
            hordeBannerPanel.SetActive(true);
            
            // Animasyonlu giriş (Scale ile)
            RectTransform rect = hordeBannerPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.zero;
                
                // Büyüyerek gel
                float elapsed = 0f;
                float growDuration = 0.3f;
                while (elapsed < growDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / growDuration;
                    rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.2f, t);
                    yield return null;
                }
                
                // Biraz küçül (bounce efekti)
                elapsed = 0f;
                float shrinkDuration = 0.15f;
                while (elapsed < shrinkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shrinkDuration;
                    rect.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
                    yield return null;
                }
            }
            
            // Ekranda bekle
            yield return new WaitForSeconds(hordeBannerDuration);
            
            // Küçülerek git
            if (rect != null)
            {
                float elapsed = 0f;
                float shrinkDuration = 0.2f;
                while (elapsed < shrinkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shrinkDuration;
                    rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                    yield return null;
                }
            }
            
            hordeBannerPanel.SetActive(false);
        }
    }
    
    private void CreateHordeBannerDynamically()
    {
        // Ana UI Canvas'ı bul (Screen Space - Overlay veya Camera)
        // World Space canvas'ları (Hole'un canvas'ı gibi) atla
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Canvas mainCanvas = null;
        
        foreach (Canvas c in allCanvases)
        {
            // World Space değilse ve renderMode Screen Space ise ana canvas
            if (c.renderMode != RenderMode.WorldSpace)
            {
                mainCanvas = c;
                break;
            }
        }
        
        // Hiç Screen Space canvas bulunamadıysa, World Space olmayan ilk canvas'ı kullan
        if (mainCanvas == null)
        {
            // Son çare: Yeni bir canvas oluştur
            GameObject canvasObj = new GameObject("HordeBannerCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100; // En üstte görünsün
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Panel oluştur
        hordeBannerPanel = new GameObject("HordeBannerPanel");
        hordeBannerPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = hordeBannerPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(800, 200);
        
        // Arkaplan YOK - sadece yazı görünsün
        // (Image component eklemiyoruz)
        
        // Text oluştur
        GameObject textObj = new GameObject("HordeText");
        textObj.transform.SetParent(hordeBannerPanel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        hordeBannerText = textObj.AddComponent<TextMeshProUGUI>();
        hordeBannerText.text = "HORDE!";
        hordeBannerText.fontSize = 100; // Daha büyük
        hordeBannerText.fontStyle = FontStyles.Bold;
        hordeBannerText.color = Color.red;
        hordeBannerText.alignment = TextAlignmentOptions.Center;
        hordeBannerText.enableWordWrapping = false;
        
        // Güçlü outline efekti (okunabilirlik için)
        hordeBannerText.outlineWidth = 0.3f;
        hordeBannerText.outlineColor = Color.black;
        
        hordeBannerPanel.SetActive(false);
    }
    [Header("Panels")]
    public GameObject marketPanel;

    public void OpenMarket()
    {
        if (marketPanel != null)
        {
            marketPanel.SetActive(true);
            // Oyunun geri planını durdurmak isterseniz: Time.timeScale = 0;
        }
    }

    public void CloseMarket()
    {
        if (marketPanel != null)
        {
            marketPanel.SetActive(false);
            // Time.timeScale = 1;
        }
    }

    // Shop butonlarına (Unity Inspector'dan) bu fonksiyonu verip, parametre olarak coin miktarını girebilirsiniz.
    // Shop butonlarına (Unity Inspector'dan) bu fonksiyonu verip, parametre olarak coin miktarını girebilirsiniz.
    public void BuyCoinPack(int amount)
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCoins(amount);
            // Burada isteğe bağlı olarak satın alma sesi veya efekti eklenebilir.
        }
    }

    [Header("Skill Upgrade UI (Store)")]
    public UnityEngine.UI.Button magnetButton;
    public TextMeshProUGUI magnetPriceText;
    public TextMeshProUGUI magnetLevelText;

    public UnityEngine.UI.Button speedButton;
    public TextMeshProUGUI speedPriceText;
    public TextMeshProUGUI speedLevelText;

    public UnityEngine.UI.Button shieldButton;  // Repellent -> Shield olarak değişti
    public TextMeshProUGUI shieldPriceText;
    public TextMeshProUGUI shieldLevelText;
    
    [Header("Active Skill Indicator (In-Game)")]
    public GameObject activeSkillPanel;
    public UnityEngine.UI.Image activeSkillIcon;
    public TextMeshProUGUI activeSkillTimerText;
    
    [Header("Skill Icons")]
    public Sprite magnetIcon;
    public Sprite speedIcon;
    public Sprite shieldIcon;

    private void UpdateSkillUI()
    {
        if (SkillManager.Instance == null) return;

        // Magnet
        if (magnetButton != null)
        {
            int level = SkillManager.Instance.MagnetUpgradeLevel;
            int price = SkillManager.Instance.GetUpgradePrice(SkillType.Magnet);
            bool isMaxLevel = price < 0;
            
            magnetButton.interactable = !isMaxLevel && SkillManager.Instance.CanUpgrade(SkillType.Magnet);
            
            if (magnetPriceText != null)
                magnetPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (magnetLevelText != null)
                magnetLevelText.text = "Lv." + level;
        }

        // Speed
        if (speedButton != null)
        {
            int level = SkillManager.Instance.SpeedUpgradeLevel;
            int price = SkillManager.Instance.GetUpgradePrice(SkillType.Speed);
            bool isMaxLevel = price < 0;
            
            speedButton.interactable = !isMaxLevel && SkillManager.Instance.CanUpgrade(SkillType.Speed);
            
            if (speedPriceText != null)
                speedPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (speedLevelText != null)
                speedLevelText.text = "Lv." + level;
        }

        // Shield
        if (shieldButton != null)
        {
            int level = SkillManager.Instance.ShieldUpgradeLevel;
            int price = SkillManager.Instance.GetUpgradePrice(SkillType.Shield);
            bool isMaxLevel = price < 0;
            
            shieldButton.interactable = !isMaxLevel && SkillManager.Instance.CanUpgrade(SkillType.Shield);
            
            if (shieldPriceText != null)
                shieldPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (shieldLevelText != null)
                shieldLevelText.text = "Lv." + level;
        }
    }
    
    private void Update()
    {
        // Aktif skill göstergesini güncelle
        UpdateActiveSkillIndicator();
    }
    
    private void UpdateActiveSkillIndicator()
    {
        if (SkillManager.Instance == null || activeSkillPanel == null) return;
        
        // En uzun süre kalan aktif skill'i bul
        SkillType? activeSkill = null;
        float maxTime = 0f;
        
        foreach (SkillType type in System.Enum.GetValues(typeof(SkillType)))
        {
            if (SkillManager.Instance.IsSkillActive(type))
            {
                float remaining = SkillManager.Instance.GetRemainingTime(type);
                if (remaining > maxTime)
                {
                    maxTime = remaining;
                    activeSkill = type;
                }
            }
        }
        
        if (activeSkill.HasValue)
        {
            activeSkillPanel.SetActive(true);
            
            // İkon ayarla
            if (activeSkillIcon != null)
            {
                activeSkillIcon.sprite = activeSkill.Value switch
                {
                    SkillType.Magnet => magnetIcon,
                    SkillType.Speed => speedIcon,
                    SkillType.Shield => shieldIcon,
                    _ => null
                };
            }
            
            // Timer göster
            if (activeSkillTimerText != null)
            {
                activeSkillTimerText.text = maxTime.ToString("F1") + "s";
            }
        }
        else
        {
            activeSkillPanel.SetActive(false);
        }
    }

    // Assign to Button OnClick
    public void UpgradeMagnet()
    {
        Debug.Log("UIManager: UpgradeMagnet clicked.");

        if (SkillManager.Instance == null) return;

        if (SkillManager.Instance.TryUpgrade(SkillType.Magnet))
        {
            UpdateSkillUI();
        }
    }

    public void UpgradeSpeed()
    {
        Debug.Log("UIManager: UpgradeSpeed clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.TryUpgrade(SkillType.Speed))
        {
            UpdateSkillUI();
        }
    }

    public void UpgradeShield()  // Eski: UpgradeRepellent
    {
        Debug.Log("UIManager: UpgradeShield clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.TryUpgrade(SkillType.Shield))
        {
            UpdateSkillUI();
        }
    }
    
    // Eski API uyumluluk (Scene'de Repellent butonu varsa çalışsın)
    public void UpgradeRepellent() => UpgradeShield();

    private void OnEnable()
    {
        UpdateSkillUI();
        
        // Skill değişikliklerini dinle
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnUpgradesChanged += UpdateSkillUI;
        }
    }
    
    private void OnDisable()
    {
        // Event'ten çık (Memory leak önleme)
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnUpgradesChanged -= UpdateSkillUI;
        }
    }
}
