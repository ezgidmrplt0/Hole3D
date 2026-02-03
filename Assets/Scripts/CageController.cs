using UnityEngine;
using System.Collections.Generic;
using DG.Tweening; // Animasyon için (opsiyonel ama şık durur)

public class CageController : MonoBehaviour
{
    private List<ZombieAI> trappedZombies = new List<ZombieAI>();
    private bool isOpened = false;
    
    // Kafes modelleri veya parçaları (animasyon için)
    // Şimdilik basitçe tüm objeyi yöneteceğiz

    public void Setup(List<ZombieAI> zombies)
    {
        trappedZombies = zombies;
        
        // Zombileri kafes merkezine yerleştir
        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                zombie.SetTrapped(true);
            }
        }
    }

    public void OpenCage()
    {
        if (isOpened) return;
        isOpened = true;

        Debug.Log("Cage Opened! Zombies released.");

        // 1. Zombileri serbest bırak
        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                zombie.SetTrapped(false);
            }
        }
        
        // 2. Kafesi yok et (Efektli)
        // Yukarı doğru uçup kaybolsun
        transform.DOMoveY(transform.position.y + 10f, 1.5f).SetEase(Ease.InBack).OnComplete(() => {
            Destroy(gameObject);
        });
        
        // Ses efekti eklenebilir
    }
}
