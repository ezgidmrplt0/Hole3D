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
    }

    void FixedUpdate()
    {
        // Rigidbody yoksa hareket edemeyiz
        if (rb == null) return;
        
        if (joystick == null)
        {
            // Fail silently or just return to avoid unused var warning if we don't move
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
        
        // --- SKILL: SPEED BOOST ---
        if (SkillManager.Instance != null && SkillManager.Instance.IsSpeedUnlocked)
        {
            currentSpeed *= 1.3f; // 30% Boost
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
