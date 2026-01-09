using UnityEngine;
using UnityEngine.UI;

public class TargetIndicator : MonoBehaviour
{
    [SerializeField] private Image indicatorImage;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private float rotationOffset = 0f;

    private Transform target;
    private Camera mainCamera;
    private Canvas canvas;
    private RectTransform parentRect;

    public void Initialize(Transform target, Camera camera, Canvas canvas)
    {
        this.target = target;
        this.mainCamera = camera;
        this.canvas = canvas;
        if (transform.parent != null) parentRect = transform.parent.GetComponent<RectTransform>();
        
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (indicatorImage == null) indicatorImage = GetComponent<Image>();
        
        // Ensure anchors are center to make math easier
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    public void UpdateTarget()
    {
        // Robust Camera Finding for Editor Mode
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindObjectOfType<Camera>(); // Fallback to any camera
        
        if (target == null || mainCamera == null || canvas == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 1. Check if visible / off-screen logic
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(target.position);
        bool isBehind = screenPoint.z < 0;
        
        // Use a safe margin. If the point is wildly off screen, we clamp it later.
        float margin = 50f; 
        bool isOnScreen = screenPoint.x > margin && screenPoint.x < Screen.width - margin &&
                          screenPoint.y > margin && screenPoint.y < Screen.height - margin &&
                          !isBehind;

        if (!isOnScreen)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 2. Calculate direction from screen center
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 targetScreenPos = new Vector2(screenPoint.x, screenPoint.y);
            
            if (isBehind)
            {
                targetScreenPos = -targetScreenPos; // Invert if behind
            }

            Vector2 direction = (targetScreenPos - screenCenter).normalized;

            // 3. Find intersection with screen edge (minus padding)
            float padding = 50f; // Padding to keep arrow fully inside
            float screenHalfWidth = (Screen.width / 2f) - padding;
            float screenHalfHeight = (Screen.height / 2f) - padding;

            // Avoid division by zero
            if (direction == Vector2.zero) direction = Vector2.up;

            // Calculate intersection point (from center)
            // Slope m
            float m = direction.y / direction.x;

            float xVal = screenHalfWidth * Mathf.Sign(direction.x);
            float yVal = xVal * m;

            // Check if we hit top/bottom instead
            if (Mathf.Abs(yVal) > screenHalfHeight)
            {
                yVal = screenHalfHeight * Mathf.Sign(direction.y);
                xVal = yVal / m;
            }

            // Final screen position (pixels)
            Vector2 arrowScreenPos = screenCenter + new Vector2(xVal, yVal);

            // 4. Convert Screen Position to UI Local Position
            // This handles Canvas Scaling automatically
            Vector2 localPos;
            if (parentRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, arrowScreenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera, out localPos);
                rectTransform.anchoredPosition = localPos;
            }
            else
            {
                // Fallback if no parent (unlikely)
                rectTransform.position = arrowScreenPos; 
            }

            // 5. Rotation
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rectTransform.rotation = Quaternion.Euler(0, 0, angle - 90 + rotationOffset);
        }
        else
        {
            // Is visible on screen -> Hide Arrow
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
