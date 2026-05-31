using UnityEngine;
using DG.Tweening; // DOTween
using TMPro; // TextMeshPro

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    [Header("UI References")]
    public GameObject tapToPlayPanel;
    public Transform tapToPlayText; // Text'in transformu (Scale efekti için)
    public Transform tapToRetryText; // Retry yazısı
    
    [Header("Market UI")]
    public GameObject levelMarketPanel; // Controls the visibility of the market buttons

    public bool IsGameActive { get; private set; } = false;
    private bool isLevelCompleteState = false; // Hangi moddayız? (Start vs Next Level)

    private float levelStartTime = 0f;



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
            
            // Show Market
            if (levelMarketPanel != null) levelMarketPanel.SetActive(true);
            
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
            // Win veya Lose panelleri açıksa (Transitioning durumundaysak), ekrana tıklanmasını yoksay.
            // Sadece butonlar (OnWinPanelOkClicked, OnLosePanelOkClicked) çalışmalı.
            if (IsLevelTransitioning) return;

            // Eğer bir UI elemanına (Buton, Panel vs.) tıklanıyorsa oyunu başlatma!
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Mobil için
            if (Input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;

            if (Input.GetMouseButtonDown(0))
            {
                // Normal "Tap to Play" başlangıcı
                StartGame();
            }
        }
    }

    public bool IsLevelTransitioning { get; private set; } = false;

    public void ShowLevelComplete()
    {
        if (IsLevelTransitioning) return; // Prevent double calls
        
        isLevelCompleteState = true;
        IsLevelTransitioning = true;

        if (LevelManager.Instance != null && FirebaseManager.Instance != null)
        {
            float duration = Time.realtimeSinceStartup - levelStartTime;
            FirebaseManager.Instance.LogLevelComplete(LevelManager.Instance.currentLevelIndex, duration);
        }

        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);
            IsGameActive = false;
            Time.timeScale = 1f; // KEEP TIME RUNNING for animations! (Critical for auto-transition timers)
            
            // Show Market
            // Hide Market (User request: Don't show in Next Level Panel)
            if (levelMarketPanel != null) levelMarketPanel.SetActive(false);

            // Yazıları Değiştir
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);

            // --- YENİ: WIN PANELI GÖSTER ---
            if (UIManager.Instance != null && LevelManager.Instance != null)
            {
                int stars = LevelManager.Instance.GetStarCount();
                UIManager.Instance.ShowWinPanel(stars);
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
         
         // Show Market
         if (levelMarketPanel != null) levelMarketPanel.SetActive(true);
         
         // Animate Play Text again
         AnimateText(tapToPlayText);
         
         // Skill satın alma sayaçlarını sıfırla (yeni level)
         if (SkillManager.Instance != null)
         {
             SkillManager.Instance.ResetLevelPurchases();
         }
         
         // Pause time until user taps
         Time.timeScale = 0f;

         // --- MISSION UI REFRESH ---
         if (UIManager.Instance != null) 
         {
             UIManager.Instance.RefreshMissionUI();
             UIManager.Instance.CloseWinPanel(); // Paneli kapat
         }
    }

    public void OnWinPanelOkClicked()
    {
        // Eğer zaten geçiş aşamasındaysak ve Win panelindeysek
        if (isLevelCompleteState)
        {
            TriggerNextLevel();
        }
    }

    public void OnLosePanelOkClicked()
    {
        if (isRetryState)
        {
            RestartLevel();
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseLosePanel();
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

    public void ShowRetry()
    {
        if (IsLevelTransitioning) return;
        
        IsLevelTransitioning = true; // Inputları kilitle (Update içinde özel kontrol ekleyeceğiz)
        isRetryState = true; // Yeni flag
        
        if (LevelManager.Instance != null && FirebaseManager.Instance != null)
        {
            float duration = Time.realtimeSinceStartup - levelStartTime;
            FirebaseManager.Instance.LogLevelFail(LevelManager.Instance.currentLevelIndex, duration);
        }
        
        IsGameActive = false;
        Time.timeScale = 0f; // Oyunu durdur

        if (tapToPlayPanel != null)
        {
            // tapToPlayPanel.SetActive(true); // Artık tam panel (LosePanel) açıyoruz
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            
            // Show Market
            // Hide Market (User request: Don't show in Retry Panel)
            if (levelMarketPanel != null) levelMarketPanel.SetActive(false);
            
            // Eski retry yazısını kapat (Artık panel var)
            if (tapToRetryText != null) tapToRetryText.gameObject.SetActive(false);

            // --- YENİ: LOSE PANELI GÖSTER ---
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowLosePanel();
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
        
        // Show Market
        if (levelMarketPanel != null) levelMarketPanel.SetActive(true);
        
        // Restart Logic via LevelManager
        if (LevelManager.Instance != null)
        {
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogLevelRetry(LevelManager.Instance.currentLevelIndex);
            }
            LevelManager.Instance.RestartCurrentLevel();
        }
        
        // Skill satın almalarını sıfırla
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ResetLevelPurchases();
        }

        // Lose sonrası oyuncuya marketten skill alma şansı ver:
        // Level'i yeniden kur, ama oyunu otomatik başlatma.
        IsGameActive = false;

        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(true);

            CanvasGroup cg = tapToPlayPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = tapToPlayPanel.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
        }

        if (tapToPlayText != null)
        {
            tapToPlayText.gameObject.SetActive(true);
            AnimateText(tapToPlayText);
        }

        if (levelMarketPanel != null) levelMarketPanel.SetActive(true);

        Time.timeScale = 0f;

        if (UIManager.Instance != null) UIManager.Instance.RefreshMissionUI();
    }

    public void StartGame()
    {
        IsGameActive = true;
        levelStartTime = Time.realtimeSinceStartup;
        
        if (tapToPlayPanel != null)
        {
            tapToPlayPanel.SetActive(false);
        }
        
        // Hide Market
        if (levelMarketPanel != null) levelMarketPanel.SetActive(false);
        
        // Tween'leri temizle (Performans için)
        if (tapToPlayText != null) tapToPlayText.DOKill();
        if (tapToRetryText != null) tapToRetryText.DOKill();

        Time.timeScale = 1f; // Zamanı başlat
        
        // --- MISSION UI REFRESH ---
        if (UIManager.Instance != null) UIManager.Instance.RefreshMissionUI();

        if (LevelManager.Instance != null && FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.LogLevelStart(LevelManager.Instance.currentLevelIndex);
        }

#if UNITY_EDITOR
        Debug.Log("Game Started!");
#endif
    }
    
}
