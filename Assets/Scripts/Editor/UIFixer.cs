using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class UIFixer : EditorWindow
{
    [MenuItem("Tools/Fix UI Issues")]
    public static void FixUI()
    {
        // 1. GameFlowManager'ı bul ve TapToPlay panelini al
        GameFlowManager mgr = FindObjectOfType<GameFlowManager>();
        if (mgr == null)
        {
            Debug.LogError("GameFlowManager bulunamadı!");
            return;
        }

        if (mgr.tapToPlayPanel != null)
        {
            // Paneldeki Image bileşenini bul
            Image panelImage = mgr.tapToPlayPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                // Raycast Target'ı kapat ki tıklamaları engellemesin
                // Çünkü GameFlowManager zaten Update içinde global tıklamayı dinliyor.
                Undo.RecordObject(panelImage, "Fix Raycast Target");
                panelImage.raycastTarget = false;
                Debug.Log($"<color=green>✓ {mgr.tapToPlayPanel.name} panelinin Raycast Target özelliği kapatıldı.</color> Artık butonları engellemeyecek.");
            }
            else
            {
                // Eğer panelde Image yoksa, belki bir alt objededir veya CanvasGroup vardır.
                // En garantisi: Paneli Hierarchy'de en üste (arkaya) taşımak.
                // Ama user butonu içine koydum dedi, o zaman buton panelin çocuğu.
                // O zaman Raycast Target kapatmak en doğrusu.
                Debug.LogWarning("TapToPlay panelinde Image bileşeni bulunamadı.");
            }
        }

        // 2. EventSystem kontrolü
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Debug.Log("<color=green>✓ EventSystem sahneye eklendi.</color>");
        }

        Debug.Log("UI Fix işlemi tamamlandı!");
    }
}
