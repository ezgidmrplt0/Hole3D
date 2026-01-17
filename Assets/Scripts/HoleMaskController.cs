using UnityEngine;

public class HoleMaskController : MonoBehaviour
{
    public Material groundMaterial;
    public float currentRadius = 1.0f;

    void Start()
    {
        InitializeMaterial();
    }

    public void InitializeMaterial()
    {
        if (groundMaterial != null)
        {
            // Ensure we use the correct shader
            if (groundMaterial.shader.name != "Custom/HoleMaskGround")
            {
                groundMaterial.shader = Shader.Find("Custom/HoleMaskGround");
            }
        }
    }

    void Update()
    {
        if (groundMaterial != null)
        {
            groundMaterial.SetVector("_HolePos", transform.position);
            groundMaterial.SetFloat("_HoleRadius", currentRadius);
        }
    }

    public void SetRadius(float radius)
    {
        currentRadius = radius;
    }
}
