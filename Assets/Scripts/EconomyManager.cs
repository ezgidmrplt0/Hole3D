using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    public float CurrentCoins { get; private set; }

    public event Action<float> OnCoinsChanged;

    private const string COIN_PREF_KEY = "PlayerCoins";

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            LoadCoins();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Broadcast initial state
        OnCoinsChanged?.Invoke(CurrentCoins);
    }

    private void LoadCoins()
    {
        CurrentCoins = PlayerPrefs.GetFloat(COIN_PREF_KEY, 0f);
    }

    private void SaveCoins()
    {
        PlayerPrefs.SetFloat(COIN_PREF_KEY, CurrentCoins);
        PlayerPrefs.Save();
    }

    public void AddCoins(float amount)
    {
        CurrentCoins += amount;
        SaveCoins();
        OnCoinsChanged?.Invoke(CurrentCoins);
    }

    public bool SpendCoins(float amount)
    {
        if (CurrentCoins >= amount)
        {
            CurrentCoins -= amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(CurrentCoins);
            return true;
        }

#if UNITY_EDITOR
        Debug.Log("Not enough coins!");
#endif
        return false;
    }
}