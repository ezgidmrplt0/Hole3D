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
            // Eğer bir UI elemanına (Buton, Panel vs.) tıklanıyorsa oyunu başlatma!
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Mobil için (Touch) kontrolü (Telefonda test yaparken UI'a basınca başlamaması için)
            if (Input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;

            if (Input.GetMouseButtonDown(0))
            {
                StartGame();
            }
        }
    }

    public void StartGame()
    {
        IsGameActive = true;
        
        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(false);
        }

        // Tween'leri temizle (Performans için)
        if (tapToPlayText != null)
        {
            tapToPlayText.DOKill();
        }

        Time.timeScale = 1f; // Zamanı başlat
        Debug.Log("Game Started!");
    }
}
