using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening; // Import DOTween

public class HoleMechanics : MonoBehaviour
{
    // Listeye çevirdik ki hem Zombi hem İnsan hem SkillPickup girebilsin
    public System.Collections.Generic.List<string> targetTags = new System.Collections.Generic.List<string> { "Zombie", "Human", "SkillPickup" };
    
    // --- DUPLICATE PREVENTION ---
    // Aynı objenin birden fazla collider'ı olabilir, çift sayımı önle
    private HashSet<GameObject> objectsBeingProcessed = new HashSet<GameObject>();

    [Header("Feedback Effects")]
    public float shakeDuration = 0.15f;
    public float shakeStrength = 0.4f;
    public int shakeVibrato = 20;
    [Tooltip("Art arda yeme sayılabilmesi için gereken maksimum süre (saniye)")]
    public float comboTimeWindow = 0.8f;
    [Tooltip("En az kaçıncı comboda ekran sallanmaya başlasın?")]
    public int minComboShake = 2; // 2. ve sonraki seri yiyişlerde salla
    
    private float lastEatTime = -10f;
    private int currentCombo = 0;
    private Camera mainCam;

    [Header("UI")]
    public TMP_Text levelText;
    public TMP_FontAsset damageFont; // Added for dynamic font assignment

    [Header("Animation Settings")]
    public float fallDuration = 0.5f;
    // public float sinkDepth = 2f; // Removed duplicate
    public float minScale = 0.1f;

    // Başlangıç değerleri (Reset için)
    private Vector3 initialScale;
    private Vector3 initialVisualsScale;
    private Vector3 initialCameraLocalPos;
    private Quaternion initialCameraLocalRot;
    private bool cameraIsChildOfHole = false;

    // Eski ayarlar temizlendi

    [Header("Level System")]
    public int holeLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 15; // Slowed down: Start with 15 (was 5)

    // Görselleri bul (Public yaptık ki editörden de sürükleyebilesin)
    public HoleVisuals visuals;
    
    private Collider[] holeCols;
    private HoleMaskController maskController;
    private ObstructionFader obstructionFader;
    
    [Header("Effects")]
    public ParticleSystem xpParticles; // Eski referans (kalsın)
    private ParticleSystem eatVFX; // Kod ile oluşturulan efekt

    private void Start()
    {
        // Başlangıç scale'lerini kaydet (reset için)
        initialScale = transform.localScale;
        
        ResetLevelState(); 
        
        // --- HYPERCASUAL VFX SETUP ---
        SetupHypercasualVFX();

        mainCam = Camera.main;
        
        // Kamera başlangıç pozisyonunu kaydet (Fever Mode reset için)
        if (mainCam != null)
        {
            cameraIsChildOfHole = mainCam.transform.IsChildOf(transform);
            if (cameraIsChildOfHole)
            {
                initialCameraLocalPos = mainCam.transform.localPosition;
                initialCameraLocalRot = mainCam.transform.localRotation;
            }
        }

        // Deliğin kendi colliderlarını (Siyah kısım, çerçeve vb.) hafızaya al
        holeCols = GetComponentsInChildren<Collider>();

        // Görselleri bulmaya çalış (Otomatik - Daha Güçlü Arama)
        if (visuals == null)
        {
            // 1. Önce çocuklara bak
            visuals = GetComponentInChildren<HoleVisuals>();
            
            // 2. Kendine bak
            if (visuals == null) visuals = GetComponent<HoleVisuals>();
            
            // 3. Sahneye bak (Tek kişilik oyun olduğu için güvenli)
            if (visuals == null) visuals = FindObjectOfType<HoleVisuals>();
        }

        if (visuals == null)
        {
            Debug.LogError("HoleMechanics: HoleVisuals scripti bulunamadı! Lütfen Inspectordan 'visuals' kutucuğuna atama yapın.");
        }
        else
        {
            // Visuals'ın başlangıç scale'ini kaydet
            initialVisualsScale = visuals.transform.localScale;
            
            // Bulduysak barı başlangıç durumuna (0) getir veya mevcut XP durumunu yansıt
            visuals.UpdateLocalProgress((float)currentXP / xpToNextLevel);
        }
        
        // MaskController'ı bul (Shader'a radius gönderen script)
        if (maskController == null)
        {
            maskController = GetComponentInChildren<HoleMaskController>();
            if (maskController == null) maskController = GetComponent<HoleMaskController>();
            if (maskController == null) maskController = FindObjectOfType<HoleMaskController>();
        }
        
        if (maskController == null)
        {
            Debug.LogWarning("HoleMechanics: HoleMaskController bulunamadı! Deliğin iç kısmı büyümeyebilir.");
        }

        // Texti güncelle
        UpdateLevelText();

        if (maskController != null)
        {
            // Initial radius based on scale
            maskController.SetRadius(voidRadius * transform.localScale.x);
        }
        
        // --- AUTO-ADD OBSTRUCTION FADER ---
        // Kullanıcı isteği: objelerin içinden geçerken transparan olması
        obstructionFader = GetComponent<ObstructionFader>();
        if (obstructionFader == null)
        {
            obstructionFader = gameObject.AddComponent<ObstructionFader>();
        }

        // --- SETUP XP PARTICLES ---
        /*
        if (xpParticles == null)
        {
            CreateDefaultXPParticles();
        }
        */
    }

