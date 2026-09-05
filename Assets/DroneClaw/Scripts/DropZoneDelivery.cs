using UnityEngine;
using System;

namespace DroneClaw
{
    /// <summary>
    /// Zona de resgate / entrega (Ex: Heliponto, Caminhão de Pesquisa, ou Cápsula de Teletransporte).
    /// Quando o drone sobrevoa a área com um bichinho e o solta (ou encosta),
    /// a criatura é computada no santuário e salva na coleção.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DropZoneDelivery : MonoBehaviour
    {
        [Header("Identificação")]
        public string zoneName = "Base de Resgate do Bosque";

        [Header("Efeitos")]
        public ParticleSystem rescueParticles;
        public AudioSource audioSource;
        public AudioClip rescueSuccessClip;

        [Header("Estatísticas")]
        public int TotalRescuedCount { get; private set; } = 0;

        public event Action<CreatureAgent> OnCreatureRescued;

        private void OnTriggerEnter(Collider other)
        {
            CreatureAgent creature = other.GetComponentInParent<CreatureAgent>();
            if (creature != null && !creature.IsGrabbed && creature.CurrentState != CreatureState.Rescued)
            {
                ProcessRescue(creature);
            }
        }

        public void ProcessRescue(CreatureAgent creature)
        {
            TotalRescuedCount++;
            Debug.Log($"[DroneClaw] ✨ Criatura resgatada com sucesso: {creature.creatureName}! Total: {TotalRescuedCount}");

            if (rescueParticles != null)
            {
                rescueParticles.Play();
            }

            if (audioSource != null && rescueSuccessClip != null)
            {
                audioSource.PlayOneShot(rescueSuccessClip);
            }

            creature.OnRescued();
            OnCreatureRescued?.Invoke(creature);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
            Gizmos.DrawCube(transform.position, transform.localScale);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}
