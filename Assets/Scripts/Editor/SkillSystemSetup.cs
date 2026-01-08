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

        SetupSlot(contentTransform, 0, "Panel_Magnet", "MAGNET", "500", uiManager.BuyMagnet, (btn, txt) => 
        {
            uiManager.magnetButton = btn;
            uiManager.magnetPriceText = txt;
        });

        SetupSlot(contentTransform, 1, "Panel_Speed", "SPEED", "750", uiManager.BuySpeed, (btn, txt) => 
        {
            uiManager.speedButton = btn;
            uiManager.speedPriceText = txt;
        });

        SetupSlot(contentTransform, 2, "Panel_Repellent", "REPELLENT", "1000", uiManager.BuyRepellent, (btn, txt) => 
        {
            uiManager.repellentButton = btn;
            uiManager.repellentPriceText = txt;
        });

        EditorUtility.SetDirty(uiManager);
        Debug.Log("Skill System Setup Complete for Magnet, Speed, and Repellent!");
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

        // Structure: Icon, Text (TMP) [Title], Text (TMP) (1) [Price], Button
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Button button = slot.GetComponentInChildren<Button>(true);

        if (texts.Length >= 1) texts[0].text = title;
        
        TextMeshProUGUI priceText = null;
        if (texts.Length >= 2)
        {
            priceText = texts[1];
            texts[1].text = price;
        }

        if (button != null)
        {
             UnityEventTools.RemovePersistentListener(button.onClick, action);
             UnityEventTools.AddPersistentListener(button.onClick, action);
             Debug.Log($"Wired '{title}' Button.");
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
