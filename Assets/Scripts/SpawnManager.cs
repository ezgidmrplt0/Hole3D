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
    public int maxSpawnAttempts = 10;

    private List<Vector3> spawnedPositions = new List<Vector3>();

    private void Start()
    {
        spawnedPositions.Clear();
        SpawnCharacters();
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
        // Check for collisions within the specified radius
        Vector3 checkPos = position + Vector3.up * collisionCheckRadius;
        if (Physics.CheckSphere(checkPos, collisionCheckRadius, obstacleLayer))
        {
            return false;
        }

        // Check distance to other spawned characters
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetRandomPosition()
    {
        // Belirtilen alan içinde rastgele X ve Z seç
        float randomX = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float randomZ = Random.Range(spawnAreaMin.y, spawnAreaMax.y);

        // Yukarıdan aşağıya Raycast atıp zemini bul
        Vector3 rayStart = new Vector3(randomX, raycastHeight, randomZ);
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
        {
            return hit.point;
        }
        
        // Zemin bulunamazsa varsayılan olarak Y=0 (veya sonsuz dönüp kontrol edebiliriz)
        // Eğer harita düz ise direkt new Vector3(randomX, 0, randomZ) döndürebiliriz.
        // Güvenlik için şimdilik Raycast tutmadığında negativeInfinity dönelim.
        // Ancak kullanıcı 'zemin' layer'ını seçmeyi unutursa hiç spawn olmaz.
        // Fallback olarak Y=0.5f verelim.
        return new Vector3(randomX, 0.5f, randomZ); 
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
