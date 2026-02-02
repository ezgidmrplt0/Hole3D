using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SkillNavigation : MonoBehaviour
{
    [Header("Settings")]
    public Sprite navigationIcon; 
    public float iconSize = 150f; // Increased to match zombie indicator
    public float padding = 50f; 
    [Tooltip("Adjust this if the pin points in the wrong direction. 90 is usually good for Down-pointing pins.")]
    public float rotationOffset = 90f;
    
    [Header("Manual Setup (Optional)")]
    // ... (lines 13-118 are fine, skipping to UpdateIndicator logic modification if needed, but variables are at top)

    // Wait, the tool requires contiguous edit. I will effectively replace the settings block and the UpdateIndicator block if necessary, or just the whole file if it's easier, but partial is better.
    // Let's replace the Settings block first.

    
    [Header("Manual Setup (Optional)")]
    public Canvas assignedCanvas;
    public Image assignedIndicator;

    [Header("Debug Info")]
    public bool showDebug = false;
    public int skillCount = 0;
    public string status = "Running";

    private RectTransform indicatorRect;
    private Image indicatorImage;
    private Canvas navCanvas;
    private Camera mainCam;
    private Transform playerTransform;
    private SkillPickup currentTarget; // Modified from ZombieAI

    void Start()
    {
        // Enforce correct rotation offset for standard pin sprites (Tip pointing Down)
        // This overrides any potentially incorrect Inspector values (like 0).
        rotationOffset = 90f; 

        AutoLoadSprite();
        Invoke(nameof(SetupSystem), 0.1f);
    }

    private void AutoLoadSprite()
    {
#if UNITY_EDITOR
        if (navigationIcon == null)
        {
            string path = "Assets/Textures/SkillIndicator.png";
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
            // Auto-Create/Find Canvas logic... REUSING NavigationCanvas if possible
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
            
            if (!indicatorImage.transform.IsChildOf(navCanvas.transform))
            {
                indicatorImage.transform.SetParent(navCanvas.transform, false);
            }
            status += " Using Manual Indicator.";
        }
        else
        {
             // Auto-Create Icon Logic for SKILL
             Transform child = navCanvas.transform.Find("SkillIndicator");
             if (child == null)
             {
                 GameObject imgObj = new GameObject("SkillIndicator");
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
                // Fallback color - Yellow/Gold for skills
                if (indicatorImage.sprite == null) indicatorImage.color = Color.yellow; 
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
            if (mainCam == null) mainCam = FindObjectOfType<Camera>(); 
        }

        if (playerTransform == null) 
        {
            HoleMechanics hole = FindObjectOfType<HoleMechanics>();
             if (hole != null) 
             {
                 playerTransform = hole.transform;
             }
             else
             {
                 GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                 if (pObj != null) playerTransform = pObj.transform;
                 else
                 {
                    status = "Waiting for Player...";
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

        // Search logic for SKILLS
        SkillPickup[] skills = FindObjectsOfType<SkillPickup>();
        skillCount = skills.Length;
        
        float minDist = float.MaxValue;
        SkillPickup bestTarget = null;
        
        // We do NOT stop if visible, because skills can be small/hard to see. 
        // But for consistency with user request "zombiler gibi", maybe we should?
        // Let's assume we want to guide them to the NEAREST skill regardless, or maybe only if offscreen.
        // ZombieNavigation hides if *ANY* is visible. Let's do the same for now to avoid clutter.
        bool anyVisible = false;

        foreach (var s in skills)
        {
            if (s == null || !s.gameObject.activeInHierarchy) continue;

            Vector3 vp = mainCam.WorldToViewportPoint(s.transform.position);
            
            // Check if IS ON SCREEN
            bool isOnScreen = (vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1 && vp.z > 0);

            if (isOnScreen)
            {
                anyVisible = true;
                // If we want to hide when ANY is visible
                break; 
            }

            // If we are here, s is Off-Screen. Is it the closest?
            float d = Vector3.Distance(playerTransform.position, s.transform.position);
            if (d < minDist)
            {
                minDist = d;
                bestTarget = s;
            }
        }
        
        // LOGIC: Use Navigation ONLY if NO skills are visible on screen
        if (anyVisible)
        {
            currentTarget = null;
            status = "Skill Visible - Nav Hidden";
        }
        else
        {
            currentTarget = bestTarget;
        }

        if (currentTarget != null)
        {
            status = "Skill Target Found: " + currentTarget.name;
            UpdateIndicator(currentTarget.transform.position);
        }
        else
        {
            if (indicatorRect != null && indicatorRect.gameObject.activeSelf) 
                indicatorRect.gameObject.SetActive(false);
        }
    }

    [Header("Visual Feedback")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    public float scaleDistance = 20f; // Distance at which scaling maxes out

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

        // --- DISTANCE SCALING (KEPT OPTIONAL) ---
        // Zombi scriptinde bu yok, ama skill için "netleşme" efekti güzel olabilir.
        // Eğer kullanıcı tamamen aynı olsun isterse burayı silebiliriz.
        // Şimdilik tutuyorum çünkü şikayet "yön/gösterme" üzerineydi.
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(playerTransform.position, targetWorldPos);
            float factor = 1f - Mathf.Clamp01(dist / scaleDistance); 
            float targetScale = Mathf.Lerp(minScale, maxScale, factor);
            indicatorRect.localScale = Vector3.one * targetScale;
        }
    }
}
