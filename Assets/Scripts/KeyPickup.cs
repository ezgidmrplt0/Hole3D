using UnityEngine;
using DG.Tweening;
using System;

public class KeyPickup : MonoBehaviour
{
    public static event Action OnKeyCollected;

    [Header("Animation")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;
    public float rotateSpeed = 90f;
    
    // Private State
    private Vector3 startPos;
    private Transform holeTransform;
    private bool isFalling = false;
    private bool isBeingSwallowed = false;
    private Rigidbody rb;
    private Collider col;

    private void Start()
    {
        startPos = transform.position;
        
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.isKinematic = true; // Float initially
        rb.useGravity = false;
        
        // Setup Collider
        col = GetComponent<Collider>();
        if (col == null)
        {
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            col = cap;
        }
        else
        {
            col.isTrigger = true;
        }

        // Find Hole
        HoleMechanics hole = FindObjectOfType<HoleMechanics>();
        if (hole != null) holeTransform = hole.transform;

        // Spawn Animation
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one * 1.5f, 1.5f).SetEase(Ease.OutBack);
    }

    private void Update()
    {
        if (isBeingSwallowed) return;

        // Visuals: Rotate & Float
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Logic: Check Distance to Hole
        if (holeTransform != null)
        {
            float distXZ = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(holeTransform.position.x, holeTransform.position.z)
            );
            
            float eatRadius = holeTransform.localScale.x * 0.7f; // Hole scale defines radius
            
            if (distXZ < eatRadius && !isFalling)
            {
                StartFalling();
            }
        }
        else
        {
             // Retry finding hole
             var h = FindObjectOfType<HoleMechanics>();
             if(h != null) holeTransform = h.transform;
        }
    }

    private void StartFalling()
    {
        isFalling = true;
        isBeingSwallowed = true; // Stop animation loop
        
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.drag = 0f;
            
            // Push towards center and down
            Vector3 toHole = (holeTransform.position - transform.position).normalized;
            rb.AddForce(toHole * 3f + Vector3.down * 10f, ForceMode.VelocityChange);
        }
        
        StartCoroutine(FallAndCollect());
    }

    private System.Collections.IEnumerator FallAndCollect()
    {
        float fallTime = 0f;
        float maxFallTime = 2f;
        
        while (fallTime < maxFallTime)
        {
            fallTime += Time.deltaTime;
            
            if (holeTransform != null && rb != null)
            {
                Vector3 toCenter = holeTransform.position - transform.position;
                toCenter.y = 0;
                rb.AddForce(toCenter.normalized * 5f, ForceMode.Acceleration);
            }
            
            // Check Depth (Y difference)
            if (holeTransform != null && transform.position.y < holeTransform.position.y - 1.5f)
            {
                break; // Deep enough!
            }
            
            yield return null;
        }
        
        Collect();
    }

    private void Collect()
    {
        Debug.Log("Key Collected! Releasing all cages...");
        
        // UI Feedback
        if (holeTransform != null)
        {
            var hole = holeTransform.GetComponent<HoleMechanics>();
            if (hole != null) hole.SpawnFloatingText("CAGE UNLOCKED!", Color.green);
        }

        OnKeyCollected?.Invoke();
        
        // Shrink and Destroy
        transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
    }
}
