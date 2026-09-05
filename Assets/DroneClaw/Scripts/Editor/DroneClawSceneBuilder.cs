using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace DroneClaw.Editor
{
    /// <summary>
    /// Ferramenta de Editor para gerar automaticamente uma cena 3D jogável
    /// com o drone, a câmera, os bichinhos e a base de entrega,
    /// permitindo testar toda a física sem precisar configurar nada manualmente.
    /// </summary>
    public static class DroneClawSceneBuilder
    {
        [MenuItem("Tools/DroneClaw/Criar Cena de Protótipo do Drone 🚁")]
        public static void BuildPrototypeScene()
        {
            // Garante que as pastas existam
            string scenesDir = "Assets/DroneClaw/Scenes";
            if (!Directory.Exists(scenesDir))
            {
                Directory.CreateDirectory(scenesDir);
                AssetDatabase.Refresh();
            }

            // Cria uma nova cena limpa
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 1. Luz Direcional do Sol
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                sun.color = new Color(1.0f, 0.96f, 0.88f);
                sun.intensity = 1.25f;
            }

            // 2. Terreno / Chão do Bosque
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Meadow";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80x80m

            Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMat.color = new Color(0.28f, 0.55f, 0.22f); // Verde grama vivo
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // 3. Obstáculos e Árvores Estilizadas
            GameObject envGroup = new GameObject("Environment_Trees");
            CreateStylizedTree(envGroup.transform, new Vector3(8f, 0f, 10f));
            CreateStylizedTree(envGroup.transform, new Vector3(-12f, 0f, 6f));
            CreateStylizedTree(envGroup.transform, new Vector3(15f, 0f, -12f));
            CreateStylizedTree(envGroup.transform, new Vector3(-8f, 0f, -14f));

            // 4. Base de Resgate (Heliponto / Drop Zone)
            GameObject dropZoneObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dropZoneObj.name = "Rescue_DropZone";
            dropZoneObj.transform.position = new Vector3(0f, 0.05f, -6f);
            dropZoneObj.transform.localScale = new Vector3(5f, 0.1f, 5f);
            var dropCollider = dropZoneObj.GetComponent<Collider>();
            dropCollider.isTrigger = true;

            Material padMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            padMat.color = new Color(0.95f, 0.75f, 0.10f); // Amarelo vibrante de pista
            dropZoneObj.GetComponent<MeshRenderer>().sharedMaterial = padMat;

            var dropDelivery = dropZoneObj.AddComponent<DropZoneDelivery>();
            dropDelivery.zoneName = "Heliponto de Resgate Alpha";

            // 5. O Drone e seus Componentes
            GameObject droneObj = new GameObject("Player_Drone");
            droneObj.transform.position = new Vector3(0f, 3.5f, 0f);

            var rb = droneObj.AddComponent<Rigidbody>();
            rb.mass = 12f;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 3f;

            var flightCtrl = droneObj.AddComponent<DroneFlightController>();

            // Corpo Visual do Drone
            GameObject meshRoot = new GameObject("Drone_MeshRoot");
            meshRoot.transform.parent = droneObj.transform;
            meshRoot.transform.localPosition = Vector3.zero;
            flightCtrl.droneModelMesh = meshRoot.transform;

            // Chassis central
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Chassis";
            chassis.transform.parent = meshRoot.transform;
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f);
            Object.DestroyImmediate(chassis.GetComponent<Collider>());

            Material chassisMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            chassisMat.color = new Color(0.12f, 0.14f, 0.18f); // Grafite escuro
            chassis.GetComponent<MeshRenderer>().sharedMaterial = chassisMat;

            // 4 Hélices e braços do drone
            Transform[] propList = new Transform[4];
            Vector3[] armOffsets = new Vector3[]
            {
                new Vector3(0.55f, 0.08f, 0.55f),
                new Vector3(-0.55f, 0.08f, 0.55f),
                new Vector3(0.55f, 0.08f, -0.55f),
                new Vector3(-0.55f, 0.08f, -0.55f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prop.name = $"Propeller_{i + 1}";
                prop.transform.parent = meshRoot.transform;
                prop.transform.localPosition = armOffsets[i];
                prop.transform.localScale = new Vector3(0.45f, 0.02f, 0.08f);
                Object.DestroyImmediate(prop.GetComponent<Collider>());

                Material propMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                propMat.color = new Color(0.2f, 0.8f, 1.0f); // Ciano neon
                prop.GetComponent<MeshRenderer>().sharedMaterial = propMat;

                propList[i] = prop.transform;
            }
            flightCtrl.propellers = propList;

            // 6. Guincho e Garra
            var winch = droneObj.AddComponent<DroneClawWinch>();
            winch.winchAnchor = droneObj.transform;
            winch.groundLayers = ~0; // Tudo
            winch.creatureLayers = ~0;

            // Linha do cabo
            var cable = droneObj.AddComponent<LineRenderer>();
            Material cableMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cableMat.color = new Color(0.85f, 0.85f, 0.85f);
            cable.sharedMaterial = cableMat;
            winch.cableLine = cable;

            // Cabeça da garra
            GameObject clawHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clawHead.name = "Claw_Head";
            clawHead.transform.parent = droneObj.transform;
            clawHead.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            clawHead.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Object.DestroyImmediate(clawHead.GetComponent<Collider>());

            Material clawMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            clawMat.color = new Color(0.9f, 0.2f, 0.2f); // Garra vermelha vibrante
            clawHead.GetComponent<MeshRenderer>().sharedMaterial = clawMat;
            winch.clawHead = clawHead.transform;

            // Dedos da garra
            Transform[] fingers = new Transform[3];
            for (int f = 0; f < 3; f++)
            {
                GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                finger.name = $"Claw_Finger_{f + 1}";
                finger.transform.parent = clawHead.transform;
                float angle = f * 120f;
                finger.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.18f, -0.18f, 0f);
                finger.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
                finger.transform.localRotation = Quaternion.Euler(15f, angle, 0f);
                Object.DestroyImmediate(finger.GetComponent<Collider>());
                finger.GetComponent<MeshRenderer>().sharedMaterial = clawMat;
                fingers[f] = finger.transform;
            }
            winch.clawFingers = fingers;

            // 7. Câmera de 3ª Pessoa
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var chaseCam = mainCam.gameObject.AddComponent<DroneChaseCamera>();
                chaseCam.target = droneObj.transform;
                winch.chaseCamera = chaseCam;
            }

            // 8. Bichinhos da Fazendinha (Cube Pets)
            CreateCreature("Bichinho_Pintinho", new Vector3(0f, 0.1f, 4.5f), "animal-chick.fbx");
            CreateCreature("Bichinho_Vaquinha", new Vector3(2.0f, 0.1f, 5.5f), "animal-cow.fbx");
            CreateCreature("Bichinho_Porquinho", new Vector3(-2.2f, 0.1f, 5.5f), "animal-pig.fbx");
            CreateCreature("Bichinho_Coelhinho", new Vector3(3.5f, 0.1f, 7.5f), "animal-bunny.fbx");
            CreateCreature("Bichinho_Cachorrinho", new Vector3(-3.5f, 0.1f, 7.0f), "animal-dog.fbx");
            CreateCreature("Bichinho_Panda", new Vector3(1.8f, 0.1f, 9.0f), "animal-panda.fbx");
            CreateCreature("Bichinho_Raposinha", new Vector3(-1.8f, 0.1f, 9.5f), "animal-fox.fbx");

            // Salva a nova cena em disco
            string scenePath = $"{scenesDir}/DroneClaw_Prototype.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DroneClaw] ✅ Cena da Fazendinha criada com sucesso com o Teddy Bear em: {scenePath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("DroneClaw", "Cena criada com sucesso com o novo Ursinho TEDDY e os Cube Pets!\nClique em PLAY para pilotar e capturar!", "OK");
            }
        }

        private static void CreateCreature(string name, Vector3 pos, string fbxFilename = null, Color fallbackColor = default)
        {
            GameObject creatureObj = new GameObject(name);
            creatureObj.transform.position = pos;

            var cc = creatureObj.AddComponent<CharacterController>();
            cc.height = 0.65f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.32f, 0f);

            var agent = creatureObj.AddComponent<CreatureAgent>();
            agent.creatureName = name.Replace("Bichinho_", "");

            GameObject modelInstance = null;
            if (!string.IsNullOrEmpty(fbxFilename))
            {
                // Tenta na pasta raiz de Models ou dentro de CubePets
                string fbxPath = $"Assets/DroneClaw/Models/{fbxFilename}";
                if (!File.Exists(fbxPath))
                {
                    fbxPath = $"Assets/DroneClaw/Models/CubePets/Models/FBX format/{fbxFilename}";
                }

                GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxPrefab != null)
                {
                    modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                    modelInstance.name = "Model_Mesh";
                    modelInstance.transform.parent = creatureObj.transform;
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Frente correta
                    modelInstance.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);

                    // Se for dos CubePets, aplica o colormap
                    if (fbxFilename.StartsWith("animal-"))
                    {
                        string texPath = "Assets/DroneClaw/Models/CubePets/Models/FBX format/Textures/colormap.png";
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null)
                        {
                            Material petMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                            petMat.mainTexture = tex;
                            if (petMat.HasProperty("_BaseMap")) petMat.SetTexture("_BaseMap", tex);

                            foreach (var mr in modelInstance.GetComponentsInChildren<MeshRenderer>())
                            {
                                mr.sharedMaterial = petMat;
                            }
                        }
                    }
                }
            }

            if (modelInstance == null)
            {
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                modelInstance.name = "Model_Mesh";
                modelInstance.transform.parent = creatureObj.transform;
                modelInstance.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                modelInstance.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
                Object.DestroyImmediate(modelInstance.GetComponent<Collider>());

                Material creatureMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                creatureMat.color = fallbackColor == default ? Color.white : fallbackColor;
                modelInstance.GetComponent<MeshRenderer>().sharedMaterial = creatureMat;
            }

            agent.modelRoot = modelInstance.transform;
        }

        private static void CreateStylizedTree(Transform parent, Vector3 pos)
        {
            GameObject tree = new GameObject("Tree_Stylized");
            tree.transform.parent = parent;
            tree.transform.position = pos;

            // Tronco
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.parent = tree.transform;
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            trunk.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);

            Material trunkMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            trunkMat.color = new Color(0.45f, 0.28f, 0.15f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            // Copa da árvore (Esferas sobrepostas)
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.parent = tree.transform;
            leaves.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            leaves.transform.localScale = new Vector3(2.8f, 2.4f, 2.8f);

            Material leavesMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            leavesMat.color = new Color(0.18f, 0.65f, 0.25f);
            leaves.GetComponent<MeshRenderer>().sharedMaterial = leavesMat;
        }
    }
}
