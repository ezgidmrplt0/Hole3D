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
        // TEST: Give coins if 0
        if (CurrentCoins < 500) 
        {
            CurrentCoins = 1000;
            SaveCoins();
            Debug.Log("EconomyManager: Added 1000 test coins.");
        }
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
        Debug.Log($"Coins Added: {amount}. Total: {CurrentCoins}");
    }

    public bool SpendCoins(int amount)
    {
        if (CurrentCoins >= amount)
        {
            CurrentCoins -= amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(CurrentCoins);
            Debug.Log($"Coins Spent: {amount}. Total: {CurrentCoins}");
            return true;
        }
        Debug.Log("Not enough coins!");
        return false;
    }
}
