using UnityEngine;
using DG.Tweening; // DOTween
using TMPro; // TextMeshPro

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    [Header("UI References")]
    public GameObject tapToPlayPanel;
    public Transform tapToPlayText; // Text'in transformu (Scale efekti için)
    public Transform tapToNextLevelText; // Level geçiş yazısı

    public bool IsGameActive { get; private set; } = false;
    private bool isLevelCompleteState = false; // Hangi moddayız? (Start vs Next Level)



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

            // Initial State: Sadece TapToPlay açık olsun
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(true);
            if (tapToNextLevelText != null) tapToNextLevelText.gameObject.SetActive(false);

            IsGameActive = false;
            Time.timeScale = 0f; // Zamanı durdur (Fizik ve hareketler durur)

            // DOTween Efekti (Büyüyüp Küçülme)
            AnimateText(tapToPlayText);
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
                if (isLevelCompleteState)
                {
                    // Level Geçişi Modu
                    if (LevelManager.Instance != null) LevelManager.Instance.NextLevel();
                    
                    // Reset state
                    isLevelCompleteState = false;
                    
                    // UI Düzenlemesi: Next level başladığı için Play yazısına dönebiliriz (ama StartGame paneli kapatacak)
                    if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(true);
                    if (tapToNextLevelText != null) tapToNextLevelText.gameObject.SetActive(false);
                }

                StartGame();
            }
        }
    }

    public void ShowLevelComplete()
    {
        isLevelCompleteState = true;
        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);
            IsGameActive = false;
            Time.timeScale = 0f;

            // Yazıları Değiştir
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            if (tapToNextLevelText != null) 
            {
                tapToNextLevelText.gameObject.SetActive(true);
                AnimateText(tapToNextLevelText);
            }
        }
    }

    private void AnimateText(Transform target)
    {
        if (target != null)
        {
            // Reset
            target.DOKill();
            
            // Mevcut Scale'i baz al (Dikkat: Sürekli büyümeyi önlemek için localScale'i sıfırlamak gerekebilir ama 
            // şimdilik inspector'daki değeri koruduğunu varsayıyoruz. 
            // İdealde originalScale'i Start'ta cache'lemek lazım ama pratik çözüm: )
            
            // Tween
            Vector3 currentScale = target.localScale; 
            // Basitçe 1.2 katına çıkarıp indir
            
            target.DOScale(currentScale * 1.2f, 0.8f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
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
        if (tapToPlayText != null) tapToPlayText.DOKill();
        if (tapToNextLevelText != null) tapToNextLevelText.DOKill();

        Time.timeScale = 1f; // Zamanı başlat
        Debug.Log("Game Started!");
    }
}
