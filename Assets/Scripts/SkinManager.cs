using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct HoleSkin
{
    public string skinID;
    public string displayName;
    public Texture2D texture;
    public Color color;
    public float rotationSpeed;
    public float repeatCount;
    public float emissionStrength;
    public bool swapUVs;
    public bool usePlanarMapping;
    public float insideRadius;
    public float outsideRadius;
    public Vector2 uvOffset;
}

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance;

    [Header("References")]
    public MeshRenderer holeRimRenderer;

    [Header("Skin Content")]
    public List<HoleSkin> skins = new List<HoleSkin>();
    public string currentSkinID = "default";
    
    // ========== PROGRESS & UNLOCKS ==========
    private const string PREF_SKIN_KEY = "SelectedHoleSkin";
    private const string PREF_SKIN_PROGRESS = "SkinUnlockProgress"; // 0-100
    private const string PREF_UNLOCKED_SKINS = "UnlockedHoleSkins"; // Comma separated IDs

    // Shader Property IDs
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int RotationSpeedID = Shader.PropertyToID("_RotationSpeed");
    private static readonly int RepeatCountID = Shader.PropertyToID("_RepeatCount");
    private static readonly int EmissionStrengthID = Shader.PropertyToID("_EmissionStrength");
    private static readonly int SwapUVsID = Shader.PropertyToID("_SwapUVs");
    private static readonly int UsePlanarMappingID = Shader.PropertyToID("_UsePlanarMapping");
    private static readonly int InsideRadiusID = Shader.PropertyToID("_InsideRadius");
    private static readonly int OutsideRadiusID = Shader.PropertyToID("_OutsideRadius");
    private static readonly int UVOffsetID = Shader.PropertyToID("_UVOffset");

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load saved skin
        currentSkinID = PlayerPrefs.GetString(PREF_SKIN_KEY, "default");
    }

    private void Start()
    {
        ApplySelectedSkin();
    }

    public void ApplySelectedSkin()
    {
        if (holeRimRenderer == null)
        {
            // Try to find it in scene if not assigned
            HoleMechanics hm = FindObjectOfType<HoleMechanics>();
            if (hm != null)
            {
                // Hole_Rim genelde çocuk objesidir
                Transform rim = hm.transform.Find("Hole_Rim");
                if (rim != null) holeRimRenderer = rim.GetComponent<MeshRenderer>();
            }
        }

        if (holeRimRenderer == null) return;

        HoleSkin skin = skins.Find(s => s.skinID == currentSkinID);
        
        // --- FALLBACK LOGIC ---
        // If the skin isn't found, use the first skin in the list as a fallback
        if (string.IsNullOrEmpty(skin.skinID) && skins.Count > 0)
        {
            skin = skins[0];
        }

        ApplySkinToMaterial(skin);
    }

    private void ApplySkinToMaterial(HoleSkin skin)
    {
        if (holeRimRenderer == null || holeRimRenderer.material == null) return;

        Material mat = holeRimRenderer.material;

        if (skin.texture != null) mat.SetTexture(MainTexID, skin.texture);
        
        // --- COLOR FALLBACK ---
        // If texture is assigned but color is black (0,0,0), force it to white
        // This prevents textures from being invisible due to accidentally leaving color at default black.
        Color targetColor = skin.color;
        if (skin.texture != null && targetColor.r <= 0.01f && targetColor.g <= 0.01f && targetColor.b <= 0.01f)
        {
            targetColor = Color.white;
        }
        
        mat.SetColor(ColorID, targetColor);
        mat.SetFloat(RotationSpeedID, skin.rotationSpeed);
        mat.SetFloat(RepeatCountID, skin.repeatCount > 0 ? skin.repeatCount : 1.0f);
        mat.SetFloat(EmissionStrengthID, skin.emissionStrength > 0 ? skin.emissionStrength : 1.0f);
        mat.SetFloat(SwapUVsID, skin.swapUVs ? 1.0f : 0.0f);
        mat.SetFloat(UsePlanarMappingID, skin.usePlanarMapping ? 1.0f : 0.0f);
        
        // Pass radius values (defaults to 0.8 / 1.0 if not specified / 0)
        mat.SetFloat(InsideRadiusID, skin.insideRadius > 0.001f ? skin.insideRadius : (skin.usePlanarMapping ? 0.0f : 0.8f));
        mat.SetFloat(OutsideRadiusID, skin.outsideRadius > 0.001f ? skin.outsideRadius : 1.0f);
        mat.SetVector(UVOffsetID, skin.uvOffset);
    }

    public void SelectSkin(string skinID)
    {
        if (!IsSkinUnlocked(skinID)) return;
        
        currentSkinID = skinID;
        PlayerPrefs.SetString(PREF_SKIN_KEY, skinID);
        PlayerPrefs.Save();
        ApplySelectedSkin();
    }

    // ========== PROGRESS LOGIC ==========
    public float GetCurrentProgress()
    {
        return PlayerPrefs.GetFloat(PREF_SKIN_PROGRESS, 0f);
    }

    public void AddProgress(float percentage)
    {
        float current = GetCurrentProgress();
        current += percentage;
        
        if (current >= 100f)
        {
            UnlockNextSkin();
            current -= 100f; // Reset for next skin
        }
        
        PlayerPrefs.SetFloat(PREF_SKIN_PROGRESS, current);
        PlayerPrefs.Save();
    }

    public bool IsSkinUnlocked(string skinID)
    {
        // Default skin is always unlocked
        if (skinID == "default") return true;
        
        string unlocked = PlayerPrefs.GetString(PREF_UNLOCKED_SKINS, "default");
        return unlocked.Contains(skinID);
    }

    public void UnlockNextSkin()
    {
        // Find the first locked skin in the list
        foreach (var skin in skins)
        {
            if (!IsSkinUnlocked(skin.skinID))
            {
                UnlockSkin(skin.skinID);
                return;
            }
        }
    }

    private void UnlockSkin(string skinID)
    {
        string unlocked = PlayerPrefs.GetString(PREF_UNLOCKED_SKINS, "default");
        if (!unlocked.Contains(skinID))
        {
            unlocked += "," + skinID;
            PlayerPrefs.SetString(PREF_UNLOCKED_SKINS, unlocked);
            PlayerPrefs.Save();
            Debug.Log($"[SkinManager] NEW SKIN UNLOCKED: {skinID}");
        }
    }
}
