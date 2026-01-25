using UnityEngine;
using System;
using System.Collections.Generic;

public enum SkillType
{
    Magnet,
    Speed,
    Shield
}

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    // ========== EVENTS ==========
    public event Action<SkillType, float> OnSkillActivated;  // Skill tipi ve süre
    public event Action<SkillType> OnSkillDeactivated;
    public event Action OnUpgradesChanged;

    // ========== UPGRADE LEVELS (Kalıcı - PlayerPrefs) ==========
    private const string MAGNET_UPGRADE_KEY = "Upgrade_Magnet";
    private const string SPEED_UPGRADE_KEY = "Upgrade_Speed";
    private const string SHIELD_UPGRADE_KEY = "Upgrade_Shield";
    
    public const int MAX_UPGRADE_LEVEL = 10;
    
    public int MagnetUpgradeLevel { get; private set; } = 0;
    public int SpeedUpgradeLevel { get; private set; } = 0;
    public int ShieldUpgradeLevel { get; private set; } = 0;

    // ========== ACTIVE SKILL TIMERS ==========
    private Dictionary<SkillType, float> activeSkillTimers = new Dictionary<SkillType, float>();
    
    // ========== BASE SETTINGS ==========
    [Header("Magnet Settings")]
    public float magnetBaseDuration = 8f;
    public float magnetBaseRadius = 3f;
    public float magnetBaseForce = 8f;
    public float magnetRadiusPerLevel = 0.5f;  // Her level +0.5m
    public float magnetForcePerLevel = 2f;     // Her level +2 force

    [Header("Speed Settings")]
    public float speedBaseDuration = 6f;
    public float speedBaseMultiplier = 1.5f;   // %50 hız artışı
    public float speedBonusPerLevel = 0.1f;    // Her level +%10

    [Header("Shield Settings")]
    public float shieldBaseDuration = 10f;
    public float shieldDurationPerLevel = 1f;  // Her level +1 saniye

    [Header("Upgrade Prices")]
    public int baseUpgradePrice = 100;
    public float priceMultiplier = 1.5f;       // Her level fiyat 1.5x artar

    // ========== UNITY LIFECYCLE ==========
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadUpgrades();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Aktif skill'lerin süresini azalt
        UpdateActiveSkills();
    }

    // ========== SKILL ACTIVATION ==========
    public void ActivateSkill(SkillType type)
    {
        float duration = GetSkillDuration(type);
        
        // Eğer zaten aktifse, süreyi uzat (stack)
        if (activeSkillTimers.ContainsKey(type))
        {
            activeSkillTimers[type] += duration;
            Debug.Log($"[SkillManager] {type} süresi uzatıldı. Toplam: {activeSkillTimers[type]:F1}s");
        }
        else
        {
            activeSkillTimers[type] = duration;
            Debug.Log($"[SkillManager] {type} aktif edildi. Süre: {duration:F1}s");
        }
        
        OnSkillActivated?.Invoke(type, activeSkillTimers[type]);
    }

    void UpdateActiveSkills()
    {
        List<SkillType> expiredSkills = new List<SkillType>();
        List<SkillType> keys = new List<SkillType>(activeSkillTimers.Keys);
        
        foreach (var type in keys)
        {
            activeSkillTimers[type] -= Time.deltaTime;
            
            if (activeSkillTimers[type] <= 0)
            {
                expiredSkills.Add(type);
            }
        }
        
        // Süresi biten skill'leri kaldır
        foreach (var type in expiredSkills)
        {
            activeSkillTimers.Remove(type);
            OnSkillDeactivated?.Invoke(type);
            Debug.Log($"[SkillManager] {type} süresi doldu.");
        }
    }

    // ========== SKILL QUERIES ==========
    public bool IsSkillActive(SkillType type)
    {
        return activeSkillTimers.ContainsKey(type) && activeSkillTimers[type] > 0;
    }
    
    public float GetRemainingTime(SkillType type)
    {
        return activeSkillTimers.ContainsKey(type) ? activeSkillTimers[type] : 0f;
    }

    // Kısayollar (Eski kodla uyumluluk için)
    public bool IsMagnetActive => IsSkillActive(SkillType.Magnet);
    public bool IsSpeedActive => IsSkillActive(SkillType.Speed);
    public bool IsShieldActive => IsSkillActive(SkillType.Shield);
    
    // Eski uyumluluk (Repellent kaldırıldı ama referans varsa hata vermesin)
    public bool IsRepellentActive => false;

    // ========== SKILL VALUES (Upgrade'e göre hesaplanır) ==========
    public float GetSkillDuration(SkillType type)
    {
        return type switch
        {
            SkillType.Magnet => magnetBaseDuration,
            SkillType.Speed => speedBaseDuration,
            SkillType.Shield => shieldBaseDuration + (ShieldUpgradeLevel * shieldDurationPerLevel),
            _ => 5f
        };
    }

    public float GetMagnetRadius()
    {
        return magnetBaseRadius + (MagnetUpgradeLevel * magnetRadiusPerLevel);
    }

    public float GetMagnetForce()
    {
        return magnetBaseForce + (MagnetUpgradeLevel * magnetForcePerLevel);
    }

    public float GetSpeedMultiplier()
    {
        return speedBaseMultiplier + (SpeedUpgradeLevel * speedBonusPerLevel);
    }

    public float GetShieldDuration()
    {
        return shieldBaseDuration + (ShieldUpgradeLevel * shieldDurationPerLevel);
    }
    
    // Eski uyumluluk (Repellent kaldırıldı)
    public float GetRepellentRadius() => 0f;
    public float GetRepellentForce() => 0f;

    // ========== UPGRADE SYSTEM ==========
    public int GetUpgradeLevel(SkillType type)
    {
        return type switch
        {
            SkillType.Magnet => MagnetUpgradeLevel,
            SkillType.Speed => SpeedUpgradeLevel,
            SkillType.Shield => ShieldUpgradeLevel,
            _ => 0
        };
    }

    public int GetUpgradePrice(SkillType type)
    {
        int level = GetUpgradeLevel(type);
        if (level >= MAX_UPGRADE_LEVEL) return -1; // Max level
        
        return Mathf.RoundToInt(baseUpgradePrice * Mathf.Pow(priceMultiplier, level));
    }

    public bool CanUpgrade(SkillType type)
    {
        int price = GetUpgradePrice(type);
        if (price < 0) return false; // Max level
        
        return EconomyManager.Instance != null && EconomyManager.Instance.CurrentCoins >= price;
    }

    public bool TryUpgrade(SkillType type)
    {
        int price = GetUpgradePrice(type);
        if (price < 0)
        {
            Debug.Log($"[SkillManager] {type} zaten max level!");
            return false;
        }
        
        if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendCoins(price))
        {
            Debug.Log($"[SkillManager] Yetersiz altın! Gerekli: {price}");
            return false;
        }
        
        // Upgrade başarılı
        switch (type)
        {
            case SkillType.Magnet:
                MagnetUpgradeLevel++;
                break;
            case SkillType.Speed:
                SpeedUpgradeLevel++;
                break;
            case SkillType.Shield:
                ShieldUpgradeLevel++;
                break;
        }
        
        SaveUpgrades();
        OnUpgradesChanged?.Invoke();
        
        Debug.Log($"[SkillManager] {type} upgraded to level {GetUpgradeLevel(type)}!");
        return true;
    }

    // ========== PERSISTENCE ==========
    void LoadUpgrades()
    {
        MagnetUpgradeLevel = PlayerPrefs.GetInt(MAGNET_UPGRADE_KEY, 0);
        SpeedUpgradeLevel = PlayerPrefs.GetInt(SPEED_UPGRADE_KEY, 0);
        ShieldUpgradeLevel = PlayerPrefs.GetInt(SHIELD_UPGRADE_KEY, 0);
        
        Debug.Log($"[SkillManager] Upgrades loaded: Magnet={MagnetUpgradeLevel}, Speed={SpeedUpgradeLevel}, Shield={ShieldUpgradeLevel}");
    }

    void SaveUpgrades()
    {
        PlayerPrefs.SetInt(MAGNET_UPGRADE_KEY, MagnetUpgradeLevel);
        PlayerPrefs.SetInt(SPEED_UPGRADE_KEY, SpeedUpgradeLevel);
        PlayerPrefs.SetInt(SHIELD_UPGRADE_KEY, ShieldUpgradeLevel);
        PlayerPrefs.Save();
    }

    // ========== LEVEL RESET (Her level başında - Aktif skill'ler sıfırlanır) ==========
    public void ResetSkills()
    {
        // Sadece aktif skill'leri sıfırla, upgrade'ler kalıcı
        foreach (var type in new List<SkillType>(activeSkillTimers.Keys))
        {
            OnSkillDeactivated?.Invoke(type);
        }
        activeSkillTimers.Clear();
        Debug.Log("[SkillManager] Active skills reset for new level.");
    }

    // ========== DEBUG ==========
    [ContextMenu("Reset All Upgrades")]
    public void ResetAllUpgrades()
    {
        MagnetUpgradeLevel = 0;
        SpeedUpgradeLevel = 0;
        ShieldUpgradeLevel = 0;
        SaveUpgrades();
        OnUpgradesChanged?.Invoke();
        Debug.Log("[SkillManager] All upgrades reset to 0.");
    }

    [ContextMenu("Max All Upgrades")]
    public void MaxAllUpgrades()
    {
        MagnetUpgradeLevel = MAX_UPGRADE_LEVEL;
        SpeedUpgradeLevel = MAX_UPGRADE_LEVEL;
        ShieldUpgradeLevel = MAX_UPGRADE_LEVEL;
        SaveUpgrades();
        OnUpgradesChanged?.Invoke();
        Debug.Log("[SkillManager] All upgrades set to MAX.");
    }
    
    [ContextMenu("Test Activate Magnet")]
    public void TestActivateMagnet() => ActivateSkill(SkillType.Magnet);
    
    [ContextMenu("Test Activate Speed")]
    public void TestActivateSpeed() => ActivateSkill(SkillType.Speed);
    
    [ContextMenu("Test Activate Shield")]
    public void TestActivateShield() => ActivateSkill(SkillType.Shield);
}
