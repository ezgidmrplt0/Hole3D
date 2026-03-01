using UnityEngine;
using TMPro; // Standard for text in modern Unity
using DG.Tweening; // For animations

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("UI References")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI levelText;
    
    [Header("Timer")]
    public TextMeshProUGUI timerText;
    
    [Header("Zombie Counter")]
    public GameObject zombieCounterPanel;
    public TextMeshProUGUI zombieCounterText;
    
    [Header("Human Counter")]
    public TextMeshProUGUI humanCounterText;

    [Header("Win Panel")]
    public GameObject winPanel;
    public GameObject[] stars; // 3 adet yıldız objesi (indis 0, 1, 2)
    public UnityEngine.UI.Button winOkButton;

    [Header("Mission UI")]
    public GameObject missionPanel;      // The main "Mission" parent object
    public GameObject missionZombieIcon;   // The "zombieicon" object
    public GameObject missionHumanIcon;    // The "humanicon" object
    public TextMeshProUGUI missionText;    // The shared "Text (TMP) (1)" object
    
    private int lastMissionProgress = -1;
    private bool lastMissionCompleted = false;
    
    [Header("Skin Progress UI")]
    public UnityEngine.UI.Image skinProgressImage; // The filling image (Filled Type or Shader)
    public TextMeshProUGUI skinProgressText;      // Percentage text
    public GameObject skinUnlockedPopup;          // Optional pop-up for 100%
    
    [Header("Skin Browser UI (Win Panel)")]
    public UnityEngine.UI.Button skinBrowserPrevBtn;
    public UnityEngine.UI.Button skinBrowserNextBtn;
    public UnityEngine.UI.Button skinBrowserEquipBtn;
    public TextMeshProUGUI skinBrowserEquipText;  // Text on the button (Equip/Equipped/Locked)
    
    private int browserSkinIndex = 0;
    
    [Header("Rewards")]
    public Sprite coinSprite;
    public GameObject coinFlyPrefab; // Optional: If user wants a specific prefab, otherwise we'll create one.
    
    [Header("Horde Banner")]
    public GameObject hordeBannerPanel;
    public TextMeshProUGUI hordeBannerText;
    public float hordeBannerDuration = 2f;
    public TMP_FontAsset bannerFont; // Added for dynamic font assignment

    private void Start()
    {
        // AUTO-WIRE LEVEL MARKET BUTTONS
        if (levelMagnetButton == null || levelSpeedButton == null || levelShieldButton == null)
        {
            Transform tapPanel = transform.Find("TapToPanel"); // Assuming UIManager is on Canvas
            if (tapPanel == null) 
            {
                // Try finding globally if UIManager is not on Canvas
                GameObject panelObj = GameObject.Find("TapToPanel");
                if (panelObj != null) tapPanel = panelObj.transform;
            }

            if (tapPanel != null)
            {
                Transform skills = tapPanel.Find("Skills");
                if (skills != null)
                {
                    // Magnet
                    Transform tMag = skills.Find("Magnet");
                    if (tMag != null)
                    {
                        if (levelMagnetButton == null) levelMagnetButton = tMag.GetComponent<UnityEngine.UI.Button>();
                        if (levelMagnetButton == null) levelMagnetButton = tMag.GetComponentInChildren<UnityEngine.UI.Button>(); 
                        
                        if (levelMagnetPriceText == null) 
                        {
                            // Akıllı Arama: İsmi "Price" içeren Text'i bul (Örn: PriceText)
                            // Bu sayede "Title" gibi diğer yazıları yanlışlıkla almaz.
                            var allTexts = tMag.GetComponentsInChildren<TextMeshProUGUI>(true);
                            foreach(var txt in allTexts) {
                                if (txt.gameObject.name.Contains("Price") || txt.gameObject.name.Contains("Cost")) {
                                    levelMagnetPriceText = txt;
                                    break;
                                }
                            }
                            // Bulamazsa ilk bulduğu text'i al (Fallback)
                            if (levelMagnetPriceText == null) levelMagnetPriceText = tMag.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }

                    // Speed
                    Transform tSpeed = skills.Find("Speed");
                    if (tSpeed != null)
                    {
                        if (levelSpeedButton == null) levelSpeedButton = tSpeed.GetComponent<UnityEngine.UI.Button>();
                        if (levelSpeedButton == null) levelSpeedButton = tSpeed.GetComponentInChildren<UnityEngine.UI.Button>();
                        
                        if (levelSpeedPriceText == null) 
                        {
                            var allTexts = tSpeed.GetComponentsInChildren<TextMeshProUGUI>(true);
                            foreach(var txt in allTexts) {
                                if (txt.gameObject.name.Contains("Price") || txt.gameObject.name.Contains("Cost")) {
                                    levelSpeedPriceText = txt;
                                    break;
                                }
                            }
                            if (levelSpeedPriceText == null) levelSpeedPriceText = tSpeed.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }

                    // Shield
                    Transform tShield = skills.Find("Shield");
                    if (tShield != null)
                    {
                        if (levelShieldButton == null) levelShieldButton = tShield.GetComponent<UnityEngine.UI.Button>();
                        if (levelShieldButton == null) levelShieldButton = tShield.GetComponentInChildren<UnityEngine.UI.Button>();
                        
                        if (levelShieldPriceText == null) 
                        {
                            var allTexts = tShield.GetComponentsInChildren<TextMeshProUGUI>(true);
                            foreach(var txt in allTexts) {
                                if (txt.gameObject.name.Contains("Price") || txt.gameObject.name.Contains("Cost")) {
                                    levelShieldPriceText = txt;
                                    break;
                                }
                            }
                            if (levelShieldPriceText == null) levelShieldPriceText = tShield.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                }
            }
        }

        // Add Listeners - ONLY if no persistent listener exists (to avoid duplicates with Editor Tool)
        if (levelMagnetButton != null && levelMagnetButton.onClick.GetPersistentEventCount() == 0) 
            levelMagnetButton.onClick.AddListener(BuyLevelMagnet);
            
        if (levelSpeedButton != null && levelSpeedButton.onClick.GetPersistentEventCount() == 0) 
            levelSpeedButton.onClick.AddListener(BuyLevelSpeed);
            
        if (levelShieldButton != null && levelShieldButton.onClick.GetPersistentEventCount() == 0) 
            levelShieldButton.onClick.AddListener(BuyLevelShield);

        // Win Panel OK Button
        if (winOkButton != null)
        {
            winOkButton.onClick.AddListener(() => {
                if (GameFlowManager.Instance != null) GameFlowManager.Instance.OnWinPanelOkClicked();
            });
        }

        // --- SKIN BROWSER LISTENERS ---
        if (skinBrowserPrevBtn != null) skinBrowserPrevBtn.onClick.AddListener(ShowPrevSkin);
        if (skinBrowserNextBtn != null) skinBrowserNextBtn.onClick.AddListener(ShowNextSkin);
        if (skinBrowserEquipBtn != null) skinBrowserEquipBtn.onClick.AddListener(EquipBrowserSkin);

        // Başlangıçta Win panelini gizle
        if (winPanel != null) winPanel.SetActive(false);

        // Subscribe to events
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged += UpdateCoinText;
            EconomyManager.Instance.OnCoinsChanged += (amount) => UpdateSkillUI(); // Update buttons when coins change
            // Force update initial value
            // Force update initial value
            UpdateCoinText(EconomyManager.Instance.CurrentCoins);
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged += UpdateLevelText;
            LevelManager.Instance.OnZombieCountChanged += UpdateZombieCounter; // Subscribe to Zombie Count
            
            // Force update initial value (add 1 because index is 0-based)
            UpdateLevelText(LevelManager.Instance.currentLevelIndex + 1);
            
            // Initial Zombie Count update will happen when LevelManager calls StartLevel -> NotifyProgress
            // But if we missed it (Start order), we should manually check
             if (LevelManager.Instance.totalZombiesInLevel > 0)
            {
                UpdateZombieCounter(LevelManager.Instance.totalZombiesInLevel - LevelManager.Instance.currentZombiesEaten);
            }

            // Subscribe to Human Count
            LevelManager.Instance.OnHumanCountChanged += UpdateHumanCounter;
            UpdateHumanCounter(LevelManager.Instance.currentHumansEaten);

            // Subscribe to Mission
            LevelManager.Instance.OnMissionUpdated += UpdateMissionUI;
        }
        
        UpdateSkillUI();
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged -= UpdateCoinText;
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged -= UpdateLevelText;
            LevelManager.Instance.OnZombieCountChanged -= UpdateZombieCounter;
            LevelManager.Instance.OnHumanCountChanged -= UpdateHumanCounter;
            LevelManager.Instance.OnMissionUpdated -= UpdateMissionUI;
        }
    }

    private void UpdateCoinText(float amount)
    {
        if (coinText != null)
        {
            // İki ondalık basamak gösteriyoruz (Örn: 0.50)
            coinText.text = amount.ToString("F2");
        }
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = "LEVEL " + level;
        }
    }
    
    private void UpdateZombieCounter(int count)
    {
        if (zombieCounterText != null)
        {
            zombieCounterText.text = count.ToString();
        }
        
        // Opsiyonel: Eğer 0 olursa paneli gizle veya efekt yap
        // if (count <= 0 && zombieCounterPanel != null) zombieCounterPanel.SetActive(false);
        // Opsiyonel: Eğer 0 olursa paneli gizle veya efekt yap
        // if (count <= 0 && zombieCounterPanel != null) zombieCounterPanel.SetActive(false);
    }

    private void UpdateHumanCounter(int remainingHumans)
    {
        if (humanCounterText != null)
        {
            // Artık doğrudan kalan insan sayısını gösteriyoruz
            humanCounterText.text = remainingHumans.ToString();
        }
    }

    public void RefreshMissionUI()
    {
        if (LevelManager.Instance != null)
        {
            // Eğer özel bir level (Horde Mode vb.) ise görevleri gizle
            if (LevelManager.Instance.isCurrentLevelSpecial)
            {
                UpdateMissionUI(MissionType.None, 0, 0);
                return;
            }

            // Normal bir level ise mevcut görevi yansıt
            int levelDataIndex = LevelManager.Instance.normalLevelIndex % LevelManager.Instance.levels.Count;
            if (LevelManager.Instance.levels.Count > 0)
            {
                LevelData data = LevelManager.Instance.levels[levelDataIndex];
                
                // --- RESET STATE IF LEVEL CHANGED ---
                if (lastMissionProgress == -1 || !lastMissionCompleted)
                {
                    // Optionally reset if needed, but the UpdateMissionUI handles transitions
                }

                UpdateMissionUI(data.missionType, LevelManager.Instance.currentMissionProgress, data.missionTarget);
            }
        }
    }

    private void UpdateMissionUI(MissionType type, int current, int target)
    {
        // 1. Mission Panel Görünürlüğü
        // Kullanıcı isteği: Sadece oyun aktifken görünüp, TapToPlay veya Level Sonu panelinde gizlenmeli.
        bool isGameActive = (GameFlowManager.Instance != null && GameFlowManager.Instance.IsGameActive);
        bool isTransitioning = (GameFlowManager.Instance != null && GameFlowManager.Instance.IsLevelTransitioning);

        if (missionPanel != null) 
        {
            missionPanel.SetActive(type != MissionType.None && isGameActive && !isTransitioning);
        }

        if (type == MissionType.None || !isGameActive) return;

        // 2. Tamamlanma Kontrolü ("REWARD" durumu)
        bool isCompleted = current >= target;

        // --- ANIMATION LOGIC ---
        bool progressChanged = current != lastMissionProgress;
        bool newlyCompleted = isCompleted && !lastMissionCompleted;

        lastMissionProgress = current;
        lastMissionCompleted = isCompleted;

        // İkon Görünürlüğü: Eğer görev bittiyse ikonları kaldır
        if (missionZombieIcon != null) missionZombieIcon.SetActive(!isCompleted && type == MissionType.EatZombies);
        if (missionHumanIcon != null)  missionHumanIcon.SetActive(!isCompleted && type == MissionType.SaveHumans);

        // 3. Yazı Güncelleme (Shared Text)
        if (missionText != null)
        {
            if (isCompleted)
            {
                missionText.text = "REWARD";
                missionText.color = Color.green;

                if (newlyCompleted)
                {
                    // Kutlama Animasyonu (Büyüme Efekti)
                    missionText.transform.DOKill(true);
                    missionText.transform.DOPunchScale(Vector3.one * 0.4f, 0.6f, 10, 1).SetUpdate(true);
                }
            }
            else
            {
                string textValue = $"{current} / {target}";
                if (missionText.text != textValue)
                {
                    missionText.text = textValue;
                    
                    if (progressChanged && !isTransitioning)
                    {
                        // İlerleme "Punch" efekti (Hafif titreme)
                        missionText.transform.DOKill(true);
                        missionText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1).SetUpdate(true);
                    }
                }

                // Renk Geri Bildirimi
                if (type == MissionType.EatZombies)
                {
                    missionText.color = Color.white;
                }
                else if (type == MissionType.SaveHumans)
                {
                    missionText.color = (current < target) ? Color.red : Color.white;
                }
            }
        }
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText != null)
        {
            // Format as 00:00 (MM:SS)
            int totalSeconds = Mathf.CeilToInt(timeRemaining);
            if (totalSeconds < 0) totalSeconds = 0;
            
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            
            // Optional: Change color if low time
            if (totalSeconds <= 5) timerText.color = Color.red;
            else timerText.color = Color.white;
        }
    }

    public void ShowWinPanel(int starCount)
    {
        if (winPanel == null) return;

        winPanel.SetActive(true);

        // Başlangıçta tüm yıldızları kapat ve scale'lerini sıfırla
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
            {
                stars[i].SetActive(false);
                stars[i].transform.localScale = Vector3.zero;
            }
        }

        // Panel girişi animasyonu
        winPanel.transform.localScale = Vector3.zero;
        
        // --- INITIALIZE SKIN PROGRESS ---
        if (SkinManager.Instance != null && skinProgressImage != null)
        {
            float currentVal = SkinManager.Instance.GetCurrentProgress();
            
            // Shader desteği kontrolü
            if (skinProgressImage.material != null && skinProgressImage.material.HasProperty("_FillAmount"))
            {
                skinProgressImage.material.SetFloat("_FillAmount", currentVal / 100f);
            }
            else
            {
                skinProgressImage.fillAmount = currentVal / 100f;
            }
            
            if (skinProgressText != null) skinProgressText.text = $"%{(int)currentVal}";
        }
        if (skinUnlockedPopup != null) skinUnlockedPopup.SetActive(false);
        
        // Sync browser index with current skin
        if (SkinManager.Instance != null)
        {
            browserSkinIndex = SkinManager.Instance.skins.FindIndex(s => s.skinID == SkinManager.Instance.currentSkinID);
            if (browserSkinIndex < 0) browserSkinIndex = 0;
            UpdateSkinBrowserUI();
        }

        winPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).OnComplete(() => {
            // Panel açıldıktan sonra yıldızları sırayla göster
            StartCoroutine(AnimateStarsRoutine(starCount));
        });
    }

    private System.Collections.IEnumerator AnimateStarsRoutine(int starCount)
    {
        for (int i = 0; i < starCount; i++)
        {
            if (i < stars.Length && stars[i] != null)
            {
                stars[i].SetActive(true);
                // Her yıldız bir öncekinden biraz sonra ve "Pop" efektiyle gelsin
                stars[i].transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
                
                // Yıldızlar arasında küçük bir bekleme (Hypercasual klasiği)
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        // Yıldızlar bittikten sonra Skin Progress animasyonunu başlat
        yield return new WaitForSecondsRealtime(0.5f);
        yield return StartCoroutine(AnimateSkinProgressRoutine(starCount));
    }
    
    private System.Collections.IEnumerator AnimateSkinProgressRoutine(int starCount)
    {
        if (SkinManager.Instance == null || skinProgressImage == null) yield break;

        float startProgress = SkinManager.Instance.GetCurrentProgress();
        float addedProgress = starCount * 10f; // Her yıldız %10
        float targetProgress = startProgress + addedProgress;

        float displayProgress = startProgress;
        
        // DOTween ile barı ve texti oynat
        // Not: Eğer 100'ü geçerse, iki aşamalı animasyon gerekebilir (Dol -> Reset -> Tekrar dol)
        // Ama şimdilik basit tutalım: 0-100 arası dolsun.
        
        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            
            float currentVal = Mathf.Lerp(startProgress, targetProgress, t);
            
            // UI Güncelle
            float fillAmount = (currentVal % 100f) / 100f;
            if (currentVal >= 100f && startProgress < 100f && fillAmount < 0.01f) fillAmount = 1f;

            if (skinProgressImage.material != null && skinProgressImage.material.HasProperty("_FillAmount"))
            {
                skinProgressImage.material.SetFloat("_FillAmount", fillAmount);
            }
            else
            {
                skinProgressImage.fillAmount = fillAmount;
            }

            if (skinProgressText != null) skinProgressText.text = $"%{(int)currentVal}";

            yield return null;
        }

        // Final değerleri set et ve Manager'a bildir
        SkinManager.Instance.AddProgress(addedProgress);
        
        float finalProgress = SkinManager.Instance.GetCurrentProgress();
        skinProgressImage.fillAmount = finalProgress / 100f;
        if (skinProgressText != null) skinProgressText.text = $"%{(int)finalProgress}";

        // %100 olduysa Pop-up göster (Basit bir görsel efekt)
        if (targetProgress >= 100f)
        {
            if (skinUnlockedPopup != null)
            {
                skinUnlockedPopup.SetActive(true);
                skinUnlockedPopup.transform.localScale = Vector3.zero;
                skinUnlockedPopup.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            
            // Skill Manager'da zaten UnlockNextSkin çağrıldı AddProgress içinde.
            Debug.Log("[UIManager] SKIN UNLOCKED EFFECT!");
            
            // UI'ı tazele (Yeni skin açılmış olabilir)
            UpdateSkinBrowserUI();
        }
    }

    // ========== SKIN BROWSER LOGIC ==========
    public void ShowNextSkin()
    {
        if (SkinManager.Instance == null || SkinManager.Instance.skins.Count == 0) return;
        browserSkinIndex = (browserSkinIndex + 1) % SkinManager.Instance.skins.Count;
        UpdateSkinBrowserUI();
    }

    public void ShowPrevSkin()
    {
        if (SkinManager.Instance == null || SkinManager.Instance.skins.Count == 0) return;
        browserSkinIndex--;
        if (browserSkinIndex < 0) browserSkinIndex = SkinManager.Instance.skins.Count - 1;
        UpdateSkinBrowserUI();
    }

    public void UpdateSkinBrowserUI()
    {
        if (SkinManager.Instance == null || SkinManager.Instance.skins.Count == 0) return;

        var skin = SkinManager.Instance.skins[browserSkinIndex];
        bool isUnlocked = SkinManager.Instance.IsSkinUnlocked(skin.skinID);
        bool isEquipped = skin.skinID == SkinManager.Instance.currentSkinID;

        // Preview Texture
        if (skinProgressImage != null && skin.texture != null)
        {
            // Note: Skin textures are usually mapped to sprites for UI
            skinProgressImage.sprite = Sprite.Create(skin.texture, new Rect(0, 0, skin.texture.width, skin.texture.height), new Vector2(0.5f, 0.5f));
            
            // Shader effect logic: 
            if (skinProgressImage.material != null && skinProgressImage.material.HasProperty("_FillAmount"))
            {
                // In browser mode, if unlocked, show full color.
                skinProgressImage.material.SetFloat("_FillAmount", isUnlocked ? 1f : 0f);
            }
        }

        // Button State
        if (skinBrowserEquipBtn != null)
        {
            if (isEquipped)
            {
                skinBrowserEquipBtn.interactable = false;
                if (skinBrowserEquipText != null) skinBrowserEquipText.text = "EQUIPPED";
            }
            else if (isUnlocked)
            {
                skinBrowserEquipBtn.interactable = true;
                if (skinBrowserEquipText != null) skinBrowserEquipText.text = "EQUIP";
            }
            else
            {
                skinBrowserEquipBtn.interactable = false;
                if (skinBrowserEquipText != null) skinBrowserEquipText.text = "LOCKED";
            }
        }
    }

    public void EquipBrowserSkin()
    {
        if (SkinManager.Instance == null) return;
        var skin = SkinManager.Instance.skins[browserSkinIndex];
        if (SkinManager.Instance.IsSkinUnlocked(skin.skinID))
        {
            SkinManager.Instance.SelectSkin(skin.skinID);
            UpdateSkinBrowserUI();
        }
    }
    
    public void CloseWinPanel()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }
    
    // --- HORDE BANNER ---
    public void ShowHordeBanner()
    {
        StartCoroutine(HordeBannerRoutine());
    }
    
    private System.Collections.IEnumerator HordeBannerRoutine()
    {
        // Eğer panel yoksa, dinamik oluştur
        if (hordeBannerPanel == null)
        {
            CreateHordeBannerDynamically();
        }
        
        if (hordeBannerPanel != null)
        {
            hordeBannerPanel.SetActive(true);
            
            // Animasyonlu giriş (Scale ile)
            RectTransform rect = hordeBannerPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.zero;
                
                // Büyüyerek gel
                float elapsed = 0f;
                float growDuration = 0.3f;
                while (elapsed < growDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / growDuration;
                    rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.2f, t);
                    yield return null;
                }
                
                // Biraz küçül (bounce efekti)
                elapsed = 0f;
                float shrinkDuration = 0.15f;
                while (elapsed < shrinkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shrinkDuration;
                    rect.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
                    yield return null;
                }
            }
            
            // Ekranda bekle
            yield return new WaitForSeconds(hordeBannerDuration);
            
            // Küçülerek git
            if (rect != null)
            {
                float elapsed = 0f;
                float shrinkDuration = 0.2f;
                while (elapsed < shrinkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shrinkDuration;
                    rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                    yield return null;
                }
            }
            
            hordeBannerPanel.SetActive(false);
        }
    }
    
    private void CreateHordeBannerDynamically()
    {
        // Ana UI Canvas'ı bul (Screen Space - Overlay veya Camera)
        // World Space canvas'ları (Hole'un canvas'ı gibi) atla
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Canvas mainCanvas = null;
        
        foreach (Canvas c in allCanvases)
        {
            // World Space değilse ve renderMode Screen Space ise ana canvas
            if (c.renderMode != RenderMode.WorldSpace)
            {
                mainCanvas = c;
                break;
            }
        }
        
        // Hiç Screen Space canvas bulunamadıysa, World Space olmayan ilk canvas'ı kullan
        if (mainCanvas == null)
        {
            // Son çare: Yeni bir canvas oluştur
            GameObject canvasObj = new GameObject("HordeBannerCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100; // En üstte görünsün
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Panel oluştur
        hordeBannerPanel = new GameObject("HordeBannerPanel");
        hordeBannerPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = hordeBannerPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(800, 200);
        
        // Arkaplan YOK - sadece yazı görünsün
        // (Image component eklemiyoruz)
        
        // Text oluştur
        GameObject textObj = new GameObject("HordeText");
        textObj.transform.SetParent(hordeBannerPanel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        hordeBannerText = textObj.AddComponent<TextMeshProUGUI>();
        hordeBannerText.text = "HORDE!";
        if (bannerFont != null) hordeBannerText.font = bannerFont; // Assign Custom Font
        hordeBannerText.fontSize = 100; // Daha büyük
        hordeBannerText.fontStyle = FontStyles.Bold;
        hordeBannerText.color = Color.red;
        hordeBannerText.alignment = TextAlignmentOptions.Center;
        hordeBannerText.enableWordWrapping = false;
        
        // Güçlü outline efekti (okunabilirlik için)
        hordeBannerText.outlineWidth = 0.3f;
        hordeBannerText.outlineColor = Color.black;
        
        hordeBannerPanel.SetActive(false);
    }

    [Header("Panels")]
    public GameObject marketPanel;

    public void OpenMarket()
    {
        if (marketPanel != null)
        {
            marketPanel.SetActive(true);
            // Oyunun geri planını durdurmak isterseniz: Time.timeScale = 0;
        }
    }

    public void CloseMarket()
    {
        if (marketPanel != null)
        {
            marketPanel.SetActive(false);
            // Time.timeScale = 1;
        }
    }

    // Shop butonlarına (Unity Inspector'dan) bu fonksiyonu verip, parametre olarak coin miktarını girebilirsiniz.
    public void BuyCoinPack(int amount)
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCoins(amount);
            // Burada isteğe bağlı olarak satın alma sesi veya efekti eklenebilir.
        }
    }

    // [Header("Skill Upgrade UI (Store)")] REMOVED
    
    [Header("Level Market (Single Use)")]
    public UnityEngine.UI.Button levelMagnetButton;
    public TextMeshProUGUI levelMagnetPriceText;
    
    public UnityEngine.UI.Button levelSpeedButton;
    public TextMeshProUGUI levelSpeedPriceText;
    
    public UnityEngine.UI.Button levelShieldButton;
    public TextMeshProUGUI levelShieldPriceText;
    
    [Header("Active Skill Indicator (In-Game)")]
    public GameObject activeSkillPanel;
    public UnityEngine.UI.Image activeSkillIcon;
    public TextMeshProUGUI activeSkillTimerText;
    
    [Header("Skill Icons")]
    public Sprite magnetIcon;
    public Sprite speedIcon;
    public Sprite shieldIcon;

    private void UpdateSkillUI()
    {
        if (SkillManager.Instance == null) return;
        
        // Helper to update a single skill button
        void UpdateButton(UnityEngine.UI.Button btn, TextMeshProUGUI priceText, SkillType type, int price)
        {
            if (btn == null) return;

            bool isPurchased = SkillManager.Instance.IsSkillPurchased(type);
            bool canAfford = false;
            if (EconomyManager.Instance != null) canAfford = EconomyManager.Instance.CurrentCoins >= price;

            if (isPurchased)
            {
                // DURUM 1: SATIN ALINDI -> KİLİTLİ VE "SOLD" YAZISI
                btn.interactable = false;
                if (priceText != null) 
                {
                    priceText.text = "SOLD";
                    priceText.color = Color.green; // Opsiyonel: Satıldı rengi
                }
            }
            else
            {
                // DURUM 2: HENÜZ ALINMADI
                if (priceText != null) 
                {
                    priceText.text = price.ToString();
                }

                if (canAfford)
                {
                    // YETERLİ PARA VAR -> AKTİF
                    btn.interactable = true;
                    if (priceText != null) priceText.color = Color.white;
                }
                else
                {
                    // YETERLİ PARA YOK -> PASİF (Ama satın alındığı için değil, para yok diye)
                    btn.interactable = false;
                    if (priceText != null) priceText.color = Color.red;
                }
            }
        }

        UpdateButton(levelMagnetButton, levelMagnetPriceText, SkillType.Magnet, SkillManager.Instance.magnetPrice);
        UpdateButton(levelSpeedButton, levelSpeedPriceText, SkillType.Speed, SkillManager.Instance.speedPrice);
        UpdateButton(levelShieldButton, levelShieldPriceText, SkillType.Shield, SkillManager.Instance.shieldPrice);
    }
    
    private void Update()
    {
        // Aktif skill göstergesini güncelle
        UpdateActiveSkillIndicator();
    }
    
    private void UpdateActiveSkillIndicator()
    {
        if (SkillManager.Instance == null || activeSkillPanel == null) return;
        
        // En uzun süre kalan aktif skill'i bul
        SkillType? activeSkill = null;
        float maxTime = 0f;
        
        foreach (SkillType type in System.Enum.GetValues(typeof(SkillType)))
        {
            if (SkillManager.Instance.IsSkillActive(type))
            {
                float remaining = SkillManager.Instance.GetRemainingTime(type);
                // 900000f is our magic logic for "Permanent"
                if (remaining > maxTime)
                {
                    maxTime = remaining;
                    activeSkill = type;
                }
            }
        }
        
        if (activeSkill.HasValue)
        {
            activeSkillPanel.SetActive(true);
            
            // İkon ayarla
            if (activeSkillIcon != null)
            {
                activeSkillIcon.sprite = activeSkill.Value switch
                {
                    SkillType.Magnet => magnetIcon,
                    SkillType.Speed => speedIcon,
                    SkillType.Shield => shieldIcon,
                    _ => null
                };
            }
            
            // Timer göster
            if (activeSkillTimerText != null)
            {
                // Permanent check
                if (maxTime > 900000f) 
                {
                    activeSkillTimerText.text = "∞"; // Infinite symbol
                }
                else
                {
                    activeSkillTimerText.text = maxTime.ToString("F1") + "s";
                }
            }
        }
        else
        {
            activeSkillPanel.SetActive(false);
        }
    }

    // Upgrade methods REMOVED

    // --- LEVEL MARKET METHODS ---
    // Assign these to the buttons in the Inspector
    
    public void BuyLevelMagnet()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuyMagnet();
            UpdateSkillUI(); // Update buttons immediately
        }
    }
    
    public void BuyLevelSpeed()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuySpeed();
            UpdateSkillUI();
        }
    }
    
    public void BuyLevelShield()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.BuyShield();
            UpdateSkillUI();
        }
    }

    private void OnEnable()
    {
        UpdateSkillUI();
        
        // Skill değişikliklerini dinle
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnUpgradesChanged += UpdateSkillUI;
        }
    }
    
    private void OnDisable()
    {
        // Event'ten çık (Memory leak önleme)
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnUpgradesChanged -= UpdateSkillUI;
        }
    }
}
