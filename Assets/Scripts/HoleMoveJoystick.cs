using UnityEngine;

public class HoleMoveJoystick : MonoBehaviour
{
    [Header("Joystick")]
    public Joystick joystick;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Limits")]
    public bool useLimits = true; // Sınırlar aktif
    // Manual limits fallback
    public float minX = -13f; 
    public float maxX = 13f;
    public float minZ = -23f; 
    public float maxZ = 23f;

    private Rigidbody rb;
    private Collider[] holeColliders; 
    
    // Dynamic Bounds State removed (Raycast used instead)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.drag = 5f; 
        rb.angularDrag = 5f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.useGravity = false; 
        
        holeColliders = GetComponentsInChildren<Collider>();

        if (joystick == null)
        {
            joystick = FindObjectOfType<Joystick>();
        }

        if (joystick != null && joystick.GetType().Name.Contains("Dynamic"))
        {
            RectTransform rt = joystick.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero; 
                rt.anchoredPosition = Vector2.zero;
                rt.SetAsFirstSibling();
            }
        }
    }

    void Update()
    {
        if (joystick != null && GameFlowManager.Instance != null)
        {
            bool shouldBeActive = GameFlowManager.Instance.IsGameActive;
            if (joystick.gameObject.activeSelf != shouldBeActive)
            {
                joystick.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (joystick == null || !joystick.gameObject.activeInHierarchy)
        {
             rb.velocity = Vector3.zero;
             rb.angularVelocity = Vector3.zero;
             return;
        }

        Vector3 direction = new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
        
        if (direction.magnitude < 0.15f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (direction.magnitude > 1f) direction.Normalize();

        float currentSpeed = moveSpeed;
        
        if (SkillManager.Instance != null && SkillManager.Instance.IsSpeedActive)
        {
            currentSpeed *= SkillManager.Instance.GetSpeedMultiplier();
        }

        Vector3 dtMove = direction * currentSpeed * Time.fixedDeltaTime;
        Vector3 potentialPos = rb.position + dtMove;

        // --- RAYCAST BOUNDARY CHECK (Kesin Çözüm) ---
        // Gideceğimiz yerin altında "Zemin" var mı?
        // Yoksa gitme.
        bool isOverGround = CheckGroundAtPosition(potentialPos);

        if (isOverGround)
        {
            // Eğer zemin varsa git
            rb.MovePosition(potentialPos);
        }
        else
        {
            // Zemin yoksa, belki sadece tek eksende gitmeye çalış (Duvara sürtünme hissi)
            // X ekseninde dene
            Vector3 tryX = rb.position + new Vector3(dtMove.x, 0, 0);
            if (CheckGroundAtPosition(tryX))
            {
                rb.MovePosition(tryX);
            }
            else
            {
                // Z ekseninde dene
                Vector3 tryZ = rb.position + new Vector3(0, 0, dtMove.z);
                if (CheckGroundAtPosition(tryZ))
                {
                    rb.MovePosition(tryZ);
                }
                // Hiçbiri olmuyorsa olduğu yerde kalsın
            }
        }
    }

    private bool CheckGroundAtPosition(Vector3 pos)
    {
        // Yukarıdan aşağı ışın at
        Ray ray = new Ray(pos + Vector3.up * 20f, Vector3.down);
        
        // RaycastAll kullanıyoruz çünkü ilk çarptığımız şey "Kendi Kenarımız" (Rim) olabilir.
        // Kendi colliderımızı delip geçip zemini bulmalıyız.
        RaycastHit[] hits = Physics.RaycastAll(ray, 50f);

        foreach (RaycastHit hit in hits)
        {
            Collider col = hit.collider;

            // 1. Kendi parçamızsa yoksay (Rim, Hole Center, vs.)
            if (col.transform.IsChildOf(transform)) continue;

            // 2. Trigger ise yoksay (Zone, Collectible vs.)
            if (col.isTrigger) continue;

            // 3. Duvar ise yoksay (Duvarın üstüne çıkamayız)
            if (col.CompareTag("Wall") || col.CompareTag("Border")) continue;
            
            // 4. Eğer buraya geldiysek, bu solid bir yerdir (Zemin).
            // Tag/İsim kontrolüne gerek yok, altında katı bir şey varsa yürüyebilirsin.
            return true;
        }

        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;
        
        // WALL / BORDER: Çarpışmayı yoksayma! (Duvar gibi davran)
        if (otherCollider.CompareTag("Wall") || otherCollider.CompareTag("Border")) 
        {
            return;
        }

        // Diğer her şeyi (Zemin, Objeler) yoksay -> İçinden geç
        foreach (Collider col in holeColliders)
        {
            if (col != null && !col.isTrigger) 
            {
                Physics.IgnoreCollision(col, otherCollider, true);
            }
        }
    }
}
