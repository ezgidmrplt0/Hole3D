using UnityEngine;

public class HoleMoveJoystick : MonoBehaviour
{
    [Header("Joystick")]
    public Joystick joystick;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Limits")]
    public bool useLimits = false; // Limitsiz başlayın, ayarları yapınca açarsınız
    public float minX = -10f;
    public float maxX = 10f;
    public float minZ = -10f;
    public float maxZ = 10f;

    private Rigidbody rb;
    private Collider[] holeColliders; // Deliğin tüm parçalarının (çerçeve vs) colliderları

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // EĞER RIGIDBODY YOKSA EKLE (Otomatik Çözüm)
        if (rb == null)
        {
            Debug.LogWarning("HoleMoveJoystick: Rigidbody bulunamadı, otomatik ekleniyor...");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Kaymayı önlemek için ayarlar
        rb.drag = 5f; 
        rb.angularDrag = 5f;
        // Y Eksenini kilitle ki aşağı düşmesin!
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.useGravity = false; // Yerçekimini kapat
        
        // Bu objenin ve altındaki tüm çocukların (rim, hole center) colliderlarını listeye al
        holeColliders = GetComponentsInChildren<Collider>();

        // AUTO-FIND JOYSTICK (Kullanıcı yeni eklerse otomatik bulsun)
        if (joystick == null)
        {
            joystick = FindObjectOfType<Joystick>();
            if (joystick == null)
            {
                Debug.LogError("HoleMoveJoystick: Sahneye 'Dynamic Joystick' eklemeyi unuttunuz! Lütfen Joystick Pack -> Prefabs klasöründen 'Dynamic Joystick'i Canvas'a sürükleyin.");
            }
            else
            {
                Debug.Log($"HoleMoveJoystick: Otomatik olarak {joystick.name} bulundu ve atandı.");
            }
        }

        // DYNAMIC JOYSTICK AYARI (Tam Ekran Dokunma)
        if (joystick != null && joystick.GetType().Name.Contains("Dynamic"))
        {
            RectTransform rt = joystick.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Ekranı Kapla (Stretch)
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero; // Offsetleri sıfırla
                rt.anchoredPosition = Vector2.zero;
                
                // En arkaya gönder ki (Hierarchy'de en üst), diğer butonları engellemesin
                rt.SetAsFirstSibling();
                
                Debug.Log("HoleMoveJoystick: Dynamic Joystick tam ekran yapıldı.");
            }
        }
    }

    void Update()
    {
        // JOYSTICK GÖRÜNÜRLÜK YÖNETİMİ
        // Oyun başlamadıysa Joystick objesini kapat ki tıklamaları engellemesin
        if (joystick != null && GameFlowManager.Instance != null)
        {
            bool shouldBeActive = GameFlowManager.Instance.IsGameActive;
            
            // Gereksiz set işlemlerinden kaçınmak için check yap
            if (joystick.gameObject.activeSelf != shouldBeActive)
            {
                joystick.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Joystick kapalıysa veya oyun aktif değilse hareket etme
        if (joystick == null || !joystick.gameObject.activeInHierarchy)
        {
             rb.velocity = Vector3.zero;
             rb.angularVelocity = Vector3.zero;
             return;
        }

        Vector3 direction = new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
        
        // Debug için konsola yaz (Sadece hareket varsa)
        if (direction.magnitude > 0.01f)
            Debug.Log($"Joystick Input: {direction.magnitude} | X: {direction.x} Y: {direction.z}");

        if (direction.magnitude < 0.15f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (direction.magnitude > 1f) direction.Normalize();

        // velocity yerine MovePosition kullanıyoruz (Daha stabil)
        float currentSpeed = moveSpeed;
        
        // --- SKILL: SPEED BOOST (Level bazlı) ---
        if (SkillManager.Instance != null && SkillManager.Instance.IsSpeedActive)
        {
            currentSpeed *= SkillManager.Instance.GetSpeedMultiplier();
        }

        Vector3 newPos = rb.position + direction * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }

    void OnCollisionEnter(Collision collision)
    {
        // HER ŞEYİ IGNORE ET
        // Zemini de ignore etsek sorun olmaz çünkü Y eksenini kilitledik.
        Collider otherCollider = collision.collider;
        foreach (Collider col in holeColliders)
        {
            if (col != null && !col.isTrigger) 
            {
                Physics.IgnoreCollision(col, otherCollider, true);
            }
        }
    }
}
