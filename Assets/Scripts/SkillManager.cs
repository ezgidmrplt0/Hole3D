using UnityEngine;
using System;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    private const string MAGNET_UNLOCKED_KEY = "Skill_Magnet_Unlocked";

    public bool IsMagnetUnlocked { get; private set; }
    
    // settings
    public int magnetPrice = 500;

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

    // Speed Skill
    private const string SPEED_UNLOCKED_KEY = "Skill_Speed_Unlocked";
    public bool IsSpeedUnlocked { get; private set; }
    public int speedPrice = 750;

    // Repellent Skill
    private const string REPELLENT_UNLOCKED_KEY = "Skill_Repellent_Unlocked";
    public bool IsRepellentUnlocked { get; private set; }
    public int repellentPrice = 1000;

    private void LoadSkills()
    {
        IsMagnetUnlocked = PlayerPrefs.GetInt(MAGNET_UNLOCKED_KEY, 0) == 1;
        IsSpeedUnlocked = PlayerPrefs.GetInt(SPEED_UNLOCKED_KEY, 0) == 1;
        IsRepellentUnlocked = PlayerPrefs.GetInt(REPELLENT_UNLOCKED_KEY, 0) == 1;
    }

    public bool UnlockMagnet()
    {
        if (IsMagnetUnlocked) 
        {
            Debug.Log("SkillManager: Already unlocked.");
            return true; 
        }

        if (TrySpend(magnetPrice))
        {
            IsMagnetUnlocked = true;
            PlayerPrefs.SetInt(MAGNET_UNLOCKED_KEY, 1);
            SaveAndNotify("Magnet");
            return true;
        }
        return false;
    }

    public bool UnlockSpeed()
    {
        if (IsSpeedUnlocked) return true;
        if (TrySpend(speedPrice))
        {
            IsSpeedUnlocked = true;
            PlayerPrefs.SetInt(SPEED_UNLOCKED_KEY, 1);
            SaveAndNotify("Speed");
            return true;
        }
        return false;
    }

    public bool UnlockRepellent()
    {
        if (IsRepellentUnlocked) return true;
        if (TrySpend(repellentPrice))
        {
            IsRepellentUnlocked = true;
            PlayerPrefs.SetInt(REPELLENT_UNLOCKED_KEY, 1);
            SaveAndNotify("Repellent");
            return true;
        }
        return false;
    }

    private bool TrySpend(int amount)
    {
        if (EconomyManager.Instance == null)
        {
            Debug.LogError("SkillManager: EconomyManager missing!");
            return false;
        }
        return EconomyManager.Instance.SpendCoins(amount);
    }

    private void SaveAndNotify(string skillName)
    {
        PlayerPrefs.Save();
        OnSkillsChanged?.Invoke();
        Debug.Log($"SkillManager: {skillName} Unlocked Successfully!");
    }
}
