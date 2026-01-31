using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

public class LevelMarketSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup Level Market UI")]
    public static void SetupLevelMarket()
    {
        // 1. UIManager'ı bul
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("Sahne'de UIManager bulunamadı!");
            return;
        }

        // 2. TapToPanel/Skills altındaki butonları bul
        GameObject skillsPanel = GameObject.Find("Skills"); // İsimden bulmaya çalış (Hiyerarşide Canvas/TapToPanel/Skills)
        
        if (skillsPanel == null)
        {
            // Tam yol deneyelim
            GameObject tapPanel = GameObject.Find("TapToPanel");
            if (tapPanel != null)
            {
                var t = tapPanel.transform.Find("Skills");
                if (t != null) skillsPanel = t.gameObject;
            }
        }

        if (skillsPanel == null)
        {
            Debug.LogError("'Skills' paneli bulunamadı! Lütfen hiyerarşide 'Skills' adında bir obje olduğundan emin olun.");
            return;
        }

        // 3. Butonları ve Textleri Eşleştir
        Undo.RecordObject(uiManager, "Setup Level Market UI");

        // --- MAGNET ---
        Transform tMagnet = skillsPanel.transform.Find("Magnet");
        if (tMagnet != null)
        {
            uiManager.levelMagnetButton = tMagnet.GetComponent<Button>();
            if (uiManager.levelMagnetButton == null) uiManager.levelMagnetButton = tMagnet.GetComponentInChildren<Button>();
            
            uiManager.levelMagnetPriceText = tMagnet.GetComponentInChildren<TextMeshProUGUI>();
            
            // OnClick Setup
            if (uiManager.levelMagnetButton != null)
            {
                SetupButtonEvent(uiManager.levelMagnetButton, uiManager, "BuyLevelMagnet");
            }
            Debug.Log("Magnet UI eşleştirildi.");
        }
        else Debug.LogError("Skills altında 'Magnet' objesi bulunamadı.");

        // --- SPEED ---
        Transform tSpeed = skillsPanel.transform.Find("Speed");
        if (tSpeed != null)
        {
            uiManager.levelSpeedButton = tSpeed.GetComponent<Button>();
            if (uiManager.levelSpeedButton == null) uiManager.levelSpeedButton = tSpeed.GetComponentInChildren<Button>();
            
            uiManager.levelSpeedPriceText = tSpeed.GetComponentInChildren<TextMeshProUGUI>();
            
            // OnClick Setup
            if (uiManager.levelSpeedButton != null)
            {
                SetupButtonEvent(uiManager.levelSpeedButton, uiManager, "BuyLevelSpeed");
            }
            Debug.Log("Speed UI eşleştirildi.");
        }
        else Debug.LogError("Skills altında 'Speed' objesi bulunamadı.");

        // --- SHIELD ---
        Transform tShield = skillsPanel.transform.Find("Shield");
        if (tShield != null)
        {
            uiManager.levelShieldButton = tShield.GetComponent<Button>();
            if (uiManager.levelShieldButton == null) uiManager.levelShieldButton = tShield.GetComponentInChildren<Button>();
            
            uiManager.levelShieldPriceText = tShield.GetComponentInChildren<TextMeshProUGUI>();
            
            // OnClick Setup
            if (uiManager.levelShieldButton != null)
            {
                SetupButtonEvent(uiManager.levelShieldButton, uiManager, "BuyLevelShield");
            }
            Debug.Log("Shield UI eşleştirildi.");
        }
        else Debug.LogError("Skills altında 'Shield' objesi bulunamadı.");

        // Değişiklikleri kaydet
        EditorUtility.SetDirty(uiManager);
        Debug.Log("<color=green>Level Market UI kurulumu tamamlandı!</color>");
    }

    private static void SetupButtonEvent(Button btn, Object target, string methodName)
    {
        Undo.RecordObject(btn, "Setup Button Event");
        
        // Remove existing listeners that match target and method
        int count = btn.onClick.GetPersistentEventCount();
        for (int i = count - 1; i >= 0; i--)
        {
            if (btn.onClick.GetPersistentTarget(i) == target && 
                btn.onClick.GetPersistentMethodName(i) == methodName)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
            }
        }
        
        // Add new listener
        var methodInfo = typeof(UIManager).GetMethod(methodName);
        if (methodInfo != null)
        {
            UnityEngine.Events.UnityAction action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, methodInfo) as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }
    }
}
