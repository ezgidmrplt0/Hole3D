using UnityEngine;

public class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance;

    [Header("Vibration Settings")]
    public float vibrationCooldown = 0.2f;

    private float lastVibrationTime = -10f;

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

    public void ComboVibrate(int combo)
    {
        // Çok sık titreşimi engelle
        if (Time.time - lastVibrationTime < vibrationCooldown)
            return;

        // Sadece combo varsa titreştir
        if (combo < 2)
            return;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif

        lastVibrationTime = Time.time;
    }
}