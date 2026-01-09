using UnityEngine;
using DG.Tweening; // DOTween
using TMPro; // TextMeshPro

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    [Header("UI References")]
    public GameObject tapToPlayPanel;
    public Transform tapToPlayText; // Text'in transformu (Scale efekti için)

    public bool IsGameActive { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Oyunu durdur ve Paneli aç
        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);
            
            // Ensure the panel does not block clicks to buttons behind it (like Market)
            // And allows clicks to pass through to the GameStart logic
            CanvasGroup cg = tapToPlayPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = tapToPlayPanel.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; 

            IsGameActive = false;
            Time.timeScale = 0f; // Zamanı durdur (Fizik ve hareketler durur)

            // DOTween Efekti (Büyüyüp Küçülme)
            if (tapToPlayText != null)
            {
                // Mevcut Scale'i kaydet
                Vector3 originalScale = tapToPlayText.localScale;
                
                // Animasyon: Büyüt -> Geri al (Loop)
                tapToPlayText.DOScale(originalScale * 1.2f, 0.8f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true); // Zaman dursa bile çalışsın!
            }
        }
        else
        {
            // Panel atanmadıysa direkt başla
            IsGameActive = true;
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        // Oyun başlamadıysa ve tıklandıysa
        if (!IsGameActive)
        {
            // Ekrana her dokunuş oyunu başlatsın (UI üzerinde olsun olmasın)
            // Çünkü TapToPlay yazısı kendisi bir UI elemanı ve tıklamayı engelliyor olabilir.
            
            if (Input.GetMouseButtonDown(0))
            {
                StartGame();
            }
        }
        else
        {
            // OYUN AKTİFKEN UI ELEMANLARI HALA AÇIKSA KAPAT
            if (tapToPlayPanel != null && tapToPlayPanel.activeSelf) tapToPlayPanel.SetActive(false);
            
            // Eğer panel null ise veya panel yoksa bile text'in kendisini bulup kapatalım
            if (tapToPlayText != null && tapToPlayText.gameObject.activeSelf) tapToPlayText.gameObject.SetActive(false);
        }
    }

    public void StartGame()
    {
        IsGameActive = true;
        
        // 1. Panel Referansını Bulmaya Çalış
        if (tapToPlayPanel == null) FindUIElements();

        // 2. Paneli Kapat
        if (tapToPlayPanel != null)
        {
            // EĞER panel Canvas'ın kendisiyse (Root) kapatma! Sadece içindekileri kapat.
            if (tapToPlayPanel.GetComponent<Canvas>() != null)
            {
                // Panel aslında Canvasmış, kapatırsak joystick de gider.
                // Sadece texti kapat.
                if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            }
            else
            {
                tapToPlayPanel.SetActive(false);
            }
        }

        // 3. Text ve El İşaretini (Hand) ayrıca zorla kapat
        if (tapToPlayText != null) 
        {
            tapToPlayText.DOKill();
            tapToPlayText.gameObject.SetActive(false);
        }

        // "Hand" veya "Finger" isimli resimleri bul ve kapat
        GameObject handIcon = GameObject.Find("Hand");
        if (handIcon == null) handIcon = GameObject.Find("Finger");
        if (handIcon == null) handIcon = GameObject.Find("TapIcon");
        if (handIcon != null) handIcon.SetActive(false);

        Time.timeScale = 1f; 
        Debug.Log("Game Started! UI elements hidden.");
    }

    private void FindUIElements()
    {
        // 1. TextMeshPro Arama
        TextMeshProUGUI[] allTexts = FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var txt in allTexts)
        {
            if (txt.text.ToUpper().Contains("TAP TO PLAY"))
            {
                tapToPlayText = txt.transform;
                
                // Paneli bulmaya çalış (Ama Canvas olmasın)
                Transform candidate = txt.transform.parent;
                if (candidate != null && candidate.GetComponent<Canvas>() == null)
                {
                    tapToPlayPanel = candidate.gameObject;
                }
                return; 
            }
        }

        // 2. Legacy Text Arama (Eğer TMP değilse)
        UnityEngine.UI.Text[] legacyTexts = FindObjectsOfType<UnityEngine.UI.Text>(true);
        foreach (var txt in legacyTexts)
        {
             if (txt.text.ToUpper().Contains("TAP TO PLAY"))
            {
                tapToPlayText = txt.transform;
                Transform candidate = txt.transform.parent;
                if (candidate != null && candidate.GetComponent<Canvas>() == null)
                {
                    tapToPlayPanel = candidate.gameObject;
                }
                return;
            }
        }
    }
}
