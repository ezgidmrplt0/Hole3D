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

        // DEBUG: Check what UIManager thinks is the Price Text
        if (magnetPriceText != null)
            Debug.Log($"UIManager Check: magnetPriceText is assigned to '{magnetPriceText.name}' (Should be 'Text (TMP)')");
        else
            Debug.LogError("UIManager Check: magnetPriceText is NULL!");

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
    public TextMeshProUGUI magnetLevelText;

    public UnityEngine.UI.Button speedButton;
    public TextMeshProUGUI speedPriceText;
    public TextMeshProUGUI speedLevelText;

    public UnityEngine.UI.Button repellentButton;
    public TextMeshProUGUI repellentPriceText;
    public TextMeshProUGUI repellentLevelText;

    private void UpdateSkillUI()
    {
        if (SkillManager.Instance == null) return;

        // Magnet
        if (magnetButton != null)
        {
            int level = SkillManager.Instance.MagnetLevel;
            int price = SkillManager.Instance.GetMagnetUpgradePrice();
            bool isMaxLevel = price < 0;
            
            magnetButton.interactable = !isMaxLevel;
            
            if (magnetPriceText != null)
                magnetPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (magnetLevelText != null)
                magnetLevelText.text = "Lv." + level;
        }

        // Speed
        if (speedButton != null)
        {
            int level = SkillManager.Instance.SpeedLevel;
            int price = SkillManager.Instance.GetSpeedUpgradePrice();
            bool isMaxLevel = price < 0;
            
            speedButton.interactable = !isMaxLevel;
            
            if (speedPriceText != null)
                speedPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (speedLevelText != null)
                speedLevelText.text = "Lv." + level;
        }

        // Repellent
        if (repellentButton != null)
        {
            int level = SkillManager.Instance.RepellentLevel;
            int price = SkillManager.Instance.GetRepellentUpgradePrice();
            bool isMaxLevel = price < 0;
            
            repellentButton.interactable = !isMaxLevel;
            
            if (repellentPriceText != null)
                repellentPriceText.text = isMaxLevel ? "MAX" : price.ToString() + " Gold";
            
            if (repellentLevelText != null)
                repellentLevelText.text = "Lv." + level;
        }
    }

    // Assign to Button OnClick
    public void UpgradeMagnet()
    {
        Debug.Log("UIManager: UpgradeMagnet clicked.");

        if (SkillManager.Instance == null) return;

        if (SkillManager.Instance.UpgradeMagnet())
        {
            UpdateSkillUI();
        }
    }

    public void UpgradeSpeed()
    {
        Debug.Log("UIManager: UpgradeSpeed clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.UpgradeSpeed())
        {
            UpdateSkillUI();
        }
    }

    public void UpgradeRepellent()
    {
        Debug.Log("UIManager: UpgradeRepellent clicked.");
        if (SkillManager.Instance != null && SkillManager.Instance.UpgradeRepellent())
        {
            UpdateSkillUI();
        }
    }

    private void OnEnable()
    {
        UpdateSkillUI();
    }
}
