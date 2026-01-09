using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class OffScreenIndicatorSystem : MonoBehaviour
{
    public static OffScreenIndicatorSystem Instance;

    [Header("Settings")]
    public GameObject indicatorPrefab;
    public Transform indicatorParent;
    public Color indicatorColor = Color.white; // Default white since sprite is white

    [Header("Runtime Info")]
    public List<ZombieAI> targets = new List<ZombieAI>();

    private List<TargetIndicator> indicatorsPool = new List<TargetIndicator>();
    private Camera mainCamera;
    private Canvas mainCanvas;

    void OnEnable()
    {
        Instance = this;
        mainCamera = Camera.main;
        if (transform.parent != null)
            mainCanvas = transform.parent.GetComponentInParent<Canvas>();
        else
            mainCanvas = FindObjectOfType<Canvas>();

        // Rebuild pool from existing children to avoid duplicates
        indicatorsPool.Clear();
        if (indicatorParent != null)
        {
            foreach (Transform child in indicatorParent)
            {
                TargetIndicator ti = child.GetComponent<TargetIndicator>();
                if (ti != null && child.gameObject != indicatorPrefab) // Don't include the template if it's in the same list
                {
                    indicatorsPool.Add(ti);
                    child.gameObject.SetActive(false); // Reset
                }
            }
        }
    }

    void Update()
    {
        // Safety checks
        if (indicatorPrefab == null || indicatorParent == null) return;
        
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindObjectOfType<Camera>(); // Fallback to any camera
        
        if (mainCanvas == null) mainCanvas = FindObjectOfType<Canvas>(); // Fallback

        if (mainCamera == null) return; // Cannot update without camera

        // Find targets
        // In Edit Mode, we want to see it update live
        // In Play Mode, we might want to optimize
        ZombieAI[] sceneZombies = FindObjectsOfType<ZombieAI>();
        targets.Clear();
        targets.AddRange(sceneZombies);

        ManageIndicators();
    }

    void ManageIndicators()
    {
        // Ensure we are drawn on top of other UI elements
        transform.SetAsLastSibling();

        ZombieAI closestTarget = null;
        float shortestDistance = float.MaxValue;

        // Find nearest off-screen target
        foreach (var target in targets)
        {
            if (target == null) continue;
            if (!target.gameObject.activeInHierarchy) continue;

            // Check if off-screen (simple viewport check)
            Vector3 vp = mainCamera.WorldToViewportPoint(target.transform.position);
            bool onScreen = vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1 && vp.z > 0;

            if (!onScreen)
            {
                float dist = Vector3.Distance(mainCamera.transform.position, target.transform.position);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    closestTarget = target;
                }
            }
        }

        int activeCount = 0;

        if (closestTarget != null)
        {
            TargetIndicator indicator = GetIndicator(0);
            
            // Initialize/Update
            indicator.Initialize(closestTarget.transform, mainCamera, mainCanvas);
            indicator.UpdateTarget();

            var img = indicator.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = indicatorColor;

            activeCount = 1;
        }

        // Hide unused
        for (int i = activeCount; i < indicatorsPool.Count; i++)
        {
            if (indicatorsPool[i] != null)
                indicatorsPool[i].gameObject.SetActive(false);
        }
    }

    TargetIndicator GetIndicator(int index)
    {
        if (index < indicatorsPool.Count)
        {
            if (indicatorsPool[index] == null) 
            {
                indicatorsPool.RemoveAt(index);
                return GetIndicator(index);
            }
            return indicatorsPool[index];
        }

        if (indicatorParent == null || indicatorPrefab == null) return null;

        GameObject obj = Instantiate(indicatorPrefab, indicatorParent);
        obj.name = "Indicator_" + index;
        TargetIndicator ti = obj.GetComponent<TargetIndicator>();
        if (ti == null) ti = obj.AddComponent<TargetIndicator>();
        
        obj.SetActive(false); 
        indicatorsPool.Add(ti);
        return ti;
    }

    // Visual Debugging in Scene View
    void OnDrawGizmos()
    {
        if (targets == null) return;
        Gizmos.color = Color.yellow;
        foreach (var target in targets)
        {
            if (target != null)
            {
                Gizmos.DrawLine(transform.position, target.transform.position);
                Gizmos.DrawSphere(target.transform.position, 0.5f);
            }
        }
    }
}
