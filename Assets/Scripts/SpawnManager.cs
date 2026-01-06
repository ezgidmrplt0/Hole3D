using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("List of Human prefabs to spawn.")]
    public List<GameObject> humanPrefabs;
    [Tooltip("List of Zombie prefabs to spawn.")]
    public List<GameObject> zombiePrefabs;

    [Header("Spawn Settings")]
    [Tooltip("Number of humans to spawn.")]
    public int humanCount = 10;
    [Tooltip("Number of zombies to spawn.")]
    public int zombieCount = 5;

    [Header("Spawn Area")]
    public Vector2 spawnAreaMin = new Vector2(-10, -10);
    public Vector2 spawnAreaMax = new Vector2(10, 10);
    [Tooltip("Y offset for the raycast start position.")]
    public float raycastHeight = 10f;
    [Tooltip("Layer mask to detect ground.")]
    public LayerMask groundLayer;
    [Header("Collision Check")]
    [Tooltip("Layer mask for obstacles to avoid spawning inside.")]
    public LayerMask obstacleLayer;
    [Tooltip("Radius to check for existing objects around spawn point.")]
    public float collisionCheckRadius = 1f;
    [Tooltip("Minimum distance between spawned characters.")]
    public float minSpawnDistance = 2f;
    [Tooltip("Maximum attempts to find a valid position per character.")]
    public int maxSpawnAttempts = 30; // 10'dan 30'a çıkardık, daha fazla şans tanıyalım

    private List<Vector3> spawnedPositions = new List<Vector3>();



    // Removed Start() to prevent auto-spawn. Controlled by LevelManager.

    // Public method called by LevelManager
    public void SpawnLevel(int humans, int zombies)
    {
        humanCount = humans;
        zombieCount = zombies;
        SpawnCharacters();
    }

    public void ClearScene()
    {
        // Find all existing characters and destroy them
        var humans = GameObject.FindGameObjectsWithTag("Human");
        foreach (var h in humans) Destroy(h);

        var zombies = GameObject.FindGameObjectsWithTag("Zombie");
        foreach (var z in zombies) Destroy(z);
        
        spawnedPositions.Clear();
    }

    private void SpawnCharacters()
    {
        // Spawn Humans
        for (int i = 0; i < humanCount; i++)
        {
            SpawnRandomPrefab(humanPrefabs, "Human");
        }

        // Spawn Zombies
        for (int i = 0; i < zombieCount; i++)
        {
            SpawnRandomPrefab(zombiePrefabs, "Zombie");
        }
    }

    private void SpawnRandomPrefab(List<GameObject> prefabs, string debugName)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning($"SpawnManager: No prefabs assigned for {debugName}!");
            return;
        }

        GameObject selectedPrefab = prefabs[Random.Range(0, prefabs.Count)];
        
        // Try finding a valid position multiple times
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 spawnPos = GetRandomPosition();

            if (spawnPos != Vector3.negativeInfinity)
            {
                if (IsValidPosition(spawnPos))
                {
                    Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                    Instantiate(selectedPrefab, spawnPos, randomRotation);
                    spawnedPositions.Add(spawnPos); // Kaydet
                    return; // Spawn successful, exit method
                }
            }
        }

        Debug.LogWarning($"SpawnManager: Could not find valid position for {debugName} after {maxSpawnAttempts} attempts.");
    }

    private bool IsValidPosition(Vector3 position)
    {
        // 1. Engel Kontrolü (Obstacle Layer)
        // Kürenin merkezini biraz yukarı kaldırıyoruz ki zeminle (Y=0) çakışmasın.
        // checkPos = (X, Y + Radius + 0.2f, Z) -> Alt noktası Y=0.2f olur.
        Vector3 checkPos = position + Vector3.up * (collisionCheckRadius + 0.2f);
        
        if (Physics.CheckSphere(checkPos, collisionCheckRadius, obstacleLayer))
        {
            // Debug.Log("Spawn Failed: Hit Obstacle"); // Çok spam yaparsa kapatın
            return false;
        }

        // 2. Diğer karakterlere mesafe kontrolü
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                // Debug.Log("Spawn Failed: Too Close to another character");
                return false;
            }
        }

        return true;
    }

    [Tooltip("Offset to add to the ground height when spawning.")]
    public float spawnHeightOffset = 0f;

    private Vector3 GetRandomPosition()
    {
        // Belirtilen alan içinde rastgele X ve Z seç
        float randomX = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float randomZ = Random.Range(spawnAreaMin.y, spawnAreaMax.y);

        // Yukarıdan aşağıya Raycast atıp zemini bul
        Vector3 rayStart = new Vector3(randomX, raycastHeight, randomZ);
        
        // Eğer Ground Layer seçilmemişse veya 'Nothing' ise direkt varsayılan yüksekliği kullan
        if (groundLayer.value == 0)
        {
             return new Vector3(randomX, 0f + spawnHeightOffset, randomZ);
        }

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
        {
            return hit.point + Vector3.up * spawnHeightOffset;
        }
        
        // Eğer Raycast hiçbir şeye çarpmazsa (Boşluktaysa) buraya spawnlama
        // Return negative infinity to signal retry
        return Vector3.negativeInfinity;
    }

    private void OnDrawGizmosSelected()
    {
        // Spawn alanını editörde çiz
        Gizmos.color = Color.green;
        Vector3 center = new Vector3((spawnAreaMin.x + spawnAreaMax.x) / 2, 0, (spawnAreaMin.y + spawnAreaMax.y) / 2);
        Vector3 size = new Vector3(spawnAreaMax.x - spawnAreaMin.x, 1, spawnAreaMax.y - spawnAreaMin.y);
        Gizmos.DrawWireCube(center, size);
    }
}
