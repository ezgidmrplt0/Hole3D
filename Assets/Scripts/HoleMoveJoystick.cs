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
        // Bu objenin ve altındaki tüm çocukların (rim, hole center) colliderlarını listeye al
        holeColliders = GetComponentsInChildren<Collider>();
    }

    void FixedUpdate()
    {
        // Joystick verisini al
        Vector3 direction = new Vector3(joystick.Horizontal, 0f, joystick.Vertical);

        // Hareket yoksa durdur
        if (direction.magnitude < 0.1f)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        // Çapraz gidişlerde hızı dengele
        if (direction.magnitude > 1f) direction.Normalize();

        // FİZİKSEL HAREKET KULLAN
        rb.velocity = direction * moveSpeed;

        // Sınırları kontrol et
        if (useLimits)
        {
            float clampedX = Mathf.Clamp(rb.position.x, minX, maxX);
            float clampedZ = Mathf.Clamp(rb.position.z, minZ, maxZ);
            rb.position = new Vector3(clampedX, rb.position.y, clampedZ);
        }
    }

    // Zombi, kenardaki gri çerçeveye çarparsa, çarpışmayı yoksay (ki içine düşebilsin)
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Zombie"))
        {
            Collider zombieCollider = collision.collider;

            foreach (Collider col in holeColliders)
            {
                // DİKKAT: Sadece "Trigger" olmayan (yani katı duvar gibi olan) parçalarla çarpışmayı kapatıyoruz.
                // Eğer Trigger olanı da kapatırsak, zombi düşme mekaniğini (OnTriggerEnter) tetikleyemez!
                if (col != null && !col.isTrigger) 
                {
                    Physics.IgnoreCollision(col, zombieCollider, true);
                }
            }
        }
    }
}
