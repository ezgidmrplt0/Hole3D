using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class LevelMarketReskinTool : EditorWindow
{
    [MenuItem("Tools/Rebuild Level Market UI (Fresh)")]
    public static void RebuildUI()
    {
        // 1. Find Main Canvas specifically (ByName is safer than Type if multiple canvases exist)
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            Canvas c = LinkCanvasFallback();
            if(c != null) canvasObj = c.gameObject;
        }

        if (canvasObj == null)
        {
            Debug.LogError("No 'Canvas' object found in scene!");
            return;
        }

        // 2. Delete existing LevelMarketUI if any
        GameObject existing = GameObject.Find("LevelMarketUI");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        // 3. Create Main Container
        GameObject marketPanel = new GameObject("LevelMarketUI");
        marketPanel.transform.SetParent(canvasObj.transform, false);
        Undo.RegisterCreatedObjectUndo(marketPanel, "Create Level Market UI");

        RectTransform rt = marketPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); // Center
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -300); // Shift down a bit
        rt.sizeDelta = new Vector2(800, 350);

        // Layout
        HorizontalLayoutGroup layout = marketPanel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        // 4. Load Assets
        Sprite bgRed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/NEW Panels/Panel 1 RED.png");
        Sprite bgOrange = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/NEW Panels/Panel 1 ORANGE.png");
        Sprite bgBlue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/NEW Panels/Panel 1 BLUE.png");
        
        Sprite iconCoin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Coin.png");
        Sprite iconMagnet = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Magnet.png");
        Sprite iconSpeed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Energy.png");
        Sprite iconShield = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/shield blue.png");

        // 5. Create Cards
        CreateCard(marketPanel.transform, "Magnet", "MAGNET", bgRed, iconMagnet, iconCoin, "50");
        CreateCard(marketPanel.transform, "Speed", "SPEED", bgOrange, iconSpeed, iconCoin, "40");
        CreateCard(marketPanel.transform, "Shield", "SHIELD", bgBlue, iconShield, iconCoin, "60");

        // 6. Assign References (Auto-Wire)
        AssignToManagers(marketPanel);

        Debug.Log("<color=green>Level Market UI Rebuilt Successfully!</color>");
    }

    private static void CreateCard(Transform parent, string name, string title, Sprite bg, Sprite icon, Sprite coin, string priceParams)
    {
        // Card Root
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);
        
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220, 300);

        // Background Image & Button
        Image bgImg = card.AddComponent<Image>();
        bgImg.sprite = bg;
        bgImg.type = Image.Type.Sliced;
        
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        // --- TITLE ---
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(card.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0, 1);
        tRt.anchorMax = new Vector2(1, 1);
        tRt.pivot = new Vector2(0.5f, 1);
        tRt.anchoredPosition = new Vector2(0, -15);
        tRt.sizeDelta = new Vector2(0, 50);
        
        TextMeshProUGUI titleTxt = tObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = title;
        titleTxt.fontSize = 24;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = Color.white;
        
        if (tObj.GetComponent<Outline>() == null)
        {
            var ol = tObj.AddComponent<Outline>();
            ol.effectColor = new Color(0,0,0, 0.5f);
            ol.effectDistance = new Vector2(2, -2);
        }

        // --- ICON ---
        GameObject iObj = new GameObject("Icon");
        iObj.transform.SetParent(card.transform, false);
        RectTransform iRt = iObj.AddComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0.5f, 0.5f);
        iRt.anchorMax = new Vector2(0.5f, 0.5f);
        iRt.anchoredPosition = new Vector2(0, 10);
        iRt.sizeDelta = new Vector2(100, 100);
        
        Image iImg = iObj.AddComponent<Image>();
        iImg.sprite = icon;
        iImg.preserveAspect = true;

        // --- PRICE TAG ---
        GameObject pObj = new GameObject("PriceTag");
        pObj.transform.SetParent(card.transform, false);
        RectTransform pRt = pObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0);
        pRt.anchorMax = new Vector2(0.5f, 0);
        pRt.pivot = new Vector2(0.5f, 0);
        pRt.anchoredPosition = new Vector2(0, 20);
        pRt.sizeDelta = new Vector2(160, 45); // Fixed size tag
        
        Image pBg = pObj.AddComponent<Image>();
        pBg.color = new Color(0, 0, 0, 0.4f); // Dark semi-transparent
        
        // Coin Icon
        GameObject cObj = new GameObject("Coin");
        cObj.transform.SetParent(pObj.transform, false);
        RectTransform cRt = cObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 0.5f); // Left
        cRt.anchorMax = new Vector2(0, 0.5f);
        cRt.pivot = new Vector2(0, 0.5f);
        cRt.anchoredPosition = new Vector2(10, 0);
        cRt.sizeDelta = new Vector2(30, 30);
        
        Image cImg = cObj.AddComponent<Image>();
        cImg.sprite = coin;

        // Price Text
        GameObject ptObj = new GameObject("PriceText");
        ptObj.transform.SetParent(pObj.transform, false);
        RectTransform ptRt = ptObj.AddComponent<RectTransform>();
        ptRt.anchorMin = new Vector2(0, 0);
        ptRt.anchorMax = new Vector2(1, 1);
        ptRt.offsetMin = new Vector2(45, 0); // Skip coin
        ptRt.offsetMax = new Vector2(-5, 0);
        
        TextMeshProUGUI pTxt = ptObj.AddComponent<TextMeshProUGUI>();
        pTxt.text = priceParams; // Placeholder, referenced later
        pTxt.fontSize = 24;
        pTxt.alignment = TextAlignmentOptions.MidlineRight; // Right align numbers
        pTxt.fontStyle = FontStyles.Bold;
        pTxt.color = Color.white;
    }

    private static void AssignToManagers(GameObject marketPanel)
    {
        // 1. GameFlowManager
        GameFlowManager flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
        {
            Undo.RecordObject(flow, "Assign Flow Market Panel");
            flow.levelMarketPanel = marketPanel;
        }

        // 2. UIManager Auto-Wire Logic (Same as SetupTool but integrated)
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            Undo.RecordObject(uiManager, "Assign Market Buttons");
            
            Transform tMag = marketPanel.transform.Find("Magnet");
            if (tMag) {
                uiManager.levelMagnetButton = tMag.GetComponent<Button>();
                uiManager.levelMagnetPriceText = tMag.Find("PriceTag/PriceText")?.GetComponent<TextMeshProUGUI>();
                SetupButtonEvent(uiManager.levelMagnetButton, uiManager, "BuyLevelMagnet");
            }
            
            Transform tSpd = marketPanel.transform.Find("Speed");
            if (tSpd) {
                uiManager.levelSpeedButton = tSpd.GetComponent<Button>();
                uiManager.levelSpeedPriceText = tSpd.Find("PriceTag/PriceText")?.GetComponent<TextMeshProUGUI>();
                SetupButtonEvent(uiManager.levelSpeedButton, uiManager, "BuyLevelSpeed");
            }

            Transform tShi = marketPanel.transform.Find("Shield");
            if (tShi) {
                uiManager.levelShieldButton = tShi.GetComponent<Button>();
                uiManager.levelShieldPriceText = tShi.Find("PriceTag/PriceText")?.GetComponent<TextMeshProUGUI>();
                SetupButtonEvent(uiManager.levelShieldButton, uiManager, "BuyLevelShield");
            }
            
            EditorUtility.SetDirty(uiManager);
        }
    }
    
    private static void SetupButtonEvent(Button btn, Object target, string methodName)
    {
        if (btn == null) return;
        
        // Clean old
        int count = btn.onClick.GetPersistentEventCount();
        for (int i = count - 1; i >= 0; i--)
        {
            if (btn.onClick.GetPersistentTarget(i) == target && 
                btn.onClick.GetPersistentMethodName(i) == methodName)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
            }
        }
        
        // Add new
        var methodInfo = typeof(UIManager).GetMethod(methodName);
        if (methodInfo != null)
        {
            UnityEngine.Events.UnityAction action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, methodInfo) as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }
    }


    private static Canvas LinkCanvasFallback()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach(var c in canvases) {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera) {
                return c;
            }
        }
        return null;
    }
}
