using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    private TextMeshProUGUI tmpText;
    private float lifeTime = 1f;
    private float timer;
    
    // Mobil için screen space kullanılıyorsa başlangıç pozisyonu
    private Vector3 startScreenPos;
    private bool isScreenSpace = false;
    
    // MOBİL FIX: Kamera referansını cache'le
    private Camera mainCam;

    public void Setup(string text, Color color, TMP_FontAsset font = null)
    {
        // Cache camera
        mainCam = Camera.main;
        
        // Setup TMP if not already
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (tmpText == null) tmpText = gameObject.AddComponent<TextMeshProUGUI>();

        tmpText.text = text;
        tmpText.color = color;
        if (font != null) tmpText.font = font; // Assign font
        
        // MOBİL FIX: Screen Space için daha büyük font ve daha iyi görünürlük
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tmpText.fontSize = 72; // Daha büyük font (48'den 72'ye)
            isScreenSpace = true;
            startScreenPos = transform.position;
            
            // MOBİL FIX: Outline ekle (Daha iyi görünürlük)
            tmpText.fontMaterial.EnableKeyword("OUTLINE_ON");
            tmpText.outlineWidth = 0.2f;
            tmpText.outlineColor = Color.black;
        }
        else
        {
            tmpText.fontSize = 8; // World space için biraz daha büyük (6'dan 8'e)
        }
        
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = false;
        tmpText.fontStyle = FontStyles.Bold;
        
        // Raycast target kapat (Performans)
        tmpText.raycastTarget = false;

        // Ensure visuals
        timer = lifeTime;
    }

    void Start()
    {
        // Billboard sadece World Space için gerekli
        if (!isScreenSpace && mainCam != null)
        {
            transform.rotation = mainCam.transform.rotation;
        }

        // --- JUICY ANIMATION (DOTween) ---
        // MOBİL FIX: SetUpdate(true) ile Time.timeScale'den bağımsız animasyon
        if (isScreenSpace)
        {
            // Screen Space animasyonu
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            
            // Yukarı hareket (Screen koordinatlarında)
            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                // MOBİL FIX: Daha fazla yukarı hareket
                rect.DOAnchorPosY(rect.anchoredPosition.y + 200f, 1.0f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            
            if (tmpText != null)
            {
                tmpText.DOFade(0f, 0.4f).SetDelay(0.6f).SetUpdate(true).OnComplete(() => {
                    // DOTween animasyonlarını temizle
                    DOTween.Kill(transform);
                    if (rect != null) DOTween.Kill(rect);
                    Destroy(gameObject);
                });
            }
            else
            {
                Destroy(gameObject, 1.0f);
            }
        }
        else
        {
            // World Space animasyonu (Eski mantık)
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one * 0.2f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true); // 0.15'ten 0.2'ye
            transform.DOMoveY(transform.position.y + 3f, 1.0f).SetEase(Ease.OutQuad).SetUpdate(true);
            
            if (tmpText != null)
            {
                tmpText.DOFade(0f, 0.5f).SetDelay(0.5f).SetUpdate(true).OnComplete(() => {
                    DOTween.Kill(transform);
                    Destroy(gameObject);
                });
            }
            else
            {
                Destroy(gameObject, 1.0f);
            }
        }
    }

    void Update()
    {
        // Billboard sadece World Space için gerekli
        if (!isScreenSpace && mainCam != null)
        {
            transform.rotation = mainCam.transform.rotation;
        }
    }
    
    // MOBİL FIX: Temizlik
    void OnDestroy()
    {
        DOTween.Kill(transform);
        DOTween.Kill(gameObject);
    }
}
