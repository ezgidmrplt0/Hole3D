using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;

public class GodotToUnityBridge : EditorWindow
{
    [MenuItem("Tools/Magic Hole Setup (Direct)")]
    public static void SetupHoleSystem()
    {
        // 1. Find or Create Ground Plane
        GameObject floor = GameObject.Find("Hole_Compatible_Floor");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Hole_Compatible_Floor";
            Undo.RegisterCreatedObjectUndo(floor, "Setup Hole Floor");
        }
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(5, 1, 5); 
        floor.layer = LayerMask.NameToLayer("Default");

        Renderer floorRen = floor.GetComponent<Renderer>();
        Material groundMat = floorRen.sharedMaterial;
        if (groundMat == null || groundMat.shader.name != "Custom/HoleMaskGround")
        {
            groundMat = new Material(Shader.Find("Custom/HoleMaskGround"));
            groundMat.color = Color.white;
            floorRen.sharedMaterial = groundMat;
        }

        // 2. Find or Create Root Player Object
        GameObject holePlayer = GameObject.Find("Hole_Player");
        if (holePlayer == null)
        {
            holePlayer = new GameObject("Hole_Player");
            Undo.RegisterCreatedObjectUndo(holePlayer, "Setup Hole Player");
        }
        holePlayer.tag = "Player";
        if (holePlayer.transform.position.y < 0.05f) holePlayer.transform.position = new Vector3(0, 0.05f, 0);
        
        var rb = holePlayer.GetComponent<Rigidbody>() ?? holePlayer.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        
        var joystickMover = holePlayer.GetComponent<HoleMoveJoystick>() ?? holePlayer.AddComponent<HoleMoveJoystick>();
        joystickMover.moveSpeed = 8f;
        
        var mechanics = holePlayer.GetComponent<HoleMechanics>() ?? holePlayer.AddComponent<HoleMechanics>();
        var maskController = holePlayer.GetComponent<HoleMaskController>() ?? holePlayer.AddComponent<HoleMaskController>();
        // maskController.groundMaterial = groundMat; // Removed as per Global Shader refactor

        // 3. Find or Create Hole_Rim
        Transform rimTrans = holePlayer.transform.Find("Hole_Rim");
        GameObject holeRim = rimTrans != null ? rimTrans.gameObject : null;
        if (holeRim == null)
        {
            holeRim = GameObject.CreatePrimitive(PrimitiveType.Plane);
            holeRim.name = "Hole_Rim";
            holeRim.transform.SetParent(holePlayer.transform);
        }
        holeRim.transform.localPosition = new Vector3(0, 0.01f, 0);
        holeRim.transform.localScale = new Vector3(0.2f, 1, 0.2f); 
        if (holeRim.GetComponent<MeshCollider>() != null) DestroyImmediate(holeRim.GetComponent<MeshCollider>());
        
        Renderer rimRenderer = holeRim.GetComponent<Renderer>();
        if (rimRenderer.sharedMaterial == null || rimRenderer.sharedMaterial.shader.name != "Custom/HoleHollowRim")
        {
            Material rimMat = new Material(Shader.Find("Custom/HoleHollowRim"));
            rimMat.SetColor("_Color", new Color(0, 0.5f, 1, 1));
            rimMat.SetFloat("_InsideRadius", 0.9f);
            rimMat.SetFloat("_OutsideRadius", 1.0f);
            rimRenderer.sharedMaterial = rimMat;
        }

        // 4. PhysicsMaskArea
        Transform physicsAreaTrans = holePlayer.transform.Find("PhysicsMaskArea");
        GameObject physicsMaskArea = physicsAreaTrans != null ? physicsAreaTrans.gameObject : null;
        if (physicsMaskArea == null)
        {
            physicsMaskArea = new GameObject("PhysicsMaskArea");
            physicsMaskArea.transform.SetParent(holePlayer.transform);
        }
        physicsMaskArea.transform.localPosition = Vector3.zero;
        
        SphereCollider physicsTrigger = physicsMaskArea.GetComponent<SphereCollider>() ?? physicsMaskArea.AddComponent<SphereCollider>();
        physicsTrigger.isTrigger = true;
        physicsTrigger.radius = 1.0f;
        
        var physicsHandler = physicsMaskArea.GetComponent<HolePhysicsHandler>() ?? physicsMaskArea.AddComponent<HolePhysicsHandler>();
        physicsHandler.floorCollider = floor.GetComponent<Collider>();

        // 5. CollectionArea
        Transform collectionTrans = holePlayer.transform.Find("CollectionArea");
        GameObject collectionArea = collectionTrans != null ? collectionTrans.gameObject : null;
        if (collectionArea == null)
        {
            collectionArea = new GameObject("CollectionArea");
            collectionArea.transform.SetParent(holePlayer.transform);
        }
        collectionArea.transform.localPosition = new Vector3(0, -5f, 0);
        
        BoxCollider collectionTrigger = collectionArea.GetComponent<BoxCollider>() ?? collectionArea.AddComponent<BoxCollider>();
        collectionTrigger.isTrigger = true;
        collectionTrigger.size = new Vector3(2, 1, 2);

        // 6. Progress Bar (World Space Canvas)
        Transform canvasTrans = holePlayer.transform.Find("HoleProgressCanvas");
        GameObject canvasObj = canvasTrans != null ? canvasTrans.gameObject : null;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("HoleProgressCanvas");
            canvasObj.transform.SetParent(holePlayer.transform);
        }
        canvasObj.transform.localPosition = new Vector3(0, 0.02f, 0); 
        canvasObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
        canvasObj.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObj.GetComponent<Canvas>() ?? canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>() == null) canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(250, 250);

        // Background / Progress Fill (Update existing or create)
        Image fillImg = null;
        Transform fillTrans = canvasObj.transform.Find("ProgressFill");
        if (fillTrans == null)
        {
            // Create BG
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.3f);
            Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Layers Round.png");
            if (circleSprite != null) bgImg.sprite = circleSprite;
            bgObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 240);

            // Create Fill
            GameObject fillObj = new GameObject("ProgressFill");
            fillObj.transform.SetParent(canvasObj.transform, false);
            fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0, 1, 0.2f, 1f);
            if (circleSprite != null) fillImg.sprite = circleSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 240);
        }
        else
        {
            fillImg = fillTrans.GetComponent<Image>();
        }

        // Link scripts final
        var vizScript = holePlayer.GetComponent<HoleVisuals>() ?? holePlayer.AddComponent<HoleVisuals>();
        vizScript.progressImage = fillImg;
        mechanics.visuals = vizScript;
        
        Joystick joystick = Object.FindObjectOfType<Joystick>();
        if (joystick != null) joystickMover.joystick = joystick;

        // maskController.InitializeMaterial(); // Removed as per Global Shader refactor
        maskController.SetRadius(mechanics.voidRadius * holePlayer.transform.localScale.x);

        Selection.activeGameObject = holePlayer;
        
        Debug.Log("Hole System Setup Complete with Floor, Zombies and Humans!");
    }
}
