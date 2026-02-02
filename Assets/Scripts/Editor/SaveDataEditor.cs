using UnityEngine;
using UnityEditor;

public class SaveDataEditor : EditorWindow
{
    [MenuItem("Tools/Reset All Game Data")]
    public static void ResetGameData()
    {
        // Delete all PlayerPrefs data
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        Debug.Log("Game Data (PlayerPrefs) has been successfully reset! You can now play from scratch.");
    }
    
    [MenuItem("Tools/Set Coins to 0")]
    public static void ResetCoinsOnly()
    {
        PlayerPrefs.SetInt("PlayerCoins", 0);
        PlayerPrefs.Save();
        Debug.Log("Coins reset to 0.");
    }
}
