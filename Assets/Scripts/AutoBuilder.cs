using UnityEngine;

public class AutoBuilder : MonoBehaviour
{
    void Start()
    {
        // Criação do Chão
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Chao_Gerado_Pela_IA";
        floor.transform.position = new Vector3(0, -3f, 0);
        floor.transform.localScale = new Vector3(5, 0.2f, 5);

        // Criação do Material de Vidro Transparente para o URP
        Material glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        glassMat.SetFloat("_Surface", 1); // 1 = Transparent
        glassMat.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 0.3f)); // Azul translúcido neon
        glassMat.SetFloat("_Metallic", 0.5f);
        glassMat.SetFloat("_Smoothness", 0.9f); // Reflexo do vidro

        // Construindo as Paredes de Vidro
        CreateWall("ParedeFundo_IA", new Vector3(0, 0, 2.5f), new Vector3(5, 6, 0.1f), glassMat);
        CreateWall("ParedeEsquerda_IA", new Vector3(-2.5f, 0, 0), new Vector3(0.1f, 6, 5), glassMat);
        CreateWall("ParedeDireita_IA", new Vector3(2.5f, 0, 0), new Vector3(0.1f, 6, 5), glassMat);
        
        Debug.Log("🤖 Antigravity: Cenário e Vidros construídos com sucesso!");
    }

    void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = mat;
    }
}