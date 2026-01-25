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

    // Base Prices (Level 1 Base)
    [Header("Base Prices")]
    public int magnetBasePrice = 100;
    public int speedBasePrice = 100;
    public int repellentBasePrice = 100;

    // Price multiplier per level (her level'da fiyat artar)
    [Header("Price Scaling")]
    public float priceMultiplier = 1.5f; // Unused but kept for structure or remove if strict

    // ========== SKILL EFFECT VALUES ==========
    // Magnet: Zombileri çekme
    [Header("Magnet Skill Settings")]
    public float magnetBaseRadius = 2f;       
    public float magnetBaseForce = 5f;        

    // Speed: Hareket hızı bonusu
    [Header("Speed Skill Settings")]
    public float speedBonusPerLevel = 0.5f; // %50 Hız artsın (Tek seferlik olduğu için güçlü olsun)

    // Repellent: İnsanları itme
    [Header("Repellent Skill Settings")]
    public float repellentBaseRadius = 1.5f;     
    public float repellentBaseForce = 10f;       

    public event Action OnSkillsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // No loading from PlayerPrefs (One-time use per level)
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ========== PRICE CALCULATIONS ==========
    // Fiyat Oyun Leveline göre değişir: Base * 2^(Level-1)
    private int CalculatePrice(int basePrice)
    {
        int gameLevelIndex = 0;
        if (LevelManager.Instance != null)
        {
            gameLevelIndex = LevelManager.Instance.currentLevelIndex;
        }
        
        // Formül: Her levelde %100 artış (2 katına çıkma)
        // Level 1 (Index 0): Base * 1
        // Level 2 (Index 1): Base * 2
        // Level 3 (Index 2): Base * 4
        
        float multiplier = Mathf.Pow(2, gameLevelIndex);
        return Mathf.RoundToInt(basePrice * multiplier);
    }

    public int GetMagnetUpgradePrice() => CalculatePrice(magnetBasePrice);
    public int GetSpeedUpgradePrice() => CalculatePrice(speedBasePrice);
    public int GetRepellentUpgradePrice() => CalculatePrice(repellentBasePrice);

    // ========== UPGRADE METHODS ==========
    // Artık "Upgrade" aslında "Satın Al / Aktif Et" demek (Tek seferlik)
    
    public bool UpgradeMagnet()
    {
        if (MagnetLevel >= 1) // Zaten aktifse alma (Level sistemi kalktı, sadece 0 veya 1)
        {
            Debug.Log("SkillManager: Magnet already active for this level!");
            return false;
        }

        int price = GetMagnetUpgradePrice();
        if (TrySpend(price))
        {
            MagnetLevel = 1; // Aktif et
            NotifyOnly("Magnet");
            return true;
        }
        return false;
    }

    public bool UpgradeSpeed()
    {
        if (SpeedLevel >= 1)
        {
            Debug.Log("SkillManager: Speed already active for this level!");
            return false;
        }

        int price = GetSpeedUpgradePrice();
        if (TrySpend(price))
        {
            SpeedLevel = 1;
            NotifyOnly("Speed");
            return true;
        }
        return false;
    }

    public bool UpgradeRepellent()
    {
        if (RepellentLevel >= 1)
        {
            Debug.Log("SkillManager: Repellent already active for this level!");
            return false;
        }

        int price = GetRepellentUpgradePrice();
        if (TrySpend(price))
        {
            RepellentLevel = 1;
            NotifyOnly("Repellent");
            return true;
        }
        return false;
    }
    
    // ========== LEVEL MANAGEMENT ==========
    // LevelManager tarafından her yeni level başlangıcında çağrılmalı
    public void ResetSkills()
    {
        MagnetLevel = 0;
        SpeedLevel = 0;
        RepellentLevel = 0;
        OnSkillsChanged?.Invoke();
        Debug.Log("SkillManager: Skills reset for new level.");
    }

    // ========== EFFECT GETTERS (Simplified) ==========
    public bool IsMagnetActive => MagnetLevel > 0;
    public bool IsSpeedActive => SpeedLevel > 0;
    public bool IsRepellentActive => RepellentLevel > 0;

    // Değerler artık sabit (Level sistemi kalktığı için tek bir güçlü değer dönelim veya Base dönelim)
    // Level sistemini "active" olduğu sürece "Level 1" gibi varsayıyoruz.
    public float GetMagnetRadius() => IsMagnetActive ? magnetBaseRadius : 0f;
    public float GetMagnetForce() => IsMagnetActive ? magnetBaseForce : 0f;
    public float GetSpeedMultiplier() => IsSpeedActive ? 1f + speedBonusPerLevel : 1f; // %5 bonus (Yükseltilebilir)
    public float GetRepellentRadius() => IsRepellentActive ? repellentBaseRadius : 0f;
    public float GetRepellentForce() => IsRepellentActive ? repellentBaseForce : 0f;


    // ========== HELPER METHODS ==========
    private bool TrySpend(int amount)
    {
        if (EconomyManager.Instance == null)
        {
             // Debug amaçlı: Economy yoksa al
            Debug.LogWarning("SkillManager: EconomyManager missing! Bypassing payment.");
            return true; 
        }
        return EconomyManager.Instance.SpendCoins(amount);
    }

    private void NotifyOnly(string skillName)
    {
        // Save yok, sadece notify
        OnSkillsChanged?.Invoke();
        Debug.Log($"SkillManager: {skillName} activated for this level!");
    }

    // ========== DEBUG ==========
    [ContextMenu("Reset All Skills")]
    public void ResetAllSkills() => ResetSkills();
}
