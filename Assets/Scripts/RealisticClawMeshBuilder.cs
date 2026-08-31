using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construtor 3D de Alta Fidelidade para a Garra Mecânica de Fliperama (Estilo Fab.com).
/// 
/// Características:
/// - 100% Mecânica e Industrial: Aço cromado espelhado, titânio escovado e buchas em latão (SEM luzes/neon).
/// - 3 Pinças Curvadas (Curved Blades) com perfil em gota/cesto (double-sided para visibilidade total).
/// - Mecanismo de 4 barras com 3 bielas/tirantes (push-rods) articulados e pistão deslizante central.
/// </summary>
public static class RealisticClawMeshBuilder
{
    public struct ClawRig
    {
        public GameObject Root;
        public Transform VisualContainer;
        public Transform[] Prongs;
        public Transform CentralPiston;
        public Transform LowerHub;
        public Transform CarrySocket;
        public Transform[] PushRods;
        public Action<float> SetOpenAmount; // 0.0f = fechada (8°), 1.0f = aberta (48°)
    }

    public static ClawRig Build(Transform visualContainer)
    {
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. MATERIAIS PBR METÁLICOS INDUSTRIAIS (SEM LUZES, PURO METAL)
        // Aço Cromado Espelhado (Lâminas da Garra, Pistão Central, Hastes das Bielas)
        Material mChrome = new Material(urpShader);
        mChrome.name = "Mat_Claw_MirrorChrome";
        mChrome.color = new Color(0.90f, 0.92f, 0.94f, 1.0f);
        mChrome.SetFloat("_Metallic", 0.95f);
        mChrome.SetFloat("_Smoothness", 0.90f);
        mChrome.SetFloat("_Cull", 0f); // Double-Sided Rendering garantido no shader

        // Titânio Escovado / Grafite Industrial (Cabeçote Cilindro, Flanges e Braçadeiras)
        Material mTitanium = new Material(urpShader);
        mTitanium.name = "Mat_Claw_BrushedTitanium";
        mTitanium.color = new Color(0.18f, 0.19f, 0.22f, 1.0f);
        mTitanium.SetFloat("_Metallic", 0.88f);
        mTitanium.SetFloat("_Smoothness", 0.65f);
        mTitanium.SetFloat("_Cull", 0f);

        // Latão / Bronze Dourado (Pinos de Articulação, Parafusos e Esferas de Mancal)
        Material mBrass = new Material(urpShader);
        mBrass.name = "Mat_Claw_BrassMachined";
        mBrass.color = new Color(0.85f, 0.68f, 0.22f, 1.0f);
        mBrass.SetFloat("_Metallic", 0.85f);
        mBrass.SetFloat("_Smoothness", 0.72f);

        // ==================== 2. CABEÇOTE SUPERIOR (HOUSING) ====================
        // Olhal Superior onde o cabo de aço é ancorado
        GameObject eyelet = CreateSphere("Eyelet_Top", visualContainer, new Vector3(0f, 0.52f, 0f), new Vector3(0.12f, 0.12f, 0.12f), mChrome);

        // Tampa / Flange Cônica Superior
        GameObject topFlange = CreateCylinder("Housing_TopCap", visualContainer, new Vector3(0f, 0.46f, 0f), new Vector3(0.38f, 0.05f, 0.38f), mTitanium);

        // Cilindro Principal do Cabeçote (Carcaça em Titânio Escovado)
        GameObject mainHousing = CreateCylinder("Housing_MainCylinder", visualContainer, new Vector3(0f, 0.25f, 0f), new Vector3(0.32f, 0.38f, 0.32f), mTitanium);

        // Anel de reforço usinado no topo
        CreateCylinder("Housing_TrimRing_Top", visualContainer, new Vector3(0f, 0.40f, 0f), new Vector3(0.35f, 0.03f, 0.35f), mChrome);

        // Anel de reforço usinado na base
        CreateCylinder("Housing_TrimRing_Bottom", visualContainer, new Vector3(0f, 0.10f, 0f), new Vector3(0.35f, 0.03f, 0.35f), mChrome);

        // Colar Inferior com Suportes das Bielas
        GameObject upperCollar = CreateCylinder("Housing_UpperCollar", visualContainer, new Vector3(0f, 0.04f, 0f), new Vector3(0.36f, 0.06f, 0.36f), mTitanium);

        // ==================== 3. PISTÃO CENTRAL DESLIZANTE ====================
        // Haste cilíndrica central em cromo espelhado que corre por dentro do cabeçote
        GameObject pistonObj = CreateCylinder("Central_PistonShaft", visualContainer, new Vector3(0f, -0.06f, 0f), new Vector3(0.09f, 0.42f, 0.09f), mChrome);

        // Disco Atuador Inferior (Hub de fixação das 3 garras)
        GameObject lowerHub = new GameObject("Lower_Actuator_Hub");
        lowerHub.transform.SetParent(visualContainer, false);
        lowerHub.transform.localPosition = new Vector3(0f, -0.22f, 0f);

        CreateCylinder("LowerHub_Disc", lowerHub.transform, Vector3.zero, new Vector3(0.32f, 0.06f, 0.32f), mTitanium);
        CreateCylinder("LowerHub_Nut", lowerHub.transform, new Vector3(0f, -0.035f, 0f), new Vector3(0.14f, 0.03f, 0.14f), mBrass);

        // Socket do Prêmio: ajustado para que as 3 pinças curvem e belisquem a cabeça/ombros do boneco,
        // deixando o corpo e as perninhas penduradas livres para baixo (física autêntica de garra)
        GameObject carrySocketObj = new GameObject("CarrySocket_Premio");
        carrySocketObj.transform.SetParent(visualContainer, false);
        carrySocketObj.transform.localPosition = new Vector3(0f, -2.42f, 0f);
        carrySocketObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // ==================== 4. AS 3 PINÇAS CURVADAS E BIELAS ====================
        Mesh curvedBladeMesh = GenerateCurvedBladeMesh();

        Transform[] prongs = new Transform[3];
        Transform[] pushRods = new Transform[3];
        Transform[] upperAnchorPoints = new Transform[3];
        Transform[] armAnchorPoints = new Transform[3];
        Transform[] rodCylinderBodies = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            float radialAngle = i * 120f;
            Quaternion radialRot = Quaternion.Euler(0f, radialAngle, 0f);

            // A. Suporte da Biela no Colar Superior do Cabeçote
            GameObject upperEar = new GameObject($"UpperCollar_Ear_{i}");
            upperEar.transform.SetParent(visualContainer, false);
            upperEar.transform.localPosition = radialRot * new Vector3(0f, 0.04f, 0.165f);
            upperEar.transform.localRotation = radialRot;

            GameObject earBracket = CreateCylinder("EarBracket", upperEar.transform, Vector3.zero, new Vector3(0.045f, 0.035f, 0.045f), mTitanium);
            earBracket.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            CreateCylinder("UpperPin", upperEar.transform, Vector3.zero, new Vector3(0.020f, 0.055f, 0.020f), mBrass);
            upperAnchorPoints[i] = upperEar.transform;

            // B. Pivô Articulado do Braço no Disco Inferior
            GameObject prongPivot = new GameObject($"ProngPivot_{i}");
            prongPivot.transform.SetParent(lowerHub.transform, false);
            prongPivot.transform.localPosition = radialRot * new Vector3(0f, 0f, 0.140f);
            prongPivot.transform.localRotation = radialRot;

            // Dobradiça base do ombro no disco inferior
            GameObject baseHinge = CreateCylinder("HingeBracket", prongPivot.transform, Vector3.zero, new Vector3(0.065f, 0.045f, 0.065f), mTitanium);
            baseHinge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject hingePin = CreateCylinder("HingePin", prongPivot.transform, Vector3.zero, new Vector3(0.025f, 0.065f, 0.025f), mBrass);
            hingePin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Braço Curvado em Aço Cromado (A PINÇA REAL)
            GameObject bladeObj = new GameObject("CurvedBlade_Steel");
            bladeObj.transform.SetParent(prongPivot.transform, false);
            bladeObj.transform.localPosition = Vector3.zero;
            bladeObj.transform.localRotation = Quaternion.identity;

            MeshFilter mf = bladeObj.AddComponent<MeshFilter>();
            mf.sharedMesh = curvedBladeMesh;
            MeshRenderer mr = bladeObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mChrome;

            // --- FÍSICA REALISTA: Colliders nos dentes da garra para empurrar e interagir com o monte ---
            // 1. Collider Superior da Lâmina (cobre o arco superior da curva)
            GameObject colUpper = new GameObject("ProngCollider_Upper");
            colUpper.transform.SetParent(bladeObj.transform, false);
            colUpper.transform.localPosition = new Vector3(0f, -0.26f, 0.28f);
            colUpper.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            CapsuleCollider capUpper = colUpper.AddComponent<CapsuleCollider>();
            capUpper.radius = 0.052f;
            capUpper.height = 0.36f;
            capUpper.direction = 2; // Z-axis along curve
            capUpper.isTrigger = false;

            // 2. Collider Inferior da Lâmina (cobre a descida do cesto)
            GameObject colLower = new GameObject("ProngCollider_Lower");
            colLower.transform.SetParent(bladeObj.transform, false);
            colLower.transform.localPosition = new Vector3(0f, -0.66f, 0.36f);
            colLower.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            CapsuleCollider capLower = colLower.AddComponent<CapsuleCollider>();
            capLower.radius = 0.048f;
            capLower.height = 0.44f;
            capLower.direction = 2; // Z-axis
            capLower.isTrigger = false;

            // 3. Collider da Ponta/Garra (o dente em colher que fecha e belisca)
            GameObject colTip = new GameObject("ProngCollider_Tip");
            colTip.transform.SetParent(bladeObj.transform, false);
            colTip.transform.localPosition = new Vector3(0f, -1.02f, 0.04f);
            SphereCollider capTip = colTip.AddComponent<SphereCollider>();
            capTip.radius = 0.062f;
            capTip.isTrigger = false;

            // Âncora da Biela no flanco/curva do braço
            GameObject armEar = new GameObject("Arm_LinkageAnchor");
            armEar.transform.SetParent(prongPivot.transform, false);
            armEar.transform.localPosition = new Vector3(0f, -0.28f, 0.32f);

            GameObject armPin = CreateCylinder("ArmPin", armEar.transform, Vector3.zero, new Vector3(0.022f, 0.055f, 0.022f), mBrass);
            armPin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            armAnchorPoints[i] = armEar.transform;

            // C. Biela Articulada (Push-Rod)
            GameObject rodContainer = new GameObject($"PushRod_Linkage_{i}");
            rodContainer.transform.SetParent(visualContainer, false);

            // O cilindro da haste conecta exatamente da origem do rodContainer até Z = dist
            GameObject rodBody = CreateCylinder("RodCylinder", rodContainer.transform, Vector3.zero, Vector3.one, mChrome);
            rodBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Terminais esféricos nas extremidades da biela
            CreateSphere("Bearing_Top", rodContainer.transform, Vector3.zero, new Vector3(0.042f, 0.042f, 0.042f), mBrass);
            GameObject bottomBearing = CreateSphere("Bearing_Bottom", rodContainer.transform, Vector3.zero, new Vector3(0.042f, 0.042f, 0.042f), mBrass);

            pushRods[i] = rodContainer.transform;
            rodCylinderBodies[i] = rodBody.transform;
            prongs[i] = prongPivot.transform;
        }

