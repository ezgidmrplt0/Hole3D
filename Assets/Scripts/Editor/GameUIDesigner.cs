using UnityEngine;
using UnityEditor;

public class GameUIDesigner : EditorWindow
{
    // Textures
    private Texture2D btnViolet;
    private Texture2D iconStore;
    private Texture2D iconCoin;
    private Texture2D panelDark;
    private Texture2D panelWhite;
    private Texture2D iconPlus; // We might use a generic one or text if not found, but I'll check specifically or just use text

    // Fonts / Styles
    private GUIStyle headerStyle;
    private GUIStyle marketBtnStyle;
    private GUIStyle coinTextStyle;
    private GUIStyle tapToPlayStyle;
    
    // Paths (Hardcoded based on project structure exploration)
    private const string PATH_BTN_VIOLET = "Assets/Violet Theme Ui/Buttons/Button Violet.png";
    private const string PATH_ICON_STORE = "Assets/Violet Theme Ui/Colored Icons/Store 1.png";
    private const string PATH_ICON_COIN = "Assets/Violet Theme Ui/Colored Icons/Coin.png";
    private const string PATH_PANEL_DARK = "Assets/Violet Theme Ui/Panels/Dark Panel Violet.png";
    private const string PATH_PANEL_WHITE = "Assets/Violet Theme Ui/Panels/White Panel Violet.png";

    [MenuItem("Tools/Game UI Designer")]
    public static void ShowWindow()
    {
        GetWindow<GameUIDesigner>("Hole3D UI Preview");
    }

    private void OnEnable()
    {
        LoadAssets();
    }

    private void LoadAssets()
    {
        btnViolet = AssetDatabase.LoadAssetAtPath<Texture2D>(PATH_BTN_VIOLET);
        iconStore = AssetDatabase.LoadAssetAtPath<Texture2D>(PATH_ICON_STORE);
        iconCoin = AssetDatabase.LoadAssetAtPath<Texture2D>(PATH_ICON_COIN);
        panelDark = AssetDatabase.LoadAssetAtPath<Texture2D>(PATH_PANEL_DARK);
        panelWhite = AssetDatabase.LoadAssetAtPath<Texture2D>(PATH_PANEL_WHITE);
    }

