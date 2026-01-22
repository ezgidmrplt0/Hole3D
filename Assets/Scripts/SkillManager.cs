using UnityEngine;
using System;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    // PlayerPrefs Keys
    private const string MAGNET_LEVEL_KEY = "Skill_Magnet_Level";
    private const string SPEED_LEVEL_KEY = "Skill_Speed_Level";
    private const string REPELLENT_LEVEL_KEY = "Skill_Repellent_Level";

    // Skill Levels (0 = not unlocked, 1+ = active levels)
    public int MagnetLevel { get; private set; } = 0;
    public int SpeedLevel { get; private set; } = 0;
    public int RepellentLevel { get; private set; } = 0;

    // Max Level
    public const int MAX_SKILL_LEVEL = 10;

    // Base Prices (Level 0 -> 1)
    [Header("Base Prices")]
    public int magnetBasePrice = 100;
    public int speedBasePrice = 100;
    public int repellentBasePrice = 100;

    // Price multiplier per level (her level'da fiyat artar)
    [Header("Price Scaling")]
    public float priceMultiplier = 1.5f;

    // ========== SKILL EFFECT VALUES ==========
    // Magnet: Zombileri çekme
    [Header("Magnet Skill Settings")]
    public float magnetBaseRadius = 2f;       // Level 1'de başlangıç
    public float magnetRadiusPerLevel = 0.8f; // Her level +0.8 radius
    public float magnetBaseForce = 5f;        // Level 1'de başlangıç
    public float magnetForcePerLevel = 5f;    // Her level +5 force

    // Speed: Hareket hızı bonusu
    [Header("Speed Skill Settings")]
    public float speedBonusPerLevel = 0.05f;  // Her level %5 bonus (Level 1 = %5, Level 10 = %50)

    // Repellent: İnsanları itme
    [Header("Repellent Skill Settings")]
    public float repellentBaseRadius = 1.5f;     // Level 1'de başlangıç
    public float repellentRadiusPerLevel = 0.5f; // Her level +0.5 radius
    public float repellentBaseForce = 10f;       // Level 1'de başlangıç
    public float repellentForcePerLevel = 8f;    // Her level +8 force

    public event Action OnSkillsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadSkills();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadSkills()
    {
        MagnetLevel = PlayerPrefs.GetInt(MAGNET_LEVEL_KEY, 0);
        SpeedLevel = PlayerPrefs.GetInt(SPEED_LEVEL_KEY, 0);
        RepellentLevel = PlayerPrefs.GetInt(REPELLENT_LEVEL_KEY, 0);
    }

    private void SaveSkills()
    {
        PlayerPrefs.SetInt(MAGNET_LEVEL_KEY, MagnetLevel);
        PlayerPrefs.SetInt(SPEED_LEVEL_KEY, SpeedLevel);
        PlayerPrefs.SetInt(REPELLENT_LEVEL_KEY, RepellentLevel);
        PlayerPrefs.Save();
    }

    // ========== PRICE CALCULATIONS ==========
    public int GetMagnetUpgradePrice()
    {
        if (MagnetLevel >= MAX_SKILL_LEVEL) return -1; // Max level
        return Mathf.RoundToInt(magnetBasePrice * Mathf.Pow(priceMultiplier, MagnetLevel));
    }

    public int GetSpeedUpgradePrice()
    {
        if (SpeedLevel >= MAX_SKILL_LEVEL) return -1;
        return Mathf.RoundToInt(speedBasePrice * Mathf.Pow(priceMultiplier, SpeedLevel));
    }

    public int GetRepellentUpgradePrice()
    {
        if (RepellentLevel >= MAX_SKILL_LEVEL) return -1;
        return Mathf.RoundToInt(repellentBasePrice * Mathf.Pow(priceMultiplier, RepellentLevel));
    }

    // ========== UPGRADE METHODS ==========
    public bool UpgradeMagnet()
    {
        if (MagnetLevel >= MAX_SKILL_LEVEL)
        {
            Debug.Log("SkillManager: Magnet already at max level!");
            return false;
        }

        int price = GetMagnetUpgradePrice();
        if (TrySpend(price))
        {
            MagnetLevel++;
            SaveAndNotify("Magnet", MagnetLevel);
            return true;
        }
        return false;
    }

    public bool UpgradeSpeed()
    {
        if (SpeedLevel >= MAX_SKILL_LEVEL)
        {
            Debug.Log("SkillManager: Speed already at max level!");
            return false;
        }

        int price = GetSpeedUpgradePrice();
        if (TrySpend(price))
        {
            SpeedLevel++;
            SaveAndNotify("Speed", SpeedLevel);
            return true;
        }
        return false;
    }

    public bool UpgradeRepellent()
    {
        if (RepellentLevel >= MAX_SKILL_LEVEL)
        {
            Debug.Log("SkillManager: Repellent already at max level!");
            return false;
        }

        int price = GetRepellentUpgradePrice();
        if (TrySpend(price))
        {
            RepellentLevel++;
            SaveAndNotify("Repellent", RepellentLevel);
            return true;
        }
        return false;
    }

    // ========== EFFECT GETTERS ==========
    /// <summary>
    /// Magnet aktif mi? (Level > 0)
    /// </summary>
    public bool IsMagnetActive => MagnetLevel > 0;

    /// <summary>
    /// Speed aktif mi? (Level > 0)
    /// </summary>
    public bool IsSpeedActive => SpeedLevel > 0;

    /// <summary>
    /// Repellent aktif mi? (Level > 0)
    /// </summary>
    public bool IsRepellentActive => RepellentLevel > 0;

    /// <summary>
    /// Mevcut Magnet çekim yarıçapı
    /// </summary>
    public float GetMagnetRadius()
    {
        if (MagnetLevel <= 0) return 0f;
        return magnetBaseRadius + (magnetRadiusPerLevel * (MagnetLevel - 1));
    }

    /// <summary>
    /// Mevcut Magnet çekim kuvveti
    /// </summary>
    public float GetMagnetForce()
    {
        if (MagnetLevel <= 0) return 0f;
        return magnetBaseForce + (magnetForcePerLevel * (MagnetLevel - 1));
    }

    /// <summary>
    /// Mevcut Speed bonus çarpanı (1.0 = normal, 1.05 = %5 bonus)
    /// </summary>
    public float GetSpeedMultiplier()
    {
        if (SpeedLevel <= 0) return 1f;
        return 1f + (speedBonusPerLevel * SpeedLevel);
    }

    /// <summary>
    /// Mevcut Repellent itme yarıçapı
    /// </summary>
    public float GetRepellentRadius()
    {
        if (RepellentLevel <= 0) return 0f;
        return repellentBaseRadius + (repellentRadiusPerLevel * (RepellentLevel - 1));
    }

    /// <summary>
    /// Mevcut Repellent itme kuvveti
    /// </summary>
    public float GetRepellentForce()
    {
        if (RepellentLevel <= 0) return 0f;
        return repellentBaseForce + (repellentForcePerLevel * (RepellentLevel - 1));
    }

    // ========== HELPER METHODS ==========
    private bool TrySpend(int amount)
    {
        if (EconomyManager.Instance == null)
        {
            Debug.LogError("SkillManager: EconomyManager missing!");
            return false;
        }
        return EconomyManager.Instance.SpendCoins(amount);
    }

    private void SaveAndNotify(string skillName, int newLevel)
    {
        SaveSkills();
        OnSkillsChanged?.Invoke();
        Debug.Log($"SkillManager: {skillName} upgraded to Level {newLevel}!");
    }

    // ========== DEBUG ==========
    [ContextMenu("Reset All Skills")]
    public void ResetAllSkills()
    {
        MagnetLevel = 0;
        SpeedLevel = 0;
        RepellentLevel = 0;
        SaveSkills();
        OnSkillsChanged?.Invoke();
        Debug.Log("SkillManager: All skills reset to Level 0");
    }
}
