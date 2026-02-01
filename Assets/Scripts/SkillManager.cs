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
    public event Action OnUpgradesChanged; // Kept for UI updates

    // ========== ACTIVE SKILL TIMERS ==========
    private Dictionary<SkillType, float> activeSkillTimers = new Dictionary<SkillType, float>();
    
    // ========== BASE SETTINGS ==========
    [Header("Magnet Settings")]
    public float magnetBaseDuration = 8f;
    public float magnetBaseRadius = 6f;     // Artırıldı (3 -> 6)
    public float magnetBaseForce = 25f;     // Artırıldı (8 -> 25)

    [Header("Speed Settings")]
    public float speedBaseDuration = 6f;
    public float speedBaseMultiplier = 1.5f;   // %50 hız artışı

    [Header("Shield Settings")]
    public float shieldBaseDuration = 10f;

    // ========== UNITY LIFECYCLE ==========
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
    // ========== SKILL ACTIVATION ==========
    public void ActivateSkill(SkillType type, bool permanent = false)
    {
        // 1. Permanent Check
        if (permanent)
        {
            // If already permanent, do nothing or log ?
            // Just set duration to infinite (using a magic number like 9999 or separate flag)
            // But let's use the dictionary with a very high value
            activeSkillTimers[type] = 999999f;
            Debug.Log($"[SkillManager] {type} PERMANENTLY activated for this level!");
        }
        else
        {
            // Normal activation (pickup) - but pickups are disabled now?
            // User said "skiller level bitene kadar bitmeyecek" (skills wont end until level ends)
            // So seemingly ALL activations should be permanent?
            // But just in case, let's keep the parameter but default to permanent if bought.
            
            float duration = GetSkillDuration(type);
            if (activeSkillTimers.ContainsKey(type))
            {
                 // If already infinite, don't overwrite with short duration
                 if (activeSkillTimers[type] > 900000f) return;
                 
                 activeSkillTimers[type] += duration;
            }
            else
            {
                activeSkillTimers[type] = duration;
            }
        }
        
        OnSkillActivated?.Invoke(type, activeSkillTimers[type]);
    }

    void UpdateActiveSkills()
    {
        // Only count down if not "infinite"
        List<SkillType> keys = new List<SkillType>(activeSkillTimers.Keys);
        
        foreach (var type in keys)
        {
            float val = activeSkillTimers[type];
            if (val > 900000f) continue; // Infinite / Permanent
            
            activeSkillTimers[type] -= Time.deltaTime;
            
            if (activeSkillTimers[type] <= 0)
            {
                activeSkillTimers.Remove(type);
                OnSkillDeactivated?.Invoke(type);
            }
        }
    }

    // ========== HELPER PROPERTIES / METHODS ==========
    public bool IsSkillActive(SkillType type)
    {
        return activeSkillTimers.ContainsKey(type) && activeSkillTimers[type] > 0;
    }
    
    public float GetRemainingTime(SkillType type)
    {
        return activeSkillTimers.ContainsKey(type) ? activeSkillTimers[type] : 0f;
    }

    public bool IsMagnetActive => IsSkillActive(SkillType.Magnet);
    public bool IsSpeedActive => IsSkillActive(SkillType.Speed);
    public bool IsShieldActive => IsSkillActive(SkillType.Shield);
    public bool IsRepellentActive => false; // Compatibility

    // ========== SKILL VALUES ==========
    public float GetSkillDuration(SkillType type)
    {
        return type switch
        {
            SkillType.Magnet => magnetBaseDuration,
            SkillType.Speed => speedBaseDuration,
            SkillType.Shield => shieldBaseDuration,
            _ => 5f
        };
    }

    public float GetMagnetRadius() => magnetBaseRadius; // Fixed values, no upgrades
    public float GetMagnetForce() => magnetBaseForce;
    public float GetSpeedMultiplier() => speedBaseMultiplier;
    public float GetShieldDuration() => shieldBaseDuration;
    
    // Eski uyumluluk
    public float GetRepellentRadius() => 0f;
    public float GetRepellentForce() => 0f;
    public int GetUpgradeLevel(SkillType type) => 1; // Always 1
    public int GetUpgradePrice(SkillType type) => 0; // No upgrades
    public bool CanUpgrade(SkillType type) => false;
    public bool TryUpgrade(SkillType type) => false;

    // ========== LEVEL MARKET (Tek kullanımlık -> Kalıcı) ==========
    [Header("Level Market Prices")]
    public int magnetPrice = 50;
    public int speedPrice = 40;
    public int shieldPrice = 60;
    
    // Satın alma sayaçları (her level sıfırlanır)
    private bool magnetPurchased = false;
    private bool speedPurchased = false;
    private bool shieldPurchased = false;

    public void ResetLevelPurchases()
    {
        magnetPurchased = false;
        speedPurchased = false;
        shieldPurchased = false;
        ResetSkills();
    }

    public void ResetSkills()
    {
        // Sadece aktif skill'leri sıfırla
        foreach (var type in new List<SkillType>(activeSkillTimers.Keys))
        {
            OnSkillDeactivated?.Invoke(type);
        }
        activeSkillTimers.Clear();
        Debug.Log("[SkillManager] Active skills reset for new level.");
    }

    public void BuyMagnet()
    {
        if (magnetPurchased) return;
        if (TryPurchaseSkill(magnetPrice))
        {
            magnetPurchased = true;
            ActivateSkill(SkillType.Magnet, true); // Permanent
            OnUpgradesChanged?.Invoke();
        }
    }
    
    public void BuySpeed()
    {
        if (speedPurchased) return;
        if (TryPurchaseSkill(speedPrice))
        {
            speedPurchased = true;
            ActivateSkill(SkillType.Speed, true); // Permanent
            OnUpgradesChanged?.Invoke();
        }
    }
    
    public void BuyShield()
    {
        if (shieldPurchased) return;
        if (TryPurchaseSkill(shieldPrice))
        {
            shieldPurchased = true;
            ActivateSkill(SkillType.Shield, true); // Permanent
            OnUpgradesChanged?.Invoke();
        }
    }
    
    private bool TryPurchaseSkill(int price)
    {
        if (EconomyManager.Instance != null && EconomyManager.Instance.CurrentCoins >= price)
        {
            EconomyManager.Instance.SpendCoins(price);
            return true;
        }
        return false;
    }

    public bool CanBuySkill(SkillType type)
    {
        bool purchased = type switch {
            SkillType.Magnet => magnetPurchased,
            SkillType.Speed => speedPurchased,
            SkillType.Shield => shieldPurchased,
            _ => true
        };
        
        if (purchased) return false;

        int price = type switch
        {
            SkillType.Magnet => magnetPrice,
            SkillType.Speed => speedPrice,
            SkillType.Shield => shieldPrice,
            _ => 999999
        };
        
        return EconomyManager.Instance != null && EconomyManager.Instance.CurrentCoins >= price;
    }
}
