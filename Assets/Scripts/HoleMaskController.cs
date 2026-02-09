using UnityEngine;

public class HoleMaskController : MonoBehaviour
{
    // Global shader properties for hole masking
    public float currentRadius = 1.0f;
    
    // MOBİL FIX: Shader property ID'lerini cache'le (Her frame string lookup yapmak yavaş)
    private static readonly int HolePosID = Shader.PropertyToID("_HolePos");
    private static readonly int HoleRadiusID = Shader.PropertyToID("_HoleRadius");
    
    // MOBİL FIX: LateUpdate'de güncelle (Kamera ve obje hareketlerinden sonra)
    private Vector3 lastPosition;
    private float lastRadius;

    // Using global properties allows any object with the hole shader to react 
    // without manual reference assignment, fixing prefab instantiation issues.
    
    void Start()
    {
        // İlk değerleri hemen uygula
        UpdateShaderProperties(true);
    }
    
    void LateUpdate()
    {
        // MOBİL OPTİMİZASYON: Sadece değiştiğinde güncelle
        UpdateShaderProperties(false);
    }
    
    private void UpdateShaderProperties(bool force)
    {
        bool positionChanged = Vector3.SqrMagnitude(transform.position - lastPosition) > 0.0001f;
        bool radiusChanged = Mathf.Abs(currentRadius - lastRadius) > 0.0001f;
        
        if (force || positionChanged || radiusChanged)
        {
            // Cache'lenmiş ID'ler ile güncelle (Daha hızlı)
            Shader.SetGlobalVector(HolePosID, transform.position);
            Shader.SetGlobalFloat(HoleRadiusID, currentRadius);
            
            lastPosition = transform.position;
            lastRadius = currentRadius;
        }
    }

    public void SetRadius(float radius)
    {
        currentRadius = radius;
        // Anında güncelle
        UpdateShaderProperties(true);
    }
    
    // MOBİL FIX: Obje devre dışı kalınca shader'ı sıfırla
    void OnDisable()
    {
        // Delik görünmez olsun
        Shader.SetGlobalFloat(HoleRadiusID, 0f);
    }
    
    void OnEnable()
    {
        // Tekrar aktif olunca güncelle
        UpdateShaderProperties(true);
    }
}
