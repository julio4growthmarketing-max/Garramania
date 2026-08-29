using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PrizeDeliveryZone : MonoBehaviour
{
    [Header("Eventos de Entrega")]
    public UnityEvent<Prize> OnPrizeDelivered = new UnityEvent<Prize>();

    private readonly System.Collections.Generic.HashSet<Prize> processedPrizes = new System.Collections.Generic.HashSet<Prize>();

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
        if (prize == null || processedPrizes.Contains(prize)) return;
        if (prize.State != PrizeState.Dropped && prize.State != PrizeState.Slipping) return;
        processedPrizes.Add(prize);

        prize.MarkDelivered();
        Debug.Log($"[DeliveryZone] Prêmio entrou fisicamente na calha: {prize.prizeId}");
        OnPrizeDelivered?.Invoke(prize);

        // O prêmio entregue sai do monte físico; a reposição será feita pelo estoque vivo.
        Destroy(prize.gameObject, 0.8f);
    }
}