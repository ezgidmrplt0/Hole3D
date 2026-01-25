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

    private void UpdateHumanCounter(int count)
    {
        if (humanCounterText != null)
        {
            // Kullanıcı isteği: 5'ten geriye saysın (Kalan Hak)
            // Bunun için LevelManager'dan Max Limit'i bilmemiz lazım.
            // İdeal yol: LevelManager'dan count değişimiyle birlikte bu veriyi almak veya doğrudan erişmek.
            
            int remaining = 0;
            if (LevelManager.Instance != null)
            {
                remaining = LevelManager.Instance.maxHumanLimit - count;
            }
            
            if (remaining < 0) remaining = 0;
            
            humanCounterText.text = remaining.ToString();
        }
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
