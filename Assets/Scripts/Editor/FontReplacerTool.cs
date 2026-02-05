using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

public class FontReplacerTool : EditorWindow
{
    private TMP_FontAsset newFont;

    [MenuItem("Tools/Replace All Fonts")]
    public static void ShowWindow()
    {
        GetWindow<FontReplacerTool>("Font Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace All Fonts", EditorStyles.boldLabel);

        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font Asset", newFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Find 'LilitaOne' Automatically"))
        {
            FindLilitaOne();
        }

        if (GUILayout.Button("Replace in Open Scene & Prefabs"))
        {
            if (newFont == null)
            {
                Debug.LogError("Please assign a New Font Asset first!");
                return;
            }
            ReplaceFonts();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Assign to Scripts (Dynamic Text)"))
        {
             AssignToManagers();
             GUIUtility.ExitGUI();
        }
    }

    private void FindLilitaOne()
    {
        string[] guids = AssetDatabase.FindAssets("LilitaOne-Regular SDF t:TMP_FontAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Debug.Log($"Found font at: {path}");
        }
        else
        {
            Debug.LogError("Could not find 'LilitaOne-Regular SDF' asset. Please assign manually.");
        }
    }

    private void ReplaceFonts()
    {
        int count = 0;

        // 1. Scene Objects
        TextMeshProUGUI[] sceneTexts = FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var txt in sceneTexts)
        {
            Undo.RecordObject(txt, "Change Font");
            txt.font = newFont;
            EditorUtility.SetDirty(txt);
            count++;
        }
        
        // Also check world space TMP
        TextMeshPro[] sceneWorldTexts = FindObjectsOfType<TextMeshPro>(true);
        foreach (var txt in sceneWorldTexts)
        {
             Undo.RecordObject(txt, "Change Font");
             txt.font = newFont;
             EditorUtility.SetDirty(txt);
             count++;
        }

        // 2. Project Prefabs
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            bool changed = false;
            
            // Check Children (UGUI)
            TextMeshProUGUI[] prefabTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in prefabTexts)
            {
                if (txt.font != newFont)
                {
                    txt.font = newFont;
                    changed = true;
                    count++;
                }
            }
            
            // Check Children (World)
            TextMeshPro[] prefabWorldTexts = prefab.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var txt in prefabWorldTexts)
            {
                if (txt.font != newFont)
                {
                    txt.font = newFont;
                    changed = true;
                    count++;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                // PrefabUtility.SavePrefabAsset(prefab); // Caution: can be heavy
            }
        }
        
        // Explicitly Save all dirty assets
        AssetDatabase.SaveAssets();

        Debug.Log($"Successfully substituted fonts on {count} text objects!");
        EditorUtility.DisplayDialog("Success", $"Replaced font on {count} objects.", "OK");
    }

    private void AssignToManagers()
    {
        if (newFont == null)
        {
            Debug.LogError("Please assign a New Font Asset first!");
            return;
        }

        int count = 0;

        // 1. UIManager
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            Undo.RecordObject(uiManager, "Assign Banner Font");
            uiManager.bannerFont = newFont;
            EditorUtility.SetDirty(uiManager);
            count++;
            Debug.Log("Assigned font to UIManager.bannerFont");
        }

        // 2. HoleMechanics (Player)
        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        if (hole != null)
        {
            Undo.RecordObject(hole, "Assign Damage Font");
            hole.damageFont = newFont;
            EditorUtility.SetDirty(hole);
            count++;
            Debug.Log("Assigned font to HoleMechanics.damageFont");
        }

        // 3. Update Prefabs (Optional but good)
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool changed = false;

            HoleMechanics prefabHole = prefab.GetComponent<HoleMechanics>();
            if (prefabHole != null && prefabHole.damageFont != newFont)
            {
                prefabHole.damageFont = newFont;
                changed = true;
                count++;
            }
            
            UIManager prefabUI = prefab.GetComponent<UIManager>();
            if (prefabUI != null && prefabUI.bannerFont != newFont)
            {
                prefabUI.bannerFont = newFont;
                changed = true;
                count++;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
            }
        }
        
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Success", $"Assigned font ref to {count} scripts/prefabs.", "OK");
    }
}
