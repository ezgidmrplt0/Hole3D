using UnityEngine;
using UnityEngine.UI;

public class HoleVisuals : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("The Image component representing the circular progress bar.")]
    public Image progressImage; // This should be type Filled (Radial 360)

    private void Start()
    {
        // LevelManager bağlantısını kaldırdık. 
        // Bu bar artık sadece Deliğin XP durumunu (Büyüme) gösterecek.
        // Stage progress için ayrı bir UI barı (ekranın üstünde) kullanılması daha doğru olur.
        UpdateProgress(0f);
    }

    private void OnDestroy()
    {
        // Temizlik
    }

    private void UpdateProgress(float progress)
    {
        if (progressImage != null)
        {
            progressImage.fillAmount = progress;
        }
    }

    public void UpdateLocalProgress(float value)
    {
        UpdateProgress(value);
    }
}
