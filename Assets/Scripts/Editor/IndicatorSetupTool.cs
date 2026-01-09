using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class IndicatorSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup Zombie Indicators")]
    public static void Setup()
    {
        // 1. Find Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the scene! Please add a UI Canvas first.", "OK");
            return;
        }

        // 2. Create System Object
        GameObject systemObj = GameObject.Find("OffScreenIndicatorSystem");
        if (systemObj == null)
        {
            systemObj = new GameObject("OffScreenIndicatorSystem");
            systemObj.transform.SetParent(canvas.transform, false);
            
            // Set RectTransform to stretch or full screen to be safe, though it acts as a container
            RectTransform rt = systemObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // 3. Add Component
        OffScreenIndicatorSystem system = systemObj.GetComponent<OffScreenIndicatorSystem>();
        if (system == null) system = systemObj.AddComponent<OffScreenIndicatorSystem>();

        // 4. Create Indicator Template
        // Load directly from path to be safe
        string spritePath = "Assets/Textures/ZombieIndicator-removebg-preview.png";
        Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        
        if (arrowSprite == null)
        {
            Debug.LogError($"Could not load sprite at path: {spritePath}. Make sure the file exists and Texture Type is set to 'Sprite (2D and UI)' in Import Settings.");
        }

        // Check if we already have a template attached, if so destroy it to recreate or update
        // Actually, let's just update the existing one or create new
        if (system.indicatorPrefab != null)
        {
            DestroyImmediate(system.indicatorPrefab);
        }

        GameObject template = new GameObject("IndicatorTemplate");
        template.transform.SetParent(systemObj.transform, false);
        
        Image img = template.AddComponent<Image>();
        if (arrowSprite != null)
        {
            img.sprite = arrowSprite;
        }
        else
        {
            Debug.LogWarning("ZombieIndicator sprite not found. Please assign 'Assets/Textures/ZombieIndicator.png' as a Sprite (2D and UI) manually to the IndicatorTemplate.");
            img.color = Color.red; // Fallback
        }

        // Size - User requested "bigger"
        RectTransform templateRt = template.GetComponent<RectTransform>();
        templateRt.sizeDelta = new Vector2(360, 360); // 360x360 size (Double previous)

        // Add Logic Script
        TargetIndicator ti = template.AddComponent<TargetIndicator>();
        
        // **IMPORTANT**: Set rotation offset. Our pin now points DOWN.
        // The code rotates assuming "Right" is 0 degrees and "Up" is 90 degrees.
        // If the arrow points UP, offset is 0. 
        // If the arrow points DOWN (like a map pin), offset might need to be 180 or 0 is correct depending on logic.
        // Let's test: Angle - 90. If angle is 90 (Up), rot is 0. If arrow is UP, it's correct.
        // If arrow is DOWN, at rot 0 it points UP. So we need to rotate it 180.
        // Actually, the new image points DOWN by default. 
        // If we want it to point UP (to target), we need to flip it.
        // So offset 180 is correct.
        SerializedObject so = new SerializedObject(ti);
        so.FindProperty("rotationOffset").floatValue = 180f;
        so.ApplyModifiedProperties();

        // Assign to system
        system.indicatorPrefab = template;
        system.indicatorParent = systemObj.transform;

        // Disable template so it's not visible itself, only copies are
        template.SetActive(false);

        Debug.Log("OffScreen Indicator System Setup Complete! Check the Canvas.");
        Selection.activeGameObject = systemObj;
    }
}