    private void OnGUI()
    {
        if (btnViolet == null) LoadAssets();

        // Draw Background (Simulate Mobile Screen roughly)
        // We just fill the window with a dark color or the panel texture to simulate the game view bg
        // Actually, let's just make it a dark gray background for the window itself
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.2f, 0.2f, 0.2f));

        // Define a "Safe Area" or "Phone Screen" aspect ratio
        float screenWidth = position.width;
        float screenHeight = position.height;
        
        // --- TOP BAR ---
        float topBarHeight = 80f;
        Rect topArea = new Rect(0, 0, screenWidth, topBarHeight);
        
        // Let's create a container for the top bar elements
        GUILayout.BeginArea(topArea);
        GUILayout.BeginHorizontal();

        // 1. MARKET BUTTON (Top Left)
        DrawMarketButton();

        GUILayout.FlexibleSpace();

        // 2. LEVEL INDICATOR (Top Center)
        DrawLevelIndicator();

        GUILayout.FlexibleSpace();

        // 3. COIN DISPLAY (Top Right)
        DrawCoinDisplay();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();


        // --- CENTER ---
        // TAP TO PLAY Text
        DrawTapToPlay_Center();
    }

    private void DrawMarketButton()
    {
        // Style Setup
        if (marketBtnStyle == null)
        {
            marketBtnStyle = new GUIStyle(GUI.skin.button);
            marketBtnStyle.normal.background = btnViolet;
            marketBtnStyle.hover.background = btnViolet;
            marketBtnStyle.active.background = btnViolet;
            marketBtnStyle.alignment = TextAnchor.MiddleLeft;
            marketBtnStyle.normal.textColor = Color.white;
            marketBtnStyle.fontSize = 14;
            marketBtnStyle.fontStyle = FontStyle.Bold;
        }

        // Layout
        // Icon + Text "Market"
        // We'll draw a button, then overlay the icon manually for better control
        
        float btnWidth = 110f;
        float btnHeight = 45f;

        Rect btnRect = new Rect(10, 15, btnWidth, btnHeight);
        
        if (GUI.Button(btnRect, "", marketBtnStyle))
        {
            Debug.Log("Market Clicked (Preview)");
        }

        // Draw Icon inside button
        if (iconStore != null)
        {
            GUI.DrawTexture(new Rect(btnRect.x + 5, btnRect.y + 5, 35, 35), iconStore);
        }

        // Draw Text "Market"
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.fontSize = 14;
        textStyle.alignment = TextAnchor.MiddleLeft;
        
        GUI.Label(new Rect(btnRect.x + 45, btnRect.y, 60, btnHeight), "Market", textStyle);
    }

    private void DrawLevelIndicator()
    {
        // White Panel or Dark Panel bg? 
        // Image shows: "LEVEL 1" text on a simple background, maybe transparent or dark panel
        // Let's use the Dark Panel for a "Card" look
        
        float width = 120f;
        float height = 40f;
        
        // Centering logic is handled by FlexibleSpace in main OnGUI, but we need rect here for local GUI
        // Actually, since we are in BeginHorizontal, we can just grab a rect using GUILayoutUtility or just draw in fixed position relative to screen center?
        // Let's use GUILayout.Box with a custom style
        
        GUIStyle levelStyle = new GUIStyle(GUI.skin.box);
        if (panelDark != null) levelStyle.normal.background = panelDark;
        levelStyle.alignment = TextAnchor.MiddleCenter;
        levelStyle.normal.textColor = new Color(1f, 0.8f, 1f); // Light violet text
        levelStyle.fontSize = 18;
        levelStyle.fontStyle = FontStyle.Bold;
        levelStyle.border = new RectOffset(10, 10, 10, 10);

        levelStyle.margin = new RectOffset(0, 0, 15, 0);
        GUILayout.Box("LEVEL 1", levelStyle, GUILayout.Width(width), GUILayout.Height(height)); 
    }

    private void DrawCoinDisplay()
    {
        float totalWidth = 120f;
        float height = 40f;
        
        Rect rect = new Rect(position.width - totalWidth - 10, 15, totalWidth, height); // Absolute positioning for right anchor

        // Background
        GUI.DrawTexture(rect, panelDark);

        // Coin Icon
        if (iconCoin != null)
        {
            GUI.DrawTexture(new Rect(rect.x - 15, rect.y + 2, 36, 36), iconCoin); // Icon slightly overlapping left
        }

        // Text "99"
        GUIStyle coinStyle = new GUIStyle(GUI.skin.label);
        coinStyle.normal.textColor = Color.yellow;
        coinStyle.fontSize = 20;
        coinStyle.fontStyle = FontStyle.Bold;
        coinStyle.alignment = TextAnchor.MiddleRight;
        
        GUI.Label(new Rect(rect.x, rect.y, rect.width - 35, rect.height), "99", coinStyle);

        // Plus Button
        float plusSize = 30f;
        Rect plusRect = new Rect(rect.x + rect.width - plusSize - 5, rect.y + 5, plusSize, plusSize);
        
        // Use standard button style but tinted red/pink
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Reddish
        if (GUI.Button(plusRect, "+"))
        {
             Debug.Log("Add Coin Clicked");
        }
        GUI.backgroundColor = oldColor;
    }

    private void DrawTapToPlay_Center()
    {
        if (tapToPlayStyle == null)
        {
            tapToPlayStyle = new GUIStyle(GUI.skin.label);
            tapToPlayStyle.fontSize = 40;
            tapToPlayStyle.fontStyle = FontStyle.Bold;
            tapToPlayStyle.normal.textColor = Color.white;
            tapToPlayStyle.alignment = TextAnchor.MiddleCenter;
        }

        // Blinking Effect
        float alpha = Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 1f);
        Color c = Color.white;
        c.a = alpha;
        tapToPlayStyle.normal.textColor = c;

        Rect centerArea = new Rect(0, position.height / 2 - 50, position.width, 100);
        GUI.Label(centerArea, "TAP TO PLAY", tapToPlayStyle);
        
        // Repaint constantly for animation
        Repaint();
    }
}
