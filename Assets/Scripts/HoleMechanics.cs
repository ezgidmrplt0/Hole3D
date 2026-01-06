using UnityEngine;
using System.Collections;

public class HoleMechanics : MonoBehaviour
{
    // Listeye çevirdik ki hem Zombi hem İnsan girebilsin
    public System.Collections.Generic.List<string> targetTags = new System.Collections.Generic.List<string> { "Zombie", "Human" };

    [Header("Animation Settings")]
    public float fallDuration = 0.5f;
    public float sinkDepth = 2f;
    public float minScale = 0.1f;

    private void OnTriggerEnter(Collider other)
    {
        // Listede var mı kontrol et
        if (targetTags.Contains(other.tag))
        {
            StartCoroutine(FallAnim(other.gameObject));
        }
    }

    IEnumerator FallAnim(GameObject victim)
    {
        // 1. Level Manager'a haber ver (Sadece Zombiyse - veya hedefse)
        if (victim.CompareTag("Zombie"))
        {
             if (LevelManager.Instance != null) LevelManager.Instance.OnZombieEaten();
        }

        // 2. AI Scriptini devre dışı bırak (Hareket/Dönüş yapmasın)
        CharacterAI ai = victim.GetComponent<CharacterAI>();
        if (ai != null) ai.enabled = false;

        // 2. Fizik tamamen kapat
        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 3. Collider'ları kapat
        Collider[] cols = victim.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = false;

        // 4. Varsa Animator'ı durdur (Koşma animasyonunda kalmasın)
        Animator anim = victim.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        Vector3 startPos = victim.transform.position;
        Vector3 targetPos = new Vector3(
            transform.position.x,
            startPos.y - sinkDepth,
            transform.position.z
        );

        Vector3 startScale = victim.transform.localScale;
        Vector3 targetScale = startScale * minScale;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / fallDuration;

            // SmoothStep = sinematik ease
            float eased = Mathf.SmoothStep(0, 1, t);

            // Merkeze çek + aşağı
            victim.transform.position = Vector3.Lerp(startPos, targetPos, eased);

            // Küçülme
            victim.transform.localScale = Vector3.Lerp(startScale, targetScale, eased);

            yield return null;
        }

        Destroy(victim);
    }
}
