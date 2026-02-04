using UnityEngine;
using System.Collections.Generic;
using DG.Tweening; // Using DOTween for smooth destroy effect

public class CageController : MonoBehaviour
{
    [Header("Settings")]
    public List<ZombieAI> trappedZombies;
    public Transform cageVisuals; // The bars/cage mesh
    
    private bool isOpened = false;

    private void Start()
    {
        // Initialize trapped zombies
        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                zombie.isTrapped = true;
                // Opsiyonel: Zombileri kafes içinde rastgele veya sabit durdur
                // zombie.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false; // Zaten Update'te return yiyor ama garanti olsun
            }
        }
    }

    private void OnEnable()
    {
        KeyPickup.OnKeyCollected += ReleaseZombies;
    }

    private void OnDisable()
    {
        KeyPickup.OnKeyCollected -= ReleaseZombies;
    }

    public void ReleaseZombies()
    {
        if (isOpened) return;
        isOpened = true;

        Debug.Log("Cage Opening! Zombies Released!");

        // 1. Kafesi Yok Et (Görsel Efektle)
        if (cageVisuals != null)
        {
            // Basitçe küçülerek yok olsun veya yukarı kalksın
            cageVisuals.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack).OnComplete(() => {
                Destroy(gameObject); // Scriptin olduğu ana objeyi yok et
            });
        }
        else
        {
            Destroy(gameObject);
        }

        // 2. Zombileri Serbest Bırak
        foreach (var zombie in trappedZombies)
        {
            if (zombie != null)
            {
                zombie.ReleaseFromCage();
                
                // Kaçış efekti: Zombiye dışarı doğru küçük bir "Push" verebiliriz (Opsiyonel)
            }
        }
    }
}
