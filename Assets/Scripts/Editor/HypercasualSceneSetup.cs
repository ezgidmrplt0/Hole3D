using UnityEngine;
using UnityEditor;

public class HypercasualSceneSetup : EditorWindow
{
    [MenuItem("Hole3D/Apply Hypercasual Look")]
    public static void ApplySettings()
    {
        // 1. --- LIGHTING (Directional Light) ---
        Light[] lights = FindObjectsOfType<Light>();
        Light sun = null;
        
        // Find existing directional light
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                sun = l;
                break;
            }
        }

        // Create if not exists
        if (sun == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            sun = lightObj.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        Undo.RecordObject(sun.gameObject, "Update Sun Config");
        Undo.RecordObject(sun, "Update Sun Settings");

        // Hypercasual Sun Settings
        // Referans resimdek gibi net ve parlak beyaz ışık
        sun.color = Color.white; 
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Hard; 
        sun.shadowStrength = 0.5f;
        
        // Klasik 45 derece ışık açısı
        sun.transform.rotation = Quaternion.Euler(50f, -45f, 0f);


        // 2. --- ENVIRONMENT (Ambient Light) ---
        // Skybox yerine Gradient (Trilight) kullanmak daha kontrollü bir aydınlık sağlar
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        
        // Referans resim: Çok canlı bir MAVİ gökyüzü
        Color deepSkyBlue = new Color(0.1f, 0.6f, 1.0f); // Deep Cyan/Blue
        Color horizonBlue = new Color(0.4f, 0.7f, 1.0f); // Lighter Blue
        
        RenderSettings.ambientSkyColor = deepSkyBlue; 
        RenderSettings.ambientEquatorColor = horizonBlue; 
        RenderSettings.ambientGroundColor = new Color(0.4f, 0.4f, 0.5f); 
        RenderSettings.ambientIntensity = 1.0f;


        // 3. FOG (İPTAL EDİLDİ)
        // Kullanıcı isteği: "Sisliği komple kaldıralım"
        RenderSettings.fog = false;
        
        // 4. CAMERA BACKGROUND
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Update Camera BG");
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = deepSkyBlue;
        }





        // 5. --- SHADOW SETTINGS ---
        // Gölgelerin kalitesini artır
        QualitySettings.shadowDistance = 50f; // Mobil için optimize ama yeterli uzaklık
        QualitySettings.shadowCascades = 2;
        
        Debug.Log("Hole3D: Hypercasual ışıklandırma ve görsel ayarlar uygulandı! (Sun, Ambient, Fog, Camera)");
    }
}
