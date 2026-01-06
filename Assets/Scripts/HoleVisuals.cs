using UnityEngine;
using UnityEngine.UI;

public class HoleVisuals : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("The Image component representing the circular progress bar.")]
    public Image progressImage; // This should be type Filled (Radial 360)

    private void Start()
    {
        // Subscribe to LevelManager progress events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnProgressUpdated += UpdateProgress;
        }

        // Initialize empty
        UpdateProgress(0f);
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnProgressUpdated -= UpdateProgress;
        }
    }

    private void UpdateProgress(float progress)
    {
        if (progressImage != null)
        {
            progressImage.fillAmount = progress;
        }
    }
}
