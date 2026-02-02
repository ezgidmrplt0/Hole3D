using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    private TextMeshProUGUI tmpText;
    private float lifeTime = 1f;
    private float timer;

    public void Setup(string text, Color color, TMP_FontAsset font = null)
    {
        // Setup TMP if not already
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (tmpText == null) tmpText = gameObject.AddComponent<TextMeshProUGUI>();

        tmpText.text = text;
        tmpText.color = color;
        if (font != null) tmpText.font = font; // Assign font
        tmpText.fontSize = 6; // Start small/readable
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = false;
        tmpText.fontStyle = FontStyles.Bold;

        // Ensure visuals
        timer = lifeTime;
    }

    void Start()
    {
        // Billboard (Look at Camera) - Start'ta bir kere ayarla, sürekli dönmesine gerek yok (veya Update'de kalsın ama performans için Start yeterli olabilir, yine de dinamik kamera varsa Update daha iyi)
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        // --- JUICY ANIMATION (DOTween) ---
        // 1. Scale Up (Pop Effect)
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one * 0.15f, 0.4f).SetEase(Ease.OutBack); // 0.1f -> 0.15f (Daha büyük)

        // 2. Move Up & Fade
        transform.DOMoveY(transform.position.y + 3f, 1.0f).SetEase(Ease.OutQuad);
        
        if (tmpText != null)
        {
            // 3. Fade Out (Sonlara doğru)
            tmpText.DOFade(0f, 0.5f).SetDelay(0.5f).OnComplete(() => Destroy(gameObject));
        }
        else
        {
             Destroy(gameObject, 1.0f);
        }
    }

    void Update()
    {
        // Kamera hareketliyse sürekli bakması lazım
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
