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
    public TMP_FontAsset bannerFont; // Added for dynamic font assignment

    private void Start()
    {
        // AUTO-WIRE LEVEL MARKET BUTTONS
        if (levelMagnetButton == null || levelSpeedButton == null || levelShieldButton == null)
        {
            Transform tapPanel = transform.Find("TapToPanel"); // Assuming UIManager is on Canvas
            if (tapPanel == null) 
            {
                // Try finding globally if UIManager is not on Canvas
                GameObject panelObj = GameObject.Find("TapToPanel");
                if (panelObj != null) tapPanel = panelObj.transform;
            }

            if (tapPanel != null)
            {
                Transform skills = tapPanel.Find("Skills");
                if (skills != null)
                {
                    // Magnet
                    Transform tMag = skills.Find("Magnet");
                    // Eğer doğrudan butonsa
                    if (tMag != null)
                    {
                        if (levelMagnetButton == null) levelMagnetButton = tMag.GetComponent<UnityEngine.UI.Button>();
                        if (levelMagnetButton == null) levelMagnetButton = tMag.GetComponentInChildren<UnityEngine.UI.Button>(); // Container ise
                        
                        if (levelMagnetPriceText == null && tMag != null) levelMagnetPriceText = tMag.GetComponentInChildren<TextMeshProUGUI>();
                    }

                    // Speed
                    Transform tSpeed = skills.Find("Speed");
                    if (tSpeed != null)
                    {
                        if (levelSpeedButton == null) levelSpeedButton = tSpeed.GetComponent<UnityEngine.UI.Button>();
                        if (levelSpeedButton == null) levelSpeedButton = tSpeed.GetComponentInChildren<UnityEngine.UI.Button>();
                        
                        if (levelSpeedPriceText == null && tSpeed != null) levelSpeedPriceText = tSpeed.GetComponentInChildren<TextMeshProUGUI>();
                    }

                    // Shield
                    Transform tShield = skills.Find("Shield");
                    if (tShield != null)
                    {
                        if (levelShieldButton == null) levelShieldButton = tShield.GetComponent<UnityEngine.UI.Button>();
                        if (levelShieldButton == null) levelShieldButton = tShield.GetComponentInChildren<UnityEngine.UI.Button>();
                        
                        if (levelShieldPriceText == null && tShield != null) levelShieldPriceText = tShield.GetComponentInChildren<TextMeshProUGUI>();
                    }
                }
            }
        }

        // Add Listeners - ONLY if no persistent listener exists (to avoid duplicates with Editor Tool)
        if (levelMagnetButton != null && levelMagnetButton.onClick.GetPersistentEventCount() == 0) 
            levelMagnetButton.onClick.AddListener(BuyLevelMagnet);
            
        if (levelSpeedButton != null && levelSpeedButton.onClick.GetPersistentEventCount() == 0) 
            levelSpeedButton.onClick.AddListener(BuyLevelSpeed);
            
        if (levelShieldButton != null && levelShieldButton.onClick.GetPersistentEventCount() == 0) 
            levelShieldButton.onClick.AddListener(BuyLevelShield);

        // Subscribe to events
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged += UpdateCoinText;
            EconomyManager.Instance.OnCoinsChanged += (amount) => UpdateSkillUI(); // Update buttons when coins change
            // Force update initial value
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
        if (bannerFont != null) hordeBannerText.font = bannerFont; // Assign Custom Font
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
    public void BuyCoinPack(int amount)
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCoins(amount);
            // Burada isteğe bağlı olarak satın alma sesi veya efekti eklenebilir.
        }
    }

    // [Header("Skill Upgrade UI (Store)")] REMOVED
    
    [Header("Level Market (Single Use)")]
    public UnityEngine.UI.Button levelMagnetButton;
    public TextMeshProUGUI levelMagnetPriceText;
    
    public UnityEngine.UI.Button levelSpeedButton;
    public TextMeshProUGUI levelSpeedPriceText;
    
    public UnityEngine.UI.Button levelShieldButton;
    public TextMeshProUGUI levelShieldPriceText;
    
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
        
        // --- LEVEL MARKET UPDATE ---
        if (levelMagnetButton != null)
        {
            int price = SkillManager.Instance.magnetPrice;
            levelMagnetButton.interactable = SkillManager.Instance.CanBuySkill(SkillType.Magnet);
            if (levelMagnetPriceText != null) levelMagnetPriceText.text = price.ToString();
        }
        
        if (levelSpeedButton != null)
        {
            int price = SkillManager.Instance.speedPrice;
            levelSpeedButton.interactable = SkillManager.Instance.CanBuySkill(SkillType.Speed);
            if (levelSpeedPriceText != null) levelSpeedPriceText.text = price.ToString();
        }
        
        if (levelShieldButton != null)
        {
            int price = SkillManager.Instance.shieldPrice;
            levelShieldButton.interactable = SkillManager.Instance.CanBuySkill(SkillType.Shield);
            if (levelShieldPriceText != null) levelShieldPriceText.text = price.ToString();
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
                // 900000f is our magic logic for "Permanent"
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
                // Permanent check
                if (maxTime > 900000f) 
                {
                    activeSkillTimerText.text = "∞"; // Infinite symbol
                }
                else
                {
                    activeSkillTimerText.text = maxTime.ToString("F1") + "s";
                }
            }
        }
        else
        {
            activeSkillPanel.SetActive(false);
        }
    }

    // Upgrade methods REMOVED

    // --- LEVEL MARKET METHODS ---
    // Assign these to the buttons in the Inspector
    
    public void BuyLevelMagnet()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuyMagnet();
            UpdateSkillUI(); // Update buttons immediately
        }
    }
    
    public void BuyLevelSpeed()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuySpeed();
            UpdateSkillUI();
        }
    }
    
    public void BuyLevelShield()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuyShield();
            UpdateSkillUI();
        }
    }

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
