using UnityEngine;
using UnityEditor;

public class HoleCreatorTool : EditorWindow
{
    [MenuItem("Tools/Create Ready-to-Play Hole")]
    public static void CreateHole()
    {
        // 1. Root Object
        GameObject holeRoot = new GameObject("PlayerHole");
        holeRoot.tag = "Player";
        holeRoot.transform.position = new Vector3(0, 0.1f, 0); // Slightly above ground
        Undo.RegisterCreatedObjectUndo(holeRoot, "Create Hole");

        // 2. Physics (Rigidbody)
        Rigidbody rb = holeRoot.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.drag = 5f;

        // 3. Visuals & Hierarchy
        // Structure:
        // PlayerHole (Logic + RB)
        //   -> Visuals (Holder for scaling)
        //      -> Rim (Gray Ring)
        //      -> Void (Black Center - Trigger)
        
        GameObject visualsRoot = new GameObject("Visuals");
        visualsRoot.transform.SetParent(holeRoot.transform, false);

        // A. Mesh: Outer Rim
        GameObject rimObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rimObj.name = "HoleRim";
        rimObj.transform.SetParent(visualsRoot.transform, false);
        rimObj.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f); // Wide and flat
        DestroyImmediate(rimObj.GetComponent<CapsuleCollider>()); // Remove default collider
        // Rim Material (Gray)
        Renderer rimRen = rimObj.GetComponent<Renderer>();
        rimRen.sharedMaterial = new Material(Shader.Find("Standard"));
        rimRen.sharedMaterial.color = Color.grey;
        
        // B. Mesh: Inner Void (The Black Hole)
        GameObject voidObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        voidObj.name = "HoleVoid";
        voidObj.transform.SetParent(visualsRoot.transform, false);
        voidObj.transform.localScale = new Vector3(1.0f, 0.1f, 1.0f); // Slightly smaller than rim, thicker to cover ground
        voidObj.transform.localPosition = new Vector3(0, 0.02f, 0); // Slight offset up
        DestroyImmediate(voidObj.GetComponent<CapsuleCollider>()); // Remove default collider
        // Void Material (Black)
        Renderer voidRen = voidObj.GetComponent<Renderer>();
        voidRen.sharedMaterial = new Material(Shader.Find("Standard"));
        voidRen.sharedMaterial.color = Color.black; 
        // Important: Make it unlit if possible, but standard black is fine for now

        // 4. Logic Scripts
        // A. HoleMoveJoystick
        HoleMoveJoystick mover = holeRoot.AddComponent<HoleMoveJoystick>();
        mover.moveSpeed = 8f;
        
        // Find Joystick in Scene
        Joystick sceneJoystick = FindObjectOfType<Joystick>();
        if (sceneJoystick != null)
        {
            mover.joystick = sceneJoystick;
            Debug.Log("HoleCreator: Found and assigned Joystick.");
        }
        else
        {
            Debug.LogWarning("HoleCreator: No Joystick found in scene! Please assign it manually.");
        }

        // B. HoleVisuals
        HoleVisuals vizScript = holeRoot.AddComponent<HoleVisuals>();
        // Assign Visuals image if we can find a UI Canvas with an Image named "HoleProgress" (Optional)

        // C. HoleMechanics
        HoleMechanics mechanics = holeRoot.AddComponent<HoleMechanics>();
        mechanics.visuals = vizScript;
        mechanics.voidRadius = 0.5f; // Match cylinder scale roughly (1.0 scale = 0.5 radius)
        mechanics.targetTags = new System.Collections.Generic.List<string> { "Zombie", "Human" };

        // 5. Physics Colliders
        // A. Trigger for Falling (The Void) - Sphere Trigger
        SphereCollider triggerCol = holeRoot.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 0.5f; 
        triggerCol.center = Vector3.zero;

        // B. Physical Rim (To push things?) - Optional, maybe just let physics handle itself
        // Actually, best to have a small collider for the rim so it doesn't pass through walls
        CapsuleCollider bodyCol = holeRoot.AddComponent<CapsuleCollider>();
        bodyCol.isTrigger = false;
        bodyCol.radius = 0.6f; // Outer rim
        bodyCol.height = 0.2f;
        bodyCol.direction = 1; // Y-Axis
        
        // 6. Camera Setup
        if (Camera.main != null)
        {
            // CameraFollow script removed as per user request.
            GameObject camObj = Camera.main.gameObject;
            camObj.transform.position = holeRoot.transform.position + new Vector3(0, 15, -10);
            camObj.transform.LookAt(holeRoot.transform);
            
            Debug.Log("HoleCreator: Positioned Camera relative to Hole.");
        }

        Selection.activeGameObject = holeRoot;
        Debug.Log("HoleCreator: PlayerHole created successfully!");
    }
}
