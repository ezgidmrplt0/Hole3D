using UnityEngine;
using UnityEditor;

public class HierarchyUIFixer : EditorWindow
{
    [MenuItem("Tools/Fix UI Hierarchy")]
    public static void FixHierarchy()
    {
        // 1. Find the Skills Object
        GameObject skillsObj = GameObject.Find("Skills");
        if (skillsObj == null)
        {
            // Try explicit path if name search fails
            GameObject tapPanel = GameObject.Find("TapToPanel");
            if (tapPanel != null)
            {
                var t = tapPanel.transform.Find("Skills");
                if (t != null) skillsObj = t.gameObject;
            }
        }

        if (skillsObj == null)
        {
            Debug.LogError("Skills panel could not be found! Is it already moved?");
            // Optionally try to find "LevelMarketUI" if run twice
             skillsObj = GameObject.Find("LevelMarketUI");
             if (skillsObj == null) return;
             Debug.Log("Found 'LevelMarketUI', ensuring it is assigned.");
        }
        else
        {
            // Found Skills under TapToPanel?
            if (skillsObj.transform.parent != null && skillsObj.transform.parent.name == "TapToPanel")
            {
                // MOVE IT
                Undo.SetTransformParent(skillsObj.transform, skillsObj.transform.parent.parent, "Move Skills out of TapToPanel"); // Assuming TapToPanel is under Canvas
                
                // If TapToPanel was root (unlikely for UI), check Canvas
                if (skillsObj.transform.parent == null)
                {
                    Canvas c = FindObjectOfType<Canvas>();
                    if (c != null) Undo.SetTransformParent(skillsObj.transform, c.transform, "Move Skills to Canvas");
                }

                // Rename
                Undo.RecordObject(skillsObj, "Rename Skills");
                skillsObj.name = "LevelMarketUI";
                Debug.Log("Moved 'Skills' to 'LevelMarketUI'.");
            }
        }

        // 2. Assign to GameFlowManager
        GameFlowManager flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
        {
            Undo.RecordObject(flow, "Assign Level Market Panel");
            flow.levelMarketPanel = skillsObj;
            EditorUtility.SetDirty(flow);
            Debug.Log("Assigned LevelMarketUI to GameFlowManager.");
        }
        else
        {
            Debug.LogError("GameFlowManager not found!");
        }
        
        // 3. Ensure Raycast Target is ON for buttons so they block clicks
        // (Usually handled by Image, but let's double check)
        var images = skillsObj.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach(var img in images)
        {
             if (!img.raycastTarget) 
             {
                 Undo.RecordObject(img, "Enable Raycast Target");
                 img.raycastTarget = true;
             }
        }
        
        Debug.Log("<color=green>Hierarchy Fix Complete!</color>");
    }
}
