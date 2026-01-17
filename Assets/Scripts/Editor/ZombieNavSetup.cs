using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class ZombieNavSetup : EditorWindow
{
    [MenuItem("Tools/ZombieNav Setup")]
    public static void Setup()
    {
        // 1. Create or Find Canvas
        string canvasName = "NavigationCanvas";
        GameObject canvasObj = GameObject.Find(canvasName);
        if (canvasObj == null)
        {
            canvasObj = new GameObject(canvasName);
            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999; // Topmost
            
            CanvasScaler cs = canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Nav Canvas");
        }

        // 2. Create or Find Indicator Image
        Image indicatorImg = null;
        Transform childInfo = canvasObj.transform.Find("ZombieIndicator");
        if (childInfo != null)
        {
            indicatorImg = childInfo.GetComponent<Image>();
        }
        else
        {
            GameObject imgObj = new GameObject("ZombieIndicator");
            imgObj.transform.SetParent(canvasObj.transform, false);
            indicatorImg = imgObj.AddComponent<Image>();
            Undo.RegisterCreatedObjectUndo(imgObj, "Create Indicator");
        }

        // 3. Load Sprite
        string path = "Assets/Textures/ZombieIndicator-removebg-preview.png";
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (icon == null)
        {
            // Try to fix texture importer
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
        }

        if (icon != null)
        {
            indicatorImg.sprite = icon;
            indicatorImg.SetNativeSize(); // Resize to match image
        }
        else
        {
            indicatorImg.color = Color.red; // Fallback
            Debug.LogWarning("ZombieNav: Could not find sprite at " + path);
        }

        // Set Size
        RectTransform rt = indicatorImg.rectTransform;
        rt.sizeDelta = new Vector2(150, 150);
        indicatorImg.gameObject.SetActive(false); // Hide by default

        // 4. Find or Create Manager Script
        ZombieNavigation navScript = FindObjectOfType<ZombieNavigation>();
        if (navScript == null)
        {
            GameObject manager = new GameObject("ZombieNavigationManager");
            navScript = manager.AddComponent<ZombieNavigation>();
            Undo.RegisterCreatedObjectUndo(manager, "Create Nav Manager");
        }

        // 5. Link References
        SerializedObject so = new SerializedObject(navScript);
        so.Update();
        
        SerializedProperty propCanvas = so.FindProperty("assignedCanvas");
        SerializedProperty propIndicator = so.FindProperty("assignedIndicator");
        SerializedProperty propIcon = so.FindProperty("navigationIcon");

        if (propCanvas != null) propCanvas.objectReferenceValue = canvasObj.GetComponent<Canvas>();
        if (propIndicator != null) propIndicator.objectReferenceValue = indicatorImg;
        if (propIcon != null && icon != null) propIcon.objectReferenceValue = icon;

        so.ApplyModifiedProperties();

        Debug.Log("<color=green>ZombieNav Setup Complete!</color> Check the scene.");
        Selection.activeGameObject = navScript.gameObject;
    }
}
