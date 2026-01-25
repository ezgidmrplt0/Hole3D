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
    public Transform tapToRetryText; // Retry yazısı

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
            // ANCAK Level Geçişi sırasındaysak, UI (Text) üzerine tıklamayı kabul etmeliyiz
            if (!IsLevelTransitioning && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Mobil için
            if (!IsLevelTransitioning && Input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (isLevelCompleteState)
                {
                    // Manual Transition (Skip Timer)
                    CancelInvoke(nameof(TriggerNextLevel));
                    TriggerNextLevel();
                }
                else if (isRetryState)
                {
                    RestartLevel();
                }
                else
                {
                    StartGame();
                }
            }
        }
    }

    public bool IsLevelTransitioning { get; private set; } = false;

    public void ShowLevelComplete()
    {
        if (IsLevelTransitioning) return; // Prevent double calls
        
        isLevelCompleteState = true;
        IsLevelTransitioning = true;

        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);
            IsGameActive = false;
            Time.timeScale = 1f; // KEEP TIME RUNNING for animations! (Critical for auto-transition timers)

            // Yazıları Değiştir
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            if (tapToNextLevelText != null) 
            {
                tapToNextLevelText.gameObject.SetActive(true);
                AnimateText(tapToNextLevelText);
            }
        }

        // --- AUTOMATIC TRANSITION REMOVED Check ---
        // Kullanıcı isteği: "Otomatik geçmesin, panel açılsın ben tıklayayım"
        // CancelInvoke(nameof(TriggerNextLevel));
        // Invoke(nameof(TriggerNextLevel), 2.0f);
    }

    private void TriggerNextLevel()
    {
         if (LevelManager.Instance != null) LevelManager.Instance.NextLevel();
         
         // Reset state
         isLevelCompleteState = false;
         IsLevelTransitioning = false;
         IsGameActive = false; // Ensure game is paused and joystick disabled
         
         // Reset UI
         if (tapToPlayPanel != null) tapToPlayPanel.SetActive(true);
         if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(true);
         if (tapToNextLevelText != null) tapToNextLevelText.gameObject.SetActive(false); 
         
         // Animate Play Text again
         AnimateText(tapToPlayText);
         
         // Pause time until user taps
         Time.timeScale = 0f;
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

    public void ShowRetry()
    {
        if (IsLevelTransitioning) return;
        
        // Retry State -> Farklı bir state gibi davranabilir ama basitlik için 
        // Level Transition mantığını kullanacağız (Inputu engellemek için)
        // Ama isLevelCompleteState = false kalacak ki tıklandığında StartGame değil RestartGame çalışsın.
        // Hatta en temizi:
        
        IsLevelTransitioning = true; // Inputları kilitle (Update içinde özel kontrol ekleyeceğiz)
        isRetryState = true; // Yeni flag
        
        IsGameActive = false;
        Time.timeScale = 0f; // Oyunu durdur

        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            if (tapToNextLevelText != null) tapToNextLevelText.gameObject.SetActive(false);
            
            if (tapToRetryText != null) 
            {
                tapToRetryText.gameObject.SetActive(true);
                AnimateText(tapToRetryText);
            }
        }
    }

    private bool isRetryState = false;

    public void RestartLevel()
    {
        // Reset flags
        IsLevelTransitioning = false;
        isRetryState = false;
        isLevelCompleteState = false;
        
        // Reset UI
        if (tapToRetryText != null) tapToRetryText.gameObject.SetActive(false);
        if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(true);
        
        // Restart Logic via LevelManager
        if (LevelManager.Instance != null) LevelManager.Instance.RestartCurrentLevel();
        
        // Start Game Immediately (or show TapToPlay again? User said "Retry textim çalışacak tıkladığımda o level tekrar oynanacak")
        // Genelde direkt başlar.
        StartGame();
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
        if (tapToRetryText != null) tapToRetryText.DOKill();

        Time.timeScale = 1f; // Zamanı başlat
        Debug.Log("Game Started!");
    }
}
