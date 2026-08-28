using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PrizeDeliveryZone : MonoBehaviour
{
    [Header("Eventos de Entrega")]
    public UnityEvent<Prize> OnPrizeDelivered = new UnityEvent<Prize>();

    void Awake()
    {
        if (OnPrizeDelivered == null) OnPrizeDelivered = new UnityEvent<Prize>();

        // Garante que o collider seja um trigger para detectar a passagem do urso
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // Busca o componente Prize no objeto ou nos pais (caso o collider esteja num filho)
        Prize prize = other.GetComponentInParent<Prize>();
        
        if (prize != null && prize.State != PrizeState.Delivered)
        {
            prize.MarkDelivered();
            
            Debug.Log($"[DeliveryZone] Prêmio capturado com sucesso: {prize.prizeId}");
            
            // Dispara o evento antes de remover o objeto para preservar o nome e os dados do prêmio.
            OnPrizeDelivered?.Invoke(prize);

            // O prêmio entregue sai do monte físico; a reposição será feita pelo estoque vivo.
            Destroy(prize.gameObject, 0.8f);
        }
    }
}