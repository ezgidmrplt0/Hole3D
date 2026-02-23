using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    public int CurrentCoins { get; private set; }

    public event Action<int> OnCoinsChanged;

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
        CurrentCoins = PlayerPrefs.GetInt(COIN_PREF_KEY, 0);
    }

    private void SaveCoins()
    {
        PlayerPrefs.SetInt(COIN_PREF_KEY, CurrentCoins);
        PlayerPrefs.Save();
    }

    public void AddCoins(int amount)
    {
        CurrentCoins += amount;
        SaveCoins();
        OnCoinsChanged?.Invoke(CurrentCoins);
    }

    public bool SpendCoins(int amount)
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