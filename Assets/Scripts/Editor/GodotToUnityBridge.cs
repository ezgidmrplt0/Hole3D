using UnityEngine;
using UnityEditor;

public class GodotToUnityBridge : EditorWindow
{
    [MenuItem("Tools/Magic Hole Setup (Direct)")]
    public static void SetupHoleSystem()
    {
        // 1. Create Ground Plane
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Hole_Compatible_Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(5, 1, 5); 
        floor.layer = LayerMask.NameToLayer("Default");

        Renderer floorRen = floor.GetComponent<Renderer>();
        Material groundMat = new Material(Shader.Find("Custom/HoleMaskGround"));
        groundMat.color = Color.white;
        floorRen.sharedMaterial = groundMat;

        // 2. Root Player Object
        GameObject holePlayer = new GameObject("Hole_Player");
        holePlayer.tag = "Player";
        holePlayer.transform.position = new Vector3(0, 0.05f, 0);
        
        var rb = holePlayer.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        
        var joystickMover = holePlayer.AddComponent<HoleMoveJoystick>();
        joystickMover.moveSpeed = 8f;
        
        var mechanics = holePlayer.AddComponent<HoleMechanics>();
        var maskController = holePlayer.AddComponent<HoleMaskController>();
        maskController.groundMaterial = groundMat;

        // 3. Hole_Rim (Visual Ring)
        GameObject holeRim = GameObject.CreatePrimitive(PrimitiveType.Plane);
        holeRim.name = "Hole_Rim";
        holeRim.transform.SetParent(holePlayer.transform);
        holeRim.transform.localPosition = new Vector3(0, 0.01f, 0);
        holeRim.transform.localScale = new Vector3(0.2f, 1, 0.2f); 
        DestroyImmediate(holeRim.GetComponent<MeshCollider>());
        
        Renderer rimRenderer = holeRim.GetComponent<Renderer>();
        Material rimMat = new Material(Shader.Find("Custom/HoleHollowRim"));
        rimMat.SetColor("_Color", new Color(0, 0.5f, 1, 1));
        rimMat.SetFloat("_InsideRadius", 0.9f);
        rimMat.SetFloat("_OutsideRadius", 1.0f);
        rimRenderer.sharedMaterial = rimMat;

        // 4. PhysicsMaskArea (Sphere Trigger)
        GameObject physicsMaskArea = new GameObject("PhysicsMaskArea");
        physicsMaskArea.transform.SetParent(holePlayer.transform);
        physicsMaskArea.transform.localPosition = Vector3.zero;
        
        SphereCollider physicsTrigger = physicsMaskArea.AddComponent<SphereCollider>();
        physicsTrigger.isTrigger = true;
        physicsTrigger.radius = 1.0f;
        
        var physicsHandler = physicsMaskArea.AddComponent<HolePhysicsHandler>();
        physicsHandler.floorCollider = floor.GetComponent<Collider>();

        // 5. CollectionArea (Box Trigger)
        GameObject collectionArea = new GameObject("CollectionArea");
        collectionArea.transform.SetParent(holePlayer.transform);
        collectionArea.transform.localPosition = new Vector3(0, -5f, 0);
        
        BoxCollider collectionTrigger = collectionArea.AddComponent<BoxCollider>();
        collectionTrigger.isTrigger = true;
        collectionTrigger.size = new Vector3(2, 1, 2);

        // 6. Spawn ACTUAL Prefabs (Zombies and Humans)
        string zombiePrefabPath = "Assets/Zombie/URP/Prefabs/ZombieMesh.prefab";
        string humanPrefabPath = "Assets/Prefabs/normal man a.prefab";

        GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(zombiePrefabPath);
        GameObject humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(humanPrefabPath);

        // Spawn Zombies
        for(int i = 0; i < 5; i++)
        {
            GameObject z;
            if (zombiePrefab != null) z = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab);
            else z = GameObject.CreatePrimitive(PrimitiveType.Capsule);

            z.name = "Zombie_" + i;
            z.tag = "Zombie";
            z.transform.position = new Vector3(Random.Range(-15, 15), 0, Random.Range(-15, 15));
            
            var ai = z.GetComponent<ZombieAI>() ?? z.AddComponent<ZombieAI>();
            ai.groundLayer = 1 << LayerMask.NameToLayer("Default");
            ai.detectionRange = 15f;
        }

        // Spawn Humans
        for(int i = 0; i < 8; i++)
        {
            GameObject h;
            if (humanPrefab != null) h = (GameObject)PrefabUtility.InstantiatePrefab(humanPrefab);
            else h = GameObject.CreatePrimitive(PrimitiveType.Capsule);

            h.name = "Human_" + i;
            h.tag = "Human";
            h.transform.position = new Vector3(Random.Range(-15, 15), 0, Random.Range(-15, 15));
            
            var ai = h.GetComponent<HumanAI>() ?? h.AddComponent<HumanAI>();
            ai.groundLayer = 1 << LayerMask.NameToLayer("Default");
            ai.fearRadius = 10f;
        }

        // Link scripts
        mechanics.visuals = holePlayer.GetComponent<HoleVisuals>() ?? holePlayer.AddComponent<HoleVisuals>();
        
        // Find Joystick
        Joystick joystick = Object.FindObjectOfType<Joystick>();
        if (joystick != null) joystickMover.joystick = joystick;

        // Initialize masking
        maskController.InitializeMaterial();
        maskController.SetRadius(mechanics.voidRadius * holePlayer.transform.localScale.x);

        Undo.RegisterCreatedObjectUndo(floor, "Setup Complete Scene");
        Undo.RegisterCreatedObjectUndo(holePlayer, "Setup Complete Scene");
        Selection.activeGameObject = holePlayer;
        
        Debug.Log("Hole System Setup Complete with Floor, Zombies and Humans!");
    }
}