        // ==================== 5. ATUALIZAÇÃO CINEMÁTICA ====================
        // openFactor: 1.0 = totalmente aberta (-38°), 0.0 = totalmente fechada (+4°)
        Action<float> updateKinematics = (float openFactor) =>
        {
            openFactor = Mathf.Clamp01(openFactor);
            // Angulação correta de fliperama: aberta se espalha para fora (-40°), fechada converge e curva para dentro (+14°)
            float targetAngle = Mathf.Lerp(14f, -40f, openFactor);

            // Pistão e disco inferior empurram as hastes ao abrir e recolhem ao fechar
            float pistonOffset = Mathf.Lerp(0.015f, -0.040f, openFactor);
            lowerHub.transform.localPosition = new Vector3(0f, -0.22f + pistonOffset, 0f);
            pistonObj.transform.localPosition = new Vector3(0f, -0.06f + (pistonOffset * 0.5f), 0f);

            for (int i = 0; i < 3; i++)
            {
                float radialAngle = i * 120f;
                prongs[i].localRotation = Quaternion.Euler(0f, radialAngle, 0f) * Quaternion.Euler(targetAngle, 0f, 0f);

                // Conecta a biela perfeitamente entre o colar superior e a lâmina
                Vector3 pTop = upperAnchorPoints[i].position;
                Vector3 pArm = armAnchorPoints[i].position;
                Vector3 dir = pArm - pTop;
                float dist = dir.magnitude;

                if (dist > 0.005f)
                {
                    pushRods[i].position = pTop;
                    pushRods[i].rotation = Quaternion.LookRotation(dir);

                    // Cilindro da biela: centro em dist/2, escala em Y = dist/2
                    rodCylinderBodies[i].localPosition = new Vector3(0f, 0f, dist * 0.5f);
                    rodCylinderBodies[i].localScale = new Vector3(0.028f, dist * 0.5f, 0.028f);

                    Transform bottomBearing = pushRods[i].Find("Bearing_Bottom");
                    if (bottomBearing != null)
                    {
                        bottomBearing.localPosition = new Vector3(0f, 0f, dist);
                    }
                }
            }
        };