    // --- RESET LOGIC ---
    // Yeni level başladığında veya fever bittiğinde çağrılmalı
    public void ResetLevelState()
    {
        // Fever modunu kapat
        isFeverMode = false;
        
        // Tüm DOTween animasyonlarını iptal et
        DOTween.Kill(transform);
        if (visuals != null) DOTween.Kill(visuals.transform);
        if (mainCam != null) DOTween.Kill(mainCam.transform);
        
        // Scale'i başlangıç değerine döndür
        if (initialScale != Vector3.zero)
        {
            transform.localScale = initialScale;
        }
        else
        {
            // Fallback: 1,1,1
            transform.localScale = Vector3.one;
        }
        
        // Visuals scale'ini de resetle
        if (visuals != null)
        {
            if (initialVisualsScale != Vector3.zero)
            {
                visuals.transform.localScale = initialVisualsScale;
            }
            else
            {
                visuals.transform.localScale = Vector3.one;
            }
        }
        
        // KAMERA RESET - Fever Mode sonrası bozulan açıyı düzelt
        if (mainCam != null && cameraIsChildOfHole)
        {
            mainCam.transform.localPosition = initialCameraLocalPos;
            mainCam.transform.localRotation = initialCameraLocalRot;
        }
        
        // Mask radius'u da resetle
        if (maskController != null)
        {
            float targetRadius = voidRadius * transform.localScale.x;
            maskController.SetRadius(targetRadius);
        }
        
        // Hole level ve XP'yi resetle (opsiyonel - her level başında sıfırdan başlamak istiyorsan)
        // holeLevel = 1;
        // currentXP = 0;
        
        // ObstructionFader'ı aktif et
        if (obstructionFader != null)
        {
            obstructionFader.enabled = true;
        }
        
        Debug.Log($"HoleMechanics: Reset completed. Scale: {transform.localScale}, Camera reset: {cameraIsChildOfHole}");
    }

    private void UpdateLevelText()
    {
        if (levelText != null) levelText.text = "Lvl " + holeLevel;
    }

