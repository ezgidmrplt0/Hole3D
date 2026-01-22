using UnityEngine;

public class HoleMaskController : MonoBehaviour
{
    // Global shader properties for hole masking
    public float currentRadius = 1.0f;

    // Using global properties allows any object with the hole shader to react 
    // without manual reference assignment, fixing prefab instantiation issues.
    
    void Update()
    {
        // Broadcast hole position and radius to all shaders
        Shader.SetGlobalVector("_HolePos", transform.position);
        Shader.SetGlobalFloat("_HoleRadius", currentRadius);
    }

    public void SetRadius(float radius)
    {
        currentRadius = radius;
    }
}
