using UnityEngine;

/// <summary>
/// Controla o movimento mecânico sincronizado do guindaste (Gantry) no teto.
/// Acompanha a garra nos eixos X e Z, simulando os trilhos de um guindaste elétrico real.
/// </summary>
public class GantryFollower : MonoBehaviour
{
    private Transform clawTransform;
    public Transform crossbar;

    void Start()
    {
        ClawController claw = FindFirstObjectByType<ClawController>();
        if (claw != null)
        {
            clawTransform = claw.transform;
        }

        if (crossbar == null)
        {
            GameObject cb = GameObject.Find("Viga_Gantry_X");
            if (cb != null) crossbar = cb.transform;
        }
    }

    void LateUpdate()
    {
        if (clawTransform == null) return;

        // O carrinho motorizado desliza no teto acompanhando a garra
        transform.position = new Vector3(clawTransform.position.x, 2.85f, clawTransform.position.z);

        // A viga transversal se move apenas no eixo Z pelos trilhos laterais
        if (crossbar != null)
        {
            crossbar.position = new Vector3(0f, 2.86f, clawTransform.position.z);
        }
    }
}
