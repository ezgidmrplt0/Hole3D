using UnityEngine;
using TMPro; // Standard for text in modern Unity

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI levelText;

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
            // Force update initial value (add 1 because index is 0-based)
            UpdateLevelText(LevelManager.Instance.currentLevelIndex + 1);
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

    [Header("Skill UI")]
    public UnityEngine.UI.Button magnetButton;
    public TextMeshProUGUI magnetPriceText;

    public UnityEngine.UI.Button speedButton;
    public TextMeshProUGUI speedPriceText;

    public UnityEngine.UI.Button repellentButton;
    public TextMeshProUGUI repellentPriceText;

    private void UpdateSkillUI()
    {
        if (SkillManager.Instance == null) return;

        // Magnet
        if (magnetButton != null)
        {
            bool unlocked = SkillManager.Instance.IsMagnetUnlocked;
            magnetButton.interactable = !unlocked; 
            
            if (magnetPriceText != null)
                magnetPriceText.text = unlocked ? "OWNED" : SkillManager.Instance.magnetPrice.ToString();
        }

        // Speed
        if (speedButton != null)
        {
            bool unlocked = SkillManager.Instance.IsSpeedUnlocked;
            speedButton.interactable = !unlocked; 
            
            if (speedPriceText != null)
                speedPriceText.text = unlocked ? "OWNED" : SkillManager.Instance.speedPrice.ToString();
        }

        // Repellent
        if (repellentButton != null)
        {
            bool unlocked = SkillManager.Instance.IsRepellentUnlocked;
            repellentButton.interactable = !unlocked; 
            
            if (repellentPriceText != null)
                repellentPriceText.text = unlocked ? "OWNED" : SkillManager.Instance.repellentPrice.ToString();
        }
    }

    // Assign to Button OnClick
    public void BuyMagnet()
    {
        Debug.Log("UIManager: BuyMagnet clicked.");

        if (SkillManager.Instance == null) return;

        if (SkillManager.Instance.UnlockMagnet())
        {
            UpdateSkillUI();
        }
    }

    public void BuySpeed()
    {
        Debug.Log("UIManager: BuySpeed clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.UnlockSpeed())
        {
            UpdateSkillUI();
        }
    }

    public void BuyRepellent()
    {
        Debug.Log("UIManager: BuyRepellent clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.UnlockRepellent())
        {
            UpdateSkillUI();
        }
    }

    private void OnEnable()
    {
        UpdateSkillUI();
    }
}
