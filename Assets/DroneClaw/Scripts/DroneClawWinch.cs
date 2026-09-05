using System.Collections;
using UnityEngine;

namespace DroneClaw
{
    public enum WinchState
    {
        Retracted,
        Lowering,
        Grabbing,
        Retracting
    }

    /// <summary>
    /// Gerencia o guincho e a garra suspensa sob o drone.
    /// Lança a garra verticalmente até o chão, agarra criaturas do tipo CreatureAgent
    /// e as recolhe suavemente até a altura de voo.
    /// </summary>
    public class DroneClawWinch : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Ponto de ancoragem do cabo na barriga do drone")]
        public Transform winchAnchor;

        [Tooltip("Objeto da Garra que desce e sobe")]
        public Transform clawHead;

        [Tooltip("Dedos articulados da garra (opcional para animação de abrir/fechar)")]
        public Transform[] clawFingers;

        [Tooltip("Linha do cabo de aço")]
        public LineRenderer cableLine;

        [Tooltip("Câmera do drone para ajuste de enquadramento")]
        public DroneChaseCamera chaseCamera;

        [Header("Parâmetros do Guincho")]
        public float lowerSpeed = 8.0f;
        public float retractSpeed = 6.0f;
        public float maxCableLength = 20.0f;
        public float grabRadius = 0.8f;
        public LayerMask groundLayers;
        public LayerMask creatureLayers;

        [Header("Áudio (Opcional)")]
        public AudioSource winchAudio;
        public AudioClip winchMotorClip;
        public AudioClip grabClip;
        public AudioClip releaseClip;

        // Estado atual
        public WinchState State { get; private set; } = WinchState.Retracted;
        public CreatureAgent CurrentGrabbedCreature { get; private set; }

        private Vector3 _clawRestLocalPos;
        private float _currentCableDistance;

        private void Start()
        {
            if (winchAnchor == null) winchAnchor = transform;

            if (clawHead != null)
            {
                _clawRestLocalPos = clawHead.localPosition;
            }

            if (cableLine != null)
            {
                cableLine.positionCount = 2;
                cableLine.useWorldSpace = true;
                cableLine.startWidth = 0.035f;
                cableLine.endWidth = 0.035f;
            }

            SetFingersOpen(false);
        }

        private void Update()
        {
            HandleInput();
            UpdateCableVisual();
        }

        private void HandleInput()
        {
            // Tecla de disparo do guincho (Botão Esquerdo do Mouse ou Tecla F ou Enter)
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
            {
                if (State == WinchState.Retracted)
                {
                    if (CurrentGrabbedCreature != null)
                    {
                        // Já está segurando alguém: solta (ex: em cima da zona de entrega)
                        ReleaseCurrentCreature();
                    }
                    else
                    {
                        // Dispara a descida da garra
                        StartCoroutine(WinchCycleRoutine());
                    }
                }
            }
        }

        private IEnumerator WinchCycleRoutine()
        {
            State = WinchState.Lowering;
            if (chaseCamera != null) chaseCamera.SetClawDeployed(true);
            SetFingersOpen(true);
            PlaySound(winchMotorClip, true);

            Vector3 startWorldPos = winchAnchor.position;
            clawHead.parent = null; // Libera da rotação do drone durante a descida vertical

            float currentLength = 0f;
            bool hitGround = false;

            // 1. Fase de descida
            while (currentLength < maxCableLength && !hitGround)
            {
                currentLength += lowerSpeed * Time.deltaTime;
                Vector3 targetClawPos = winchAnchor.position + Vector3.down * currentLength;

                // Verifica se encostou no chão
                if (Physics.Raycast(targetClawPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit, 0.6f, groundLayers))
                {
                    targetClawPos = groundHit.point + Vector3.up * 0.15f;
                    hitGround = true;
                }

                clawHead.position = targetClawPos;

                // Verifica se tocou numa criatura
                Collider[] hits = Physics.OverlapSphere(clawHead.position, grabRadius, creatureLayers);
                if (hits != null && hits.Length > 0)
                {
                    foreach (var hit in hits)
                    {
                        CreatureAgent creature = hit.GetComponentInParent<CreatureAgent>();
                        if (creature != null && !creature.IsGrabbed)
                        {
                            GrabCreature(creature);
                            hitGround = true;
                            break;
                        }
                    }
                }

                yield return null;
            }

            // 2. Fase de agarre (pausa rápida para fechar os dedos)
            State = WinchState.Grabbing;
            SetFingersOpen(false);
            PlaySound(grabClip, false);
            yield return new WaitForSeconds(0.4f);

            // 3. Fase de recolhimento
            State = WinchState.Retracting;
            PlaySound(winchMotorClip, true);

            while (Vector3.Distance(clawHead.position, winchAnchor.position) > 0.2f)
            {
                clawHead.position = Vector3.MoveTowards(clawHead.position, winchAnchor.position, retractSpeed * Time.deltaTime);

                // Se estiver carregando uma criatura, ela sobe junto presa
                if (CurrentGrabbedCreature != null)
                {
                    CurrentGrabbedCreature.transform.position = clawHead.position + Vector3.down * 0.4f;
                }

                yield return null;
            }

            // Garra recolhida com sucesso
            clawHead.parent = winchAnchor;
            clawHead.localPosition = _clawRestLocalPos;
            clawHead.localRotation = Quaternion.identity;

            if (winchAudio != null && winchAudio.loop) winchAudio.Stop();
            if (chaseCamera != null) chaseCamera.SetClawDeployed(false);

            State = WinchState.Retracted;
        }

        private void GrabCreature(CreatureAgent creature)
        {
            CurrentGrabbedCreature = creature;
            creature.OnGrabbedByClaw(clawHead);
        }

        public void ReleaseCurrentCreature()
        {
            if (CurrentGrabbedCreature == null) return;

            PlaySound(releaseClip, false);
            SetFingersOpen(true);
            CurrentGrabbedCreature.OnReleased();
            CurrentGrabbedCreature = null;

            StartCoroutine(CloseFingersAfterDelay(0.5f));
        }

        private IEnumerator CloseFingersAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetFingersOpen(false);
        }

        private void SetFingersOpen(bool open)
        {
            if (clawFingers == null || clawFingers.Length == 0) return;

            float angle = open ? -30f : 15f;
            foreach (var finger in clawFingers)
            {
                if (finger != null)
                {
                    finger.localRotation = Quaternion.Euler(angle, 0f, 0f);
                }
            }
        }

        private void UpdateCableVisual()
        {
            if (cableLine == null || clawHead == null || winchAnchor == null) return;

            cableLine.SetPosition(0, winchAnchor.position);
            cableLine.SetPosition(1, clawHead.position);
        }

        private void PlaySound(AudioClip clip, bool loop)
        {
            if (winchAudio == null || clip == null) return;
            winchAudio.clip = clip;
            winchAudio.loop = loop;
            winchAudio.Play();
        }

        private void OnDrawGizmosSelected()
        {
            if (clawHead != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(clawHead.position, grabRadius);
            }
        }
    }
}