        // Inicia aberta para o jogo
        updateKinematics(1.0f);

        return new ClawRig
        {
            Root = visualContainer.gameObject,
            VisualContainer = visualContainer,
            Prongs = prongs,
            CentralPiston = pistonObj.transform,
            LowerHub = lowerHub.transform,
            CarrySocket = carrySocketObj.transform,
            PushRods = pushRods,
            SetOpenAmount = updateKinematics
        };
    }

    /// <summary>
    /// Gera a malha 3D sólida, suave e curvada em lâmina de aço espelhado da garra.
    /// Polígonos são criados com faces externas e internas (Double-Sided) para visibilidade absoluta.
    /// </summary>
    private static Mesh GenerateCurvedBladeMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Curved_Arcade_Prong_Blade";

        // Curva da espinha dorsal em perfil de gota/cesto (X = profundidade para baixo, Y = raio para fora)
        Vector2[] spinePoints = new Vector2[]
        {
            new Vector2(0.00f, 0.00f),   // 0. Pivô na base da dobradiça
            new Vector2(0.04f, 0.07f),   // 1. Ombro arqueado para cima e fora
            new Vector2(0.12f, 0.18f),   // 2. Arco externo
            new Vector2(0.24f, 0.32f),   // 3. Flanco superior da gota
            new Vector2(0.38f, 0.42f),   // 4. Barriga máxima da curva externa
            new Vector2(0.54f, 0.44f),   // 5. Descida externa ampla
            new Vector2(0.70f, 0.38f),   // 6. Início da curvatura de retorno
            new Vector2(0.84f, 0.26f),   // 7. Envergadura para dentro
            new Vector2(0.96f, 0.12f),   // 8. Afunilamento em direção ao centro
            new Vector2(1.04f, 0.02f)    // 9. Ponta chanfrada em colher que fecha o cesto
        };

        int segments = spinePoints.Length;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            Vector2 p = spinePoints[i];
            float prog = (float)i / (segments - 1);

            // Largura (X local) e Espessura (Z/Y perpendicular)
            float w = Mathf.Lerp(0.075f, 0.038f, prog);
            float t = Mathf.Lerp(0.042f, 0.022f, prog);

            Vector2 tangent;
            if (i == 0) tangent = (spinePoints[1] - spinePoints[0]).normalized;
            else if (i == segments - 1) tangent = (spinePoints[segments - 1] - spinePoints[segments - 2]).normalized;
            else tangent = (spinePoints[i + 1] - spinePoints[i - 1]).normalized;

            Vector2 normal2D = new Vector2(-tangent.y, tangent.x);

            Vector3 center = new Vector3(0f, -p.x, p.y);
            Vector3 upDir = new Vector3(0f, -normal2D.x, normal2D.y);
            Vector3 rightDir = Vector3.right;

            Vector3 v0 = center + (-rightDir * w) + (upDir * t); // Top-Left
            Vector3 v1 = center + (rightDir * w) + (upDir * t);  // Top-Right
            Vector3 v2 = center + (rightDir * w) + (-upDir * t); // Bottom-Right
            Vector3 v3 = center + (-rightDir * w) + (-upDir * t);// Bottom-Left

            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            uvs.Add(new Vector2(0.0f, prog));
            uvs.Add(new Vector2(0.33f, prog));
            uvs.Add(new Vector2(0.66f, prog));
            uvs.Add(new Vector2(1.0f, prog));

            if (i > 0)
            {
                int prev = (i - 1) * 4;
                int curr = i * 4;

                // --- FACES EXTERNAS ---
                // Top (+upDir)
                triangles.Add(prev + 0); triangles.Add(curr + 0); triangles.Add(curr + 1);
                triangles.Add(prev + 0); triangles.Add(curr + 1); triangles.Add(prev + 1);

                // Right (+rightDir)
                triangles.Add(prev + 1); triangles.Add(curr + 1); triangles.Add(curr + 2);
                triangles.Add(prev + 1); triangles.Add(curr + 2); triangles.Add(prev + 2);

                // Bottom (-upDir)
                triangles.Add(prev + 2); triangles.Add(curr + 2); triangles.Add(curr + 3);
                triangles.Add(prev + 2); triangles.Add(curr + 3); triangles.Add(prev + 3);

                // Left (-rightDir)
                triangles.Add(prev + 3); triangles.Add(curr + 3); triangles.Add(curr + 0);
                triangles.Add(prev + 3); triangles.Add(curr + 0); triangles.Add(prev + 0);

                // --- FACES INTERNAS (Double-Sided para imunidade a culling) ---
                // Top Invertido
                triangles.Add(prev + 0); triangles.Add(curr + 1); triangles.Add(curr + 0);
                triangles.Add(prev + 0); triangles.Add(prev + 1); triangles.Add(curr + 1);

                // Right Invertido
                triangles.Add(prev + 1); triangles.Add(curr + 2); triangles.Add(curr + 1);
                triangles.Add(prev + 1); triangles.Add(prev + 2); triangles.Add(curr + 2);

                // Bottom Invertido
                triangles.Add(prev + 2); triangles.Add(curr + 3); triangles.Add(curr + 2);
                triangles.Add(prev + 2); triangles.Add(prev + 3); triangles.Add(curr + 3);

                // Left Invertido
                triangles.Add(prev + 3); triangles.Add(curr + 0); triangles.Add(curr + 3);
                triangles.Add(prev + 3); triangles.Add(prev + 0); triangles.Add(curr + 0);
            }
        }

        // Tampas das pontas
        int last = (segments - 1) * 4;
        // Tampa da ponta
        triangles.Add(last + 0); triangles.Add(last + 2); triangles.Add(last + 1);
        triangles.Add(last + 0); triangles.Add(last + 3); triangles.Add(last + 2);
        triangles.Add(last + 0); triangles.Add(last + 1); triangles.Add(last + 2);
        triangles.Add(last + 0); triangles.Add(last + 2); triangles.Add(last + 3);

        // Tampa da raiz (ombro)
        triangles.Add(0); triangles.Add(1); triangles.Add(2);
        triangles.Add(0); triangles.Add(2); triangles.Add(3);
        triangles.Add(0); triangles.Add(2); triangles.Add(1);
        triangles.Add(0); triangles.Add(3); triangles.Add(2);

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().material = mat;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().material = mat;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        return go;
    }
}