    // --- PROGRAMMATIC VFX GENERATOR ---
    private void SetupHypercasualVFX()
    {
        // 1. Obje Yarat
        GameObject vfxObj = new GameObject("HypercasualEatVFX");
        vfxObj.transform.SetParent(this.transform);
        vfxObj.transform.localPosition = Vector3.up * 0.1f; 

        eatVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = eatVFX.main;
        var emission = eatVFX.emission;
        var shape = eatVFX.shape;
        var renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        var sizeOverLifetime = eatVFX.sizeOverLifetime;
        var limitVelocity = eatVFX.limitVelocityOverLifetime;
        var rotationOverLifetime = eatVFX.rotationOverLifetime;

        // 2. MAIN AYARLAR (CONFETTI BLAST)
        eatVFX.Stop(); // Ensure stopped before setting properties
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f); // Daha hızlı patlasın!
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f); // Biraz daha büyük parçalar
        main.gravityModifier = 2.0f; // Hızlı düşsünler
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        // 3. MOTION (Sürtünme hissi - Pop etkisi)
        limitVelocity.enabled = true;
        limitVelocity.dampen = 0.2f; // Havada fren yapıp süzülme hissi
        limitVelocity.limit = 0f; 

        // 4. EMISSION
        emission.enabled = false; 

        // 5. SHAPE (Geniş Koni)
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 60f; // Daha geniş saçılma
        shape.radius = 0.5f;

        // 6. ROTATION (Dönerek düşsünler - Konfeti hissi)
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

        // 7. VISUALS (Kare Texture ve Shrink)
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f); // Pop-in (Sıfırdan başla)
        curve.AddKey(0.2f, 1.0f); // Hızla büyü
        curve.AddKey(1.0f, 0.0f); // Sonunda sıfır
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        // Texture oluştur (KARE - Confetti için)
        Texture2D confetiTex = GenerateSquareTexture();
        
        // Shader ve Material
        // Shader ve Material
        // "Legacy Shaders/Particles/Alpha Blended" yerine "Particles/Standard Unlit" kullanıyoruz.
        // Bu sayede renkler daha "Solid" ve parlak görünecek, transparanlık sorunu çözülecek.
        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit")); 
        particleMat.enableInstancing = true;
        
        // Render Mode ayarları (Opaque/Cutout gibi davranması için)
        particleMat.SetFloat("_Mode", 2); // Transparent
        particleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        particleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        particleMat.SetInt("_ZWrite", 0);
        particleMat.EnableKeyword("_ALPHABLEND_ON");
        particleMat.renderQueue = 3000;
        
        particleMat.mainTexture = confetiTex;
        renderer.material = particleMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }
    
    // Runtime'da basit bir KARE texture oluşturur (Confetti)
    private Texture2D GenerateSquareTexture()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] colors = new Color[size * size];
        
        // Düz beyaz kare (SOLID - Transparan kenar YOK)
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white; // Tam opak beyaz
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }
    


    // Runtime'da basit bir yuvarlak texture oluşturur
    private Texture2D GenerateCircleTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - dist) / 2f); // Yumuşak kenarlı (Anti-aliasing benzeri)
                colors[y * size + x] = new Color(1, 1, 1, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    public void PlayEatEffect(Vector3 pos, Color color)
    {
        if (eatVFX != null)
        {
            eatVFX.transform.position = pos;
            
            var main = eatVFX.main;
            main.startColor = color; // Direkt renk ata
            
            // Patlat! (Büyük parça sayısı - Confetti Shower)
            eatVFX.Emit(40);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- ROOT OBJE BUL (Child collider'lar için) ---
        // Zombilerin birden fazla collider'ı olabilir, hepsini aynı root'a bağla
        GameObject rootObject = other.gameObject;
        
        // Eğer ZombieAI veya CharacterAI parent'ta ise, onu root say
        CharacterAI charAI = other.GetComponentInParent<CharacterAI>();
        if (charAI != null) rootObject = charAI.gameObject;
        
        // SkillPickup için de aynısı
        SkillPickup skillPickupParent = other.GetComponentInParent<SkillPickup>();
        if (skillPickupParent != null) rootObject = skillPickupParent.gameObject;
        
        // --- DUPLICATE KONTROLÜ ---
        if (objectsBeingProcessed.Contains(rootObject))
        {
            return; // Bu obje zaten işleniyor, atla
        }
        
        // --- SKILL PICKUP KONTROLÜ (Tag'a bağımlı değil, component'e bakıyor) ---
        SkillPickup skillPickup = other.GetComponent<SkillPickup>();
        if (skillPickup == null) skillPickup = other.GetComponentInParent<SkillPickup>();
        
        if (skillPickup != null)
        {
            Debug.Log($"[HoleMechanics] Skill Pickup algılandı: {skillPickup.skillType}");
            StartCoroutine(PhysicsFall(other.gameObject));
            return;
        }
        
        // --- FEVER MODE CHECK ---
        bool canEat = targetTags.Contains(other.tag);
        
        if (isFeverMode)
        {
            // Fever modunda "Ground" hariç her şeyi yiyebiliriz
            // Ancak LevelManager bina/taş gibi objeleri de "Ground" olarak etiketliyor.
            // Bu yüzden "Ground" tagine bakmak yerine, objenin İSMİNE bakarak "Zemin" olup olmadığını anla.
            if (!other.CompareTag("MainCamera"))
            {
                string n = other.name.ToLower();
                // Gerçek zemin genellikle "Floor", "Plane" veya "Ground" ismini taşır.
                bool isActualFloor = n.Contains("floor") || n.Contains("plane") || n.Contains("zemin") || n.Equals("ground");

                if (!isActualFloor)
                {
                    canEat = true;
                }
            }
        }

        // Listede var mı kontrol et veya Fever Mode aktif mi
        if (canEat)
        {
            // --- TRAPPED ZOMBIE CHECK ---
            // Kafesteki zombiler yenemez!
            ZombieAI zAI = other.GetComponent<ZombieAI>();
            if (zAI == null) zAI = other.GetComponentInParent<ZombieAI>();
            
            if (zAI != null && zAI.isTrapped)
            {
                // Zombi kafeste, yeme!
                return;
            }

            // Level kontrolü kaldırıldı - Hole her zaman tüm zombileri yiyebilir
            StartCoroutine(PhysicsFall(other.gameObject));
        }
    }

    [Header("Fever Mode")]
    public bool isFeverMode = false;
    private Vector3 preFeverScale;

    public void ActivateFeverMode(float duration, System.Action onComplete)
    {
        if (isFeverMode) return;
        StartCoroutine(FeverModeRoutine(duration, onComplete));
    }

    private IEnumerator FeverModeRoutine(float duration, System.Action onComplete)
    {
        isFeverMode = true;
        preFeverScale = transform.localScale;

        // Fever Başlangıcı: Transparanlık efektini KAPAT (Her şey net görünsün)
        if (obstructionFader != null)
        {
            obstructionFader.ForceRestoreAll();
            obstructionFader.enabled = false;
        }

        // 1. DEVASA BÜYÜME (Daha kontrollü - 3.5 Kat)
        // 6x yapınca tüm haritayı tek karede yiyor ve kötü görünüyor. 3.5x daha dengeli.
        float feverMultiplier = 3.5f;
        Vector3 targetScale = preFeverScale * feverMultiplier; 
        
        // Hızlıca büyü (Biraz daha yavaş ki oyuncu hissetsin)
        float growDuration = 2.0f;
        transform.DOScale(targetScale, growDuration).SetEase(Ease.OutElastic);
        
        // Görselleri de büyüt
        if (visuals != null && visuals.transform.parent != transform)
        {
            visuals.transform.DOScale(visuals.transform.localScale * feverMultiplier, growDuration).SetEase(Ease.OutElastic);
        }

        // Mask Radius Update (Shader için)
        if (maskController != null)
        {
             float targetRadius = voidRadius * targetScale.x;
             DOTween.To(() => maskController.currentRadius, x => maskController.currentRadius = x, targetRadius, growDuration);
        }

        // Detect Camera if Child
        Vector3? originalCamPos = null;
        if (mainCam != null && mainCam.transform.IsChildOf(transform))
        {
             originalCamPos = mainCam.transform.localPosition;
             // Hole büyüyünce kamera çok uzaklaşıyor. Yakınlaştırmak için local pozisyonu küçültüyoruz.
             // 3.5x büyüdüğü için, 0.4x e çekersek -> Toplamda 1.4x uzaklaşmış olur (Daha makul).
             mainCam.transform.DOLocalMove(originalCamPos.Value * 0.4f, growDuration).SetEase(Ease.OutElastic);
        }

        SpawnFloatingText("FEVER MODE!", Color.red);
        SpawnFloatingText("EAT EVERYTHING!", Color.yellow);

        // 2. Bekle (Yıkım Zamanı - OYUNCU HAREKET EDEBİLİR)
        // Burada paneli AÇMIYORUZ. Oyuncu 10 saniye boyunca serbestçe gezip yıkım yapacak.
        yield return new WaitForSeconds(duration);

        // 3. Küçül ve Normale Dön (Fever Bitti)
        transform.DOScale(preFeverScale, 0.5f).SetEase(Ease.InBack);
        
        if (visuals != null && visuals.transform.parent != transform)
        {
            visuals.transform.DOScale(visuals.transform.localScale / feverMultiplier, 0.5f).SetEase(Ease.InBack);
        }
        
        if (maskController != null)
        {
             float targetRadius = voidRadius * preFeverScale.x;
             DOTween.To(() => maskController.currentRadius, x => maskController.currentRadius = x, targetRadius, 0.5f);
        }

        // Kamera Reset
        if (originalCamPos.HasValue)
        {
             mainCam.transform.DOLocalMove(originalCamPos.Value, 0.5f).SetEase(Ease.InBack);
        }

        yield return new WaitForSeconds(0.5f); // Animasyon bitsin

        isFeverMode = false;

        // Fever Bitişi: Transparanlık efektini geri AÇ
        if (obstructionFader != null)
        {
            obstructionFader.enabled = true;
        }
        
        // Callback çağır (Artık paneli açabiliriz)
        onComplete?.Invoke();
    }

    [Header("Physics Settings")]
    public float voidRadius = 1.0f; 
    public float sinkDepth = 3f;
    public float pullForce = 150f; // New strong pull force
    public float rotationSpeed = 360f; // Degrees per second

    IEnumerator PhysicsFall(GameObject victim)
    {
        // --- ROOT OBJE BUL VE İŞARETLE ---
        GameObject rootObject = victim;
        CharacterAI charAI = victim.GetComponentInParent<CharacterAI>();
        if (charAI != null) rootObject = charAI.gameObject;
        SkillPickup skillPickup = victim.GetComponentInParent<SkillPickup>();
        if (skillPickup != null) rootObject = skillPickup.gameObject;
        
        // İşlenen objelere ekle (çift sayımı önle)
        objectsBeingProcessed.Add(rootObject);
        
        // --- LEVEL KONTROLÜ İPTAL ---
        // Kullanıcı isteği: Büyük zombiler de yensin ve çok XP versin.
        /*
        if (victim.CompareTag("Zombie"))
        {
            ZombieAI zombieInfo = victim.GetComponent<ZombieAI>();
            if (zombieInfo != null && holeLevel < zombieInfo.level) yield break; 
        }
        */

        Transform vTransform = victim.transform;
        
        // --- AŞAMA 1: KENAR KONTROLÜ (BEKLEME) ---
        // Obje deliğin merkezine (siyah kısmına) tam girene kadar bekle
        // FEVER MODE: Bekleme yapma! Direkt yut.
        if (!isFeverMode)
        {
            while (vTransform != null)
            {
                // Sadece X-Z düzleminde mesafe (Yükseklik önemsiz)
                float dist = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), 
                                              new Vector2(vTransform.position.x, vTransform.position.z));

                // Scale arttıkça radius artar. Biraz tolerans (0.8f) ekledik ki tam kenarda düşmesin, hafif içeri girince düşsün.
                float scaledVoidRadius = voidRadius * transform.localScale.x * 0.9f;

                if (dist < scaledVoidRadius)
                {
                    break; // Düşüş Başlasın!
                }
                yield return null; 
            }
        }

        if (vTransform == null) yield break;

        // --- AŞAMA 2: DÜŞÜŞ BAŞLIYOR ---
        
        // 1. AI ve Kontrolcüleri Kapat
        CharacterAI ai = victim.GetComponent<CharacterAI>();
        if (ai != null) ai.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.velocity = Vector3.zero; // Anlık durdur
            agent.isStopped = true;
            agent.enabled = false;
        }
        
        // SkillPickup için özel işlem
        SkillPickup victimSkillPickup = victim.GetComponent<SkillPickup>();
        if (victimSkillPickup != null)
        {
            victimSkillPickup.OnSwallowStart();
        }

        Animator anim = victim.GetComponent<Animator>();
        if (anim != null) 
        {
            anim.enabled = false; // Ragdoll aktifleşsin veya donup düşsün.
            // Eğer "Falling" animasyonu varsa o oynatılabilir: anim.SetTrigger("Fall");
        }

        // 2. Fizik Motorunu Devreye Sok
        Rigidbody rb = victim.GetComponent<Rigidbody>();
        
        // --- NON-CONVEX MESHCOLLIDER FIX ---
        // MeshCollider varsa ve convex değilse, convex yap veya kaldır
        MeshCollider[] meshCols = victim.GetComponentsInChildren<MeshCollider>();
        foreach (var mc in meshCols)
        {
            if (mc != null && !mc.convex)
            {
                // Basit objeler için convex yapılabilir, karmaşık olanlar için kaldır
                try
                {
                    mc.convex = true;
                }
                catch
                {
                    // Convex yapılamıyorsa, trigger yap (fizik çarpışması olmaz ama hata da vermez)
                    mc.isTrigger = true;
                }
            }
        }
        
        if (rb == null) 
        {
            // Fever Modunda her şeye RB ekle ki düşebilsin
            rb = victim.AddComponent<Rigidbody>(); 
        }

        rb.isKinematic = false;
        rb.useGravity = true; 
        rb.constraints = RigidbodyConstraints.None;
        rb.drag = 0f; // Hızlı düşsün
        rb.angularDrag = 0.5f;
        
        // Rastgele bir ilk dönüş hızı ver (Tumble)
        rb.angularVelocity = Random.insideUnitSphere * 10f; 

        // SCATTER EFFECT (Dağılma Efekti)
        // Özellikle Fever modunda objeler kaotik şekilde savrulsun
        if (isFeverMode)
        {
            // Rastgele fırlat (Hafifçe havaya ve yana doğru)
            rb.velocity = Random.insideUnitSphere * 5f; 
            // Güçlü bir dönüş ver (Tumble)
            rb.angularVelocity = Random.insideUnitSphere * 10f;
        } 

        // 3. Yerle Çarpışmayı Kes (ÖNEMLİ: Obje yerin içinden geçebilmeli)
        Collider[] victimCols = victim.GetComponentsInChildren<Collider>();
        Collider[] nearbyGrounds = Physics.OverlapSphere(vTransform.position, 10f, LayerMask.GetMask("Default", "Ground", "Environment")); 
        
        foreach (var envCol in nearbyGrounds)
        {
            // Kendisi veya Delik değilse çarpışmayı kapat
            if (envCol.transform.root != vTransform.root && !envCol.transform.IsChildOf(this.transform))
            {
                foreach (var vCol in victimCols) Physics.IgnoreCollision(vCol, envCol, true);
            }
        }

        // 4. KÜÇÜLME EFEKTİ İPTAL (Burada hemen yapma)
        // vTransform.DOScale(Vector3.zero, 1.0f).SetEase(Ease.InBack);
        
        // 4. KÜÇÜLME EFEKTİ (Girdap etkisi)
        // Düşerken küçülerek yok olsun
        vTransform.DOScale(Vector3.zero, 1.0f).SetEase(Ease.InBack);

        // --- AŞAMA 3: AKTİF ÇEKİM GÜCÜ (Physics Loop) ---
        float timer = 0f;
        
        // 3D Çukur için limitleri ayarla
        float bottomLimit = transform.position.y - sinkDepth;
        
        while (timer < 3f && vTransform != null) 
        {
            timer += Time.deltaTime;

            // Merkeze ve Aşağıya Doğru Çek
            Vector3 centerBottom = transform.position + Vector3.down * sinkDepth;
            Vector3 diff = centerBottom - vTransform.position;
            
            // --- INFINITE FORCE KORUMASI ---
            // Eğer mesafe çok küçükse veya NaN ise, direction sıfırla
            float dist = diff.magnitude;
            Vector3 direction = Vector3.down; // Varsayılan
            if (dist > 0.01f && !float.IsNaN(dist) && !float.IsInfinity(dist))
            {
                direction = diff / dist; // normalized
            }
            
            // Rigidbody hala geçerli mi kontrol et
            if (rb != null && !float.IsNaN(direction.x))
            {
                // Güçlü Çekim
                rb.AddForce(direction * pullForce * Time.deltaTime * 60f, ForceMode.Acceleration);
                rb.AddTorque(Vector3.up * rotationSpeed * Time.deltaTime, ForceMode.Force);
            }

            // Çukurun dibine yaklaştı mı?
            if (vTransform.position.y < bottomLimit + 0.5f) // Dibe yaklaştı
            {
                // Kullanıcının isteği: "0.1 salise sonra yok olsun"
                // Önce küçültelim ki "yok oluş" pop diye olmasın
                vTransform.DOScale(Vector3.zero, 0.1f);
                yield return new WaitForSeconds(0.1f);
                break; // Ve döngü biter -> Destroy çağrılır
            }
            yield return null;
        }

        // --- SONUÇ: YOK ET ve PUAN VER ---
        if (vTransform != null)
        {
            // Puanlama Mantığı
            ProcessEatenObject(victim);

            // Efekt (Varsa partikül vs eklenebilir)
            Destroy(victim);
        }
        
        // Listeden çıkar (artık işlenmedi)
        objectsBeingProcessed.Remove(rootObject);
    }

    void ProcessEatenObject(GameObject victim)
    {
        // --- HYPERCASUAL PARTICLE EFFECT ---
        // Kurbanın rengini bulmaya çalış
        Color victimColor = Color.white;
        Renderer r = victim.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            if (r.material.HasProperty("_Color")) victimColor = r.material.color;
            else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color")) victimColor = r.sharedMaterial.color;
        }

        // Zombi ise özel renk (Neon Yeşil - Confetti)
        if (victim.CompareTag("Zombie")) victimColor = new Color(0.2f, 1.0f, 0.1f); // Neon Green
        // İnsan ise Hot Pink / Red
        else if (victim.CompareTag("Human")) victimColor = new Color(1.0f, 0.2f, 0.6f); // Hot Pink

        // Efekti, delik yüzeyinde oynat (Dibin dibinde değil)
        Vector3 surfacePos = new Vector3(victim.transform.position.x, transform.position.y + 0.2f, victim.transform.position.z);
        // 1. Partikül Patlaması
        PlayEatEffect(surfacePos, victimColor);

        // --- FEVER MODE BONUS ---
        if (isFeverMode)
        {
            // Fever modunda yenilen HER ŞEY için ekstra altın
            if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(5);
            SpawnFloatingText("+5 Gold", Color.yellow);
        }

        // --- ZOMBİ TESPİTİ ---
        // Önce tag kontrol et, sonra component bazlı yedek kontrol (tag atanmamış olabilir)
        ZombieAI zombieAI = victim.GetComponent<ZombieAI>();
        bool isZombie = victim.CompareTag("Zombie") || zombieAI != null;
        
        if (isZombie)
        {
            int gainedXP = 1;
            if (zombieAI != null)
            {
                gainedXP = zombieAI.level; // Level kadar XP ver
            }
            
            // Fever Modunda XP Double!
            if (isFeverMode) gainedXP *= 2;

            currentXP += gainedXP;
            
            // Floating Text (+XP Green)
            SpawnFloatingText("+" + gainedXP, Color.green);

            // Eski effect iptal (Yenisi yukarıda var)
            // if (xpParticles != null) xpParticles.Play();

            // --- COMBO VIBRATION (SERİ YEME) ---
            // Eğer son zombiden bu yana geçen süre az ise combo yap
            if (Time.time - lastEatTime < comboTimeWindow)
            {
                currentCombo++;
            }
            else
            {
                currentCombo = 1; // Süre geçtiyse sıfırla (bu yeni serinin ilk elemanı)
            }
            lastEatTime = Time.time;

            // Eğer combo sayısı yeterliyse salla (2 ve daha fazlası)
            if (currentCombo >= minComboShake)
            {
                 if (mainCam != null)
                {
                    // Şiddeti combo arttıkça hafif artırabiliriz (Opsiyonel, şimdilik sabit)
                    mainCam.transform.DOComplete(); 
                    mainCam.transform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato);
                }
            }

            if (LevelManager.Instance != null) LevelManager.Instance.OnZombieEaten();
        }
        // Human condition removed as per user request (Counter only for Zombies)
        // YENİ: İnsanı yiyince sadece level manager'a bildir (Fail condition için)
        else if (victim.CompareTag("Human"))
        {
            // XP Cezası
            currentXP--;
            if (currentXP < 0) currentXP = 0;
            
            // Floating Text (-1 Red)
            SpawnFloatingText("-1", Color.red);
            
            if (LevelManager.Instance != null) LevelManager.Instance.OnHumanEaten();
        }
        else if (victim.CompareTag("SkillPickup"))
        {
            // Skill Pickup yutuldu - Skill'i aktif et!
            SkillPickup pickup = victim.GetComponent<SkillPickup>();
            if (pickup != null && SkillManager.Instance != null)
            {
                // Permanent flag'ini de gönder!
                SkillManager.Instance.ActivateSkill(pickup.skillType, pickup.isPermanentPickup);
                
                string durationText = pickup.isPermanentPickup ? " (∞)" : "";
                SpawnFloatingText(pickup.skillType.ToString() + durationText + "!", Color.cyan);
                
                Debug.Log($"[HoleMechanics] Skill pickup yutuldu: {pickup.skillType} (Permanent: {pickup.isPermanentPickup})");
            }
        }
        else if (isFeverMode)
        {
            // Fever modunda Environment yedik
            // Zaten Bonus Gold yukarıda verildi.
            // Ekstra efekt?
        }

        if (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
        
        if (visuals != null && xpToNextLevel > 0)
        {
            visuals.UpdateLocalProgress((float)currentXP / xpToNextLevel);
        }
    }

    public void SpawnFloatingText(string text, Color color)
    {
        GameObject go = new GameObject("FloatingXP");
        go.transform.position = transform.position + Vector3.up * 2f; // Deliğin biraz üstünde
        
        // Canvas Ekle (World Space)
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        
        // Scale
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(5, 2);
        rect.localScale = Vector3.one * 0.1f; // Yazı boyutu

        // Floating Logic
        FloatingText ft = go.AddComponent<FloatingText>();
        ft.Setup(text, color, damageFont);
    }

    void LevelUp()
    {
        holeLevel++;
        currentXP = 0; 
        xpToNextLevel = (int)(xpToNextLevel * 1.8f); // Harder scaling (was 1.5f)

        // Büyüme Oranı
        float growthFactor = 1.2f;
        float duration = 0.5f;

        // 1. Fiziği/Kendini Büyüt (Animasyonlu)
        Vector3 targetScale = transform.localScale * growthFactor;
        transform.DOScale(targetScale, duration).SetEase(Ease.OutElastic);
        
        // 2. Görseli Büyüt (Eğer ayrı bir objedeyse)
        if (visuals != null && visuals.transform.parent != transform)
        {
             Vector3 visualTargetScale = visuals.transform.localScale * growthFactor;
             visuals.transform.DOScale(visualTargetScale, duration).SetEase(Ease.OutElastic);
        }

        // 3. Shader Radius Güncelle (Masker)
        if (maskController != null)
        {
            float targetRadius = voidRadius * targetScale.x;
            DOTween.To(() => maskController.currentRadius, x => maskController.currentRadius = x, targetRadius, duration).SetEase(Ease.OutElastic);
        }
        
        // 4. Kamera Uzaklığını Dengele (Normal Levels)
        // User Request: "Her levelde aynı olsun sadece boyutuna göre biraz uzaklaşsın"
        // Scale 1.2x artıyor, eğer kamerayı ellemezsek 1.2x uzaklaşır.
        // Biz local pozisyonu 0.9x yaparsak -> 1.2 * 0.9 = 1.08x (Sadece %8 uzaklaşır, mantıklı "biraz")
        if (mainCam != null && mainCam.transform.IsChildOf(transform))
        {
            mainCam.transform.DOLocalMove(mainCam.transform.localPosition * 0.9f, duration).SetEase(Ease.OutElastic);
        }
        
        UpdateLevelText();

        Debug.Log($"HOLE LEVEL UP! New Level: {holeLevel} | Target Scale: {targetScale.x}");
    }

    [Header("Shield Visual")]
    public GameObject shieldVisualEffect; // Inspector'dan ata (yeşil halo/ring prefab)
    private bool wasShieldActive = false;

    private bool wasMagnetActive = false;

    private void Update()
    {
        // --- SKILL EFFECTS ---
        if (SkillManager.Instance != null)
        {
            bool magnetNow = SkillManager.Instance.IsMagnetActive;
            
            if (magnetNow)
            {
                ApplyMagnetEffect();
            }
            else if (wasMagnetActive && !magnetNow)
            {
                // Magnet bitti - tüm zombilerin isBeingPulled flag'ini sıfırla
                ResetAllPulledFlags();
            }
            
            wasMagnetActive = magnetNow;
            
            // Shield görsel efekti ve İNSAN İTME
            bool shieldNow = SkillManager.Instance.IsShieldActive;
            if (shieldNow != wasShieldActive)
            {
                wasShieldActive = shieldNow;
                if (shieldVisualEffect != null)
                {
                    shieldVisualEffect.SetActive(shieldNow);
                }
                
                if (shieldNow)
                {
                    Debug.Log("[HoleMechanics] Shield aktif! İnsanlar itilecek!");
                }
            }
            
            // Shield aktifken insanları it
            if (shieldNow)
            {
                ApplyShieldRepelEffect();
            }
        }
    }
    
    void ApplyShieldRepelEffect()
    {
        // Shield radius - SkillManager'dan al veya sabit değer kullan
        float repelRadius = 8f; // İtme alanı
        float repelForce = 15f; // İtme kuvveti
        
        // Yakındaki insanları bul
        Collider[] nearby = Physics.OverlapSphere(transform.position, repelRadius);
        
        foreach (var col in nearby)
        {
            // SADECE İNSANLARI İT
            if (col.CompareTag("Human"))
            {
                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    // Hole'dan uzaklaştırma yönü
                    Vector3 diff = col.transform.position - transform.position;
                    float dist = diff.magnitude;
                    
                    if (dist < 0.1f) continue; // Çok yakın, atla
                    
                    Vector3 direction = diff / dist;
                    direction.y = 0; // Yatay itme
                    
                    // Mesafeye göre kuvvet (yakında daha güçlü)
                    float distanceFactor = 1f - (dist / repelRadius);
                    float actualForce = repelForce * distanceFactor;
                    
                    targetRb.AddForce(direction * actualForce, ForceMode.Acceleration);
                }
            }
        }
    }
    
    private void ResetAllPulledFlags()
    {
        // Tüm zombilerin isBeingPulled flag'ini sıfırla
        CharacterAI[] allChars = FindObjectsOfType<CharacterAI>();
        foreach (var charAI in allChars)
        {
            if (charAI != null)
            {
                charAI.isBeingPulled = false;
                
                // Drag değerlerini de normale döndür
                Rigidbody rb = charAI.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.drag = 1f;
                    rb.angularDrag = 5f;
                }
            }
        }
        Debug.Log("[HoleMechanics] Magnet bitti, tüm zombiler serbest.");
    }

    // Skill değerleri artık SkillManager'dan dinamik olarak alınıyor

    void ApplyMagnetEffect()
    {
        // Dinamik değerleri SkillManager'dan al
        float radius = SkillManager.Instance.GetMagnetRadius();
        float force = SkillManager.Instance.GetMagnetForce();
        
        // 1. Find targets nearby
        Collider[] nearby = Physics.OverlapSphere(transform.position, radius);

        foreach (var col in nearby)
        {
            // ONLY PULL ZOMBIES (Ignore Humans)
            if (col.CompareTag("Zombie"))
            {
                // --- FIX: FALLING CHECK ---
                // Eğer zombi deliğin merkezine çok yakınsa (düşme mesafesindeyse),
                // Magnet kuvvetini ve yüksek sürtünmeyi (drag=5) İPTAL ET.
                // Aksi takdirde zombi havada "yüzüyor" gibi takılı kalıyor ve düşmüyor.
                
                Vector3 holePos = transform.position;
                Vector3 targetPos = col.transform.position;
                
                // Yükseklik farkını yoksay (2D Uzaklık)
                float distToCenter = Vector2.Distance(new Vector2(holePos.x, holePos.z), new Vector2(targetPos.x, targetPos.z));
                // PhysicsFall coroutine'i 0.9f çarpanını kullanıyor.
                // Biz de aynısını kullanmalıyız ki arada boşluk (Dead Zone) kalmasın.
                float killZoneRadius = voidRadius * transform.localScale.x * 0.9f; 

                if (distToCenter < killZoneRadius)
                {
                     // Zombi düşme alanında! Magnet onu rahat bıraksın ki PhysicsFall (Gravity) işini yapsın.
                     // ANCAK: Önceki kareden kalan "Drag 5" yüzünden havada asılı kalabilir.
                     // Bu yüzden burada ZORLA Drag'i sıfırla ve aşağı it.
                     
                     Rigidbody rbInZone = col.GetComponent<Rigidbody>();
                     if (rbInZone != null)
                     {
                         rbInZone.drag = 0f;
                         rbInZone.angularDrag = 0.05f;
                         
                         // Ekstra: Hafif aşağı it ki kesin düşsün (Hovering Fix)
                         rbInZone.AddForce(Vector3.down * 20f, ForceMode.Acceleration);
                     }
                     
                     // Agent'ı da kapat ki kaçmaya çalışmasın
                     UnityEngine.AI.NavMeshAgent agentInZone = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
                     if (agentInZone != null && agentInZone.enabled) agentInZone.enabled = false;

                     continue; 
                }

                // 2. Disable NavMeshAgent so Physics can take over
                UnityEngine.AI.NavMeshAgent agent = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled)
                {
                    agent.enabled = false;
                }

                CharacterAI charAI = col.GetComponent<CharacterAI>();
                // CharacterAI'ya "çekiliyorsun" diye haber ver
                if (charAI != null)
                {
                    charAI.isBeingPulled = true;
                }

                // 3. Apply Force with Heavy Damping (To stop orbiting)
                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    targetRb.isKinematic = false; 

                    // -- Physics Tweak --
                    // Magnet aktifken drag'i düşür ki çekebilsin
                    targetRb.drag = 0.5f; 
                    targetRb.angularDrag = 0.5f;

                    Vector3 diff = transform.position - col.transform.position;
                    float dist = diff.magnitude;
                    
                    // --- INFINITE FORCE KORUMASI ---
                    if (dist < 0.01f || float.IsNaN(dist) || float.IsInfinity(dist))
                    {
                        continue; // Çok yakın veya geçersiz, atla
                    }
                    
                    Vector3 direction = diff / dist;
                    direction.y = 0; // Keep pull horizontal, gravity handles falling

                    // Pull towards hole center - GÜÇLÜ ÇEKİŞ
                    // ForceMode.Acceleration kütle farkını yoksayar
                    targetRb.AddForce(direction * force, ForceMode.Acceleration);
                    
                    // Draw debug line to confirm lock-on
                    Debug.DrawLine(transform.position, col.transform.position, Color.cyan);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Void Radius
        Gizmos.color = Color.black;
        float scaledRadius = voidRadius * transform.localScale.x;
        Gizmos.DrawWireSphere(transform.position, scaledRadius);

        if (SkillManager.Instance != null && SkillManager.Instance.IsMagnetActive)
        {
             Gizmos.color = Color.blue;
             Gizmos.DrawWireSphere(transform.position, SkillManager.Instance.GetMagnetRadius());
        }
        
        if (SkillManager.Instance != null && SkillManager.Instance.IsRepellentActive)
        {
             Gizmos.color = Color.magenta;
             Gizmos.DrawWireSphere(transform.position, SkillManager.Instance.GetRepellentRadius());
        }
    }

    private void CreateDefaultXPParticles()
    {
        GameObject go = new GameObject("XP_Burst_Effects");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.2f; 
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);

        xpParticles = go.AddComponent<ParticleSystem>();
        
        // --- Main Module (Zombie Blood / Slime) ---
        var main = xpParticles.main;
        // Zombi kanı: Parlak Yeşil -> Koyu Yeşil
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 0.1f, 1f), new Color(0.1f, 0.5f, 0.1f, 1f)); 
        // Biraz daha belirgin damlalar
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f); 
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f); // Çok hızlı fırlamasın, vıcık olsun
        main.startRotation = new ParticleSystem.MinMaxCurve(0, 360);
        main.gravityModifier = 0.8f; // Yer çekimi: Kan gibi aşağı düşsün
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        // --- Emission (Reduced Count) ---
        var emission = xpParticles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) }); // Daha az ama öz (15)

        // --- Shape (Halka) ---
        var shape = xpParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;
        shape.radiusThickness = 0.1f;
        
        // --- Size Over Lifetime (Eriyen Damlalar) ---
        var sol = xpParticles.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.5f);
        curve.AddKey(0.2f, 1.0f); 
        curve.AddKey(1.0f, 0.0f); // Küçülerek yok ol
        sol.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        // --- Renderer (Standard Alpha Blended - Not Additive) ---
        // Kan/Slime efektinin net görünmesi için Additive yerine Alpha Blended daha iyi durur (Koyu renkleri gösterir)
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            // Standard Unlit, rengi olduğu gibi basar (Additive parlama yapmaz)
            Material bloodMat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (bloodMat != null) 
            {
                renderer.material = bloodMat;
                
                // Render Ayarları (Transparent)
                bloodMat.SetFloat("_Mode", 2);
                bloodMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                bloodMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                bloodMat.SetInt("_ZWrite", 0);
                bloodMat.EnableKeyword("_ALPHABLEND_ON");
                bloodMat.renderQueue = 3000;
            }
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
