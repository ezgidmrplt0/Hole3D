using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

/// <summary>
/// Bu script, build sırasında gerekli shader'ların dahil edilmesini sağlar.
/// Mobilde pembe/magenta görünen shader sorununu çözer.
/// </summary>
public class MobileShaderIncluder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // Build öncesi shader'ların dahil edildiğinden emin ol
        EnsureShadersIncluded();
    }

    [MenuItem("Tools/Mobile/Ensure Shaders Included")]
    public static void EnsureShadersIncluded()
    {
        // Graphics Settings'e erişim
        SerializedObject graphicsSettings = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        
        SerializedProperty alwaysIncludedShaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
        
        // Dahil edilmesi gereken shader listesi
        string[] requiredShaders = new string[]
        {
            "Sprites/Default",
            "UI/Default",
            "Mobile/Diffuse",
            "Mobile/Particles/Alpha Blended",
            "Unlit/Transparent",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Custom/HoleMaskGround",
            "Custom/HoleHollowRim"
        };

        bool modified = false;

        foreach (string shaderName in requiredShaders)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[MobileShaderIncluder] Shader bulunamadı: {shaderName}");
                continue;
            }

            // Zaten listede mi kontrol et
            bool alreadyIncluded = false;
            for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                SerializedProperty element = alwaysIncludedShaders.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == shader)
                {
                    alreadyIncluded = true;
                    break;
                }
            }

            if (!alreadyIncluded)
            {
                // Listeye ekle
                int newIndex = alwaysIncludedShaders.arraySize;
                alwaysIncludedShaders.InsertArrayElementAtIndex(newIndex);
                alwaysIncludedShaders.GetArrayElementAtIndex(newIndex).objectReferenceValue = shader;
                modified = true;
                Debug.Log($"[MobileShaderIncluder] Shader eklendi: {shaderName}");
            }
        }

        if (modified)
        {
            graphicsSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[MobileShaderIncluder] GraphicsSettings güncellendi. Shader'lar build'e dahil edilecek.");
        }
        else
        {
            Debug.Log("[MobileShaderIncluder] Tüm shader'lar zaten dahil edilmiş.");
        }
    }

    [MenuItem("Tools/Mobile/Check Build Settings")]
    public static void CheckMobileBuildSettings()
    {
        Debug.Log("=== MOBİL BUILD KONTROL LİSTESİ ===");
        
        // 1. Quality Settings kontrolü
        int currentQuality = QualitySettings.GetQualityLevel();
        Debug.Log($"1. Aktif Quality Level: {QualitySettings.names[currentQuality]} (Index: {currentQuality})");
        
        // 2. Player Settings kontrolü
        Debug.Log($"2. Color Space: {PlayerSettings.colorSpace}");
        Debug.Log($"3. Graphics API (Android): {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
        
        // 3. Fixed Timestep kontrolü
        Debug.Log($"4. Fixed Timestep: {Time.fixedDeltaTime}");
        
        // 4. VSync kontrolü
        Debug.Log($"5. VSync Count: {QualitySettings.vSyncCount}");
        
        // 5. Target Frame Rate
        Debug.Log($"6. Target Frame Rate: {Application.targetFrameRate}");
        
        Debug.Log("=================================");
        Debug.Log("ÖNERİLER:");
        Debug.Log("- Mobil için Quality Level 'Low' veya 'Medium' kullanın");
        Debug.Log("- VSync Count = 0 yapın ve targetFrameRate = 60 ayarlayın");
        Debug.Log("- Color Space: Gamma (mobil uyumluluğu için)");
    }
}
