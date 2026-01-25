using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.Events; // For wiring buttons
using TMPro;

public class SkillSystemSetup : EditorWindow
{
    [MenuItem("Tools/Setup Skill System")]
    public static void Setup()
    {
        Debug.Log("Starting Skill System Setup...");

        // 1. Setup SkillManager
        SkillManager skillMgr = FindObjectOfType<SkillManager>();
        if (skillMgr == null)
        {
            GameObject mgrObj = new GameObject("SkillManager");
            skillMgr = mgrObj.AddComponent<SkillManager>();
            Debug.Log("Created SkillManager GameObject.");
        }
        else
        {
            Debug.Log("SkillManager already exists.");
        }

        // 2. Find UIManager
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("Setup Failed: Could not find UIManager in the scene!");
            return;
        }

        // 3. Find Market Panel using UIManager reference
        GameObject marketPanel = uiManager.marketPanel;
        if (marketPanel == null)
        {
            // Try to find it by name if reference is missing
            Transform canTransform = uiManager.transform.root; 
            // Or assume it might be a child of a Canvas
            if (uiManager.marketPanel == null)
            {
               // Search recursively? Or just warn.
               // Let's search specifically for "MarketPanel" or "ShopPanel"
               Transform found = FindTransform(canTransform, "MarketPanel");
               if (found == null) found = FindTransform(canTransform, "ShopPanel");
               
               if (found != null) 
               {
                   marketPanel = found.gameObject;
                   uiManager.marketPanel = marketPanel; // Assign it back to UIManager
                   EditorUtility.SetDirty(uiManager);
                   Debug.Log("Found and assigned Market Panel to UIManager.");
               }
            }
        }

        if (marketPanel == null)
        {
             Debug.LogError("Setup Failed: Could not find 'MarketPanel' in UIManager or Scene.");
             return;
        }

        // 4. Find Existing UI Slot (Panel) in Scroll View
        // Hierarchy based on image: MarketPanel -> Store -> Panel -> Scroll View -> Viewport -> Content -> Panel
        Transform contentTransform = FindTransform(marketPanel.transform, "Content");
        if (contentTransform == null)
        {
             Debug.LogError("Setup Failed: Could not find 'Content' in Market Panel hierarchy.");
             return;
        }

        SetupSlot(contentTransform, 0, "Panel_Magnet", "MAGNET", "100", uiManager.UpgradeMagnet, (btn, txt) => 
        {
            uiManager.magnetButton = btn;
            uiManager.magnetPriceText = txt;
        });

        SetupSlot(contentTransform, 1, "Panel_Speed", "SPEED", "100", uiManager.UpgradeSpeed, (btn, txt) => 
        {
            uiManager.speedButton = btn;
            uiManager.speedPriceText = txt;
        });

        SetupSlot(contentTransform, 2, "Panel_Shield", "SHIELD", "100", uiManager.UpgradeShield, (btn, txt) => 
        {
            uiManager.shieldButton = btn;
            uiManager.shieldPriceText = txt;
        });

        EditorUtility.SetDirty(uiManager);
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(uiManager.gameObject.scene);
        }
        Debug.Log("Skill System Setup Complete for Magnet, Speed, and Shield!");
    }

    private static void SetupSlot(Transform content, int index, string name, string title, string price, UnityEngine.Events.UnityAction action, System.Action<Button, TextMeshProUGUI> onAssign)
    {
        if (content.childCount <= index)
        {
            Debug.LogWarning($"Setup Warning: Not enough slots/panels in Content! Expected at least {index + 1}. Skipping {title}.");
            return;
        }

        Transform slot = content.GetChild(index);
        slot.name = name;

        // Structure (User Defined):
        // texts[0] -> Price
        // texts[1] -> Skill Name
        // Button Text -> "BUY"

        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Button button = slot.GetComponentInChildren<Button>(true);

        // 1. Set Price (Index 0)
        TextMeshProUGUI priceText = null;
        if (texts.Length >= 1) 
        {
            Undo.RecordObject(texts[0], "Set Price Text");
            texts[0].text = price + " Gold";
            priceText = texts[0];
            EditorUtility.SetDirty(texts[0]); // Explicitly dirty component
        }

        // 2. Set Skill Name (Index 1)
        if (texts.Length >= 2)
        {
            Undo.RecordObject(texts[1], "Set Skill Name");
            texts[1].text = title;
            EditorUtility.SetDirty(texts[1]);
        }

        if (button != null)
        {
             // 3. Set Button Text to "BUY"
             TextMeshProUGUI btnText = button.GetComponentInChildren<TextMeshProUGUI>(true);
             if (btnText != null)
             {
                 Undo.RecordObject(btnText, "Set Buy Text");
                 btnText.text = "BUY";
                 EditorUtility.SetDirty(btnText);
             }

             Undo.RecordObject(button, "Wire Button");
             UnityEventTools.RemovePersistentListener(button.onClick, action);
             UnityEventTools.AddPersistentListener(button.onClick, action);
             Debug.Log($"Wired '{title}' Button.");
             EditorUtility.SetDirty(button);
        }

        onAssign?.Invoke(button, priceText);
    }

    // Helper to find deep child
    private static Transform FindTransform(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindTransform(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
