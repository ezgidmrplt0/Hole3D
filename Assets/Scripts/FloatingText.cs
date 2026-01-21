using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    private TextMeshProUGUI tmpText;
    private float moveSpeed = 2f;
    private float lifeTime = 1f;
    private float timer;

    public void Setup(string text, Color color)
    {
        // Setup TMP if not already
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (tmpText == null) tmpText = gameObject.AddComponent<TextMeshProUGUI>();

        tmpText.text = text;
        tmpText.color = color;
        tmpText.fontSize = 6; // Start small/readable
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = false;
        tmpText.fontStyle = FontStyles.Bold;

        // Ensure visuals
        timer = lifeTime;
    }

    void Update()
    {
        // Move Up
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // Fade Out
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            if (tmpText != null && timer < lifeTime * 0.5f) // Fade locally in second half
            {
                float alpha = timer / (lifeTime * 0.5f);
                tmpText.alpha = alpha;
            }
        }
        else
        {
            Destroy(gameObject);
        }

        // Billboard (Look at Camera)
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
