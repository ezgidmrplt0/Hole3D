using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ZombieNavigation : MonoBehaviour
{
    [Header("Settings")]
    public Sprite navigationIcon; 
    public float iconSize = 150f;
    public float padding = 50f; 
    [Tooltip("Adjust this if the pin points in the wrong direction. 90 is usually good for Down-pointing pins.")]
    public float rotationOffset = 90f; 
    
    [Header("Manual Setup (Optional)")]
    public Canvas assignedCanvas;
    public Image assignedIndicator;

    [Header("Debug Info")]
    public bool showDebug = false; // Hidden by default
    public int zombieCount = 0;
    public string status = "Running";


    private RectTransform indicatorRect;
    private Image indicatorImage;
    private Canvas navCanvas;
    private Camera mainCam;
    private Transform playerTransform;
    private ZombieAI currentTarget;

    void Start()
    {
        AutoLoadSprite();
        Invoke(nameof(SetupSystem), 0.1f);
    }

    private void AutoLoadSprite()
    {
#if UNITY_EDITOR
        if (navigationIcon == null)
        {
            string path = "Assets/Textures/ZombieIndicator-removebg-preview.png";
            navigationIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (navigationIcon == null)
            {
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                    if (importer != null) {
                        importer.textureType = UnityEditor.TextureImporterType.Sprite;
                        importer.SaveAndReimport();
                        navigationIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }
            }
        }
#endif
    }

    void SetupSystem()
    {
        status = "Setting up...";

        // 1. USE MANUAL SETUP IF AVAILABLE
        if (assignedCanvas != null)
        {
            navCanvas = assignedCanvas;
            status += " Using Manual Canvas.";
        }
        else
        {
            // Auto-Create Canvas logic...
            GameObject canvasObj = GameObject.Find("NavigationCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("NavigationCanvas");
                navCanvas = canvasObj.AddComponent<Canvas>();
                navCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                navCanvas.sortingOrder = 100;
                
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                navCanvas = canvasObj.GetComponent<Canvas>();
            }
        }

        // 2. USE MANUAL INDICATOR IF AVAILABLE
        if (assignedIndicator != null)
        {
            indicatorImage = assignedIndicator;
            indicatorRect = indicatorImage.rectTransform;
            
            // Ensure child of canvas if user messed up
            if (!indicatorImage.transform.IsChildOf(navCanvas.transform))
            {
                indicatorImage.transform.SetParent(navCanvas.transform, false);
            }
            status += " Using Manual Indicator.";
        }
        else
        {
             // Auto-Create Icon Logic...
             Transform child = navCanvas.transform.Find("ZombieIndicator");
             if (child == null)
             {
                 GameObject imgObj = new GameObject("ZombieIndicator");
                 imgObj.transform.SetParent(navCanvas.transform, false);
                 indicatorImage = imgObj.AddComponent<Image>();
                 indicatorRect = imgObj.GetComponent<RectTransform>();
             }
             else
             {
                 indicatorImage = child.GetComponent<Image>();
                 indicatorRect = child.GetComponent<RectTransform>();
             }
             
             // Setup Auto Icon properties
             indicatorRect.sizeDelta = new Vector2(iconSize, iconSize);
             indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
             indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
             indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 3. Final visual setup
        if (indicatorImage != null)
        {
            if (navigationIcon != null)
            {
                indicatorImage.sprite = navigationIcon;
                indicatorImage.color = Color.white;
            }
            else
            {
                // Only set red if no sprite manual or auto
                if (indicatorImage.sprite == null) indicatorImage.color = Color.red; 
            }
            indicatorImage.raycastTarget = false;
        }

        if (indicatorRect != null) indicatorRect.gameObject.SetActive(false); // Hide initially
        
        mainCam = Camera.main;
        status = "Setup Complete.";
    }

    void Update()
    {
        // 1. Validate Critical References
        if (mainCam == null) 
        {
            mainCam = Camera.main;
            if (mainCam == null) mainCam = FindObjectOfType<Camera>(); // Fallback
        }

        if (playerTransform == null) 
        {
            // Try Strategy 1: Find HoleMechanics
            HoleMechanics hole = FindObjectOfType<HoleMechanics>();
             if (hole != null) 
             {
                 playerTransform = hole.transform;
             }
             else
             {
                 // Try Strategy 2: Find by Tag
                 GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                 if (pObj == null) pObj = GameObject.FindGameObjectWithTag("PlayerHole"); // Custom Tag?
                 
                 if (pObj != null) playerTransform = pObj.transform;
                 else
                 {
                    // If no player, hide indicator and Wait
                    status = "Waiting for Player... (Check Tags)";
                    if (indicatorRect != null) indicatorRect.gameObject.SetActive(false);
                    return;
                 }
             }
        }
        
        if (mainCam == null)
        {
             status = "Waiting for Camera...";
             return;
        }

        // Search logic
        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        zombieCount = zombies.Length;
        
        float minDist = float.MaxValue;
        ZombieAI bestTarget = null;
        bool anyVisible = false;

        foreach (var z in zombies)
        {
            if (z == null) continue;

            Vector3 vp = mainCam.WorldToViewportPoint(z.transform.position);
            
            // Check if IS ON SCREEN
            bool isOnScreen = (vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1 && vp.z > 0);

            if (isOnScreen)
            {
                anyVisible = true;
                break; // Found a visible zombie, so we don't need navigation.
            }

            // If we are here, z is Off-Screen. Is it the closest?
            float d = Vector3.Distance(playerTransform.position, z.transform.position);
            if (d < minDist)
            {
                minDist = d;
                bestTarget = z;
            }
        }
        
        // LOGIC: Use Navigation ONLY if NO zombies are visible
        if (anyVisible)
        {
            currentTarget = null;
            status = "Zombie Visible - Nav Hidden";
        }
        else
        {
            currentTarget = bestTarget;
        }

        if (currentTarget != null)
        {
            status = "Target Found: " + currentTarget.name;
            UpdateIndicator(currentTarget.transform.position);
        }
        else
        {
            // Either existing visible zombies OR no zombies at all
            if (indicatorRect != null && indicatorRect.gameObject.activeSelf) 
                indicatorRect.gameObject.SetActive(false);
        }
    }

    void UpdateIndicator(Vector3 targetWorldPos)
    {
        if (indicatorRect == null) return;
        
        if (!indicatorRect.gameObject.activeSelf) indicatorRect.gameObject.SetActive(true);

        Vector3 screenPos = mainCam.WorldToScreenPoint(targetWorldPos);

        // If behind the camera, flip the point
        if (screenPos.z < 0)
        {
            screenPos *= -1; 
        }

        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        Vector3 dir = (screenPos - screenCenter).normalized;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        indicatorRect.rotation = Quaternion.Euler(0, 0, angle + rotationOffset); 

        // Clamp to edges
        float w = Screen.width * 0.9f; // 90% of screen width (Padding built in)
        float h = Screen.height * 0.9f; 
        
        float boundaryX = w / 2f;
        float boundaryY = h / 2f;

        // Intersect ray with box
        // avoid div/0
        if (Mathf.Abs(dir.x) < 0.001f) dir.x = Mathf.Sign(dir.x) * 0.001f; 
        if (Mathf.Abs(dir.y) < 0.001f) dir.y = Mathf.Sign(dir.y) * 0.001f;

        float tX = (dir.x > 0 ? boundaryX : -boundaryX) / dir.x;
        float tY = (dir.y > 0 ? boundaryY : -boundaryY) / dir.y;
        
        // Smallest positive t is the intersection
        float t = Mathf.Min(Mathf.Abs(tX), Mathf.Abs(tY));
        
        Vector3 finalPos = screenCenter + (dir * t);
        
        // Convert to Local
        if (navCanvas != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(navCanvas.GetComponent<RectTransform>(), finalPos, navCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out localPoint);
            indicatorRect.anchoredPosition = localPoint;
        }
    }


}
