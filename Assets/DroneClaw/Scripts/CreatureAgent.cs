using System.Collections;
using UnityEngine;

namespace DroneClaw
{
    public enum CreatureState
    {
        Idle,
        Wandering,
        Alert,
        Grabbed,
        Rescued
    }

    /// <summary>
    /// Inteligência artificial e animações procedurais para os bichinhos terrestres.
    /// Vagam pelo cenário, olham para o drone quando ele se aproxima, reagem ao agarre
    /// e realizam pequenos pulinhos (squash & stretch) procedurais.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CreatureAgent : MonoBehaviour
    {
        [Header("Identidade")]
        public string creatureName = "Coelhinho do Bosque";
        public string speciesId = "Rabbit_Meadow";

        [Header("Movimento e Patrulha")]
        public float walkSpeed = 1.8f;
        public float wanderRadius = 12.0f;
        public float minIdleDuration = 2.0f;
        public float maxIdleDuration = 5.0f;

        [Header("Reação ao Drone")]
        public float alertDistance = 6.0f;

        [Header("Efeitos Visuais Procedurais")]
        [Tooltip("Transform do modelo da criatura para animações de pulinho e inclinação")]
        public Transform modelRoot;

        // Estado
        public CreatureState CurrentState { get; private set; } = CreatureState.Idle;
        public bool IsGrabbed => CurrentState == CreatureState.Grabbed;

        private CharacterController _cc;
        private Vector3 _homeOrigin;
        private Vector3 _targetDestination;
        private float _stateTimer;
        private float _hopTimer;
        private Transform _droneRef;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _homeOrigin = transform.position;
            if (modelRoot == null && transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }
        }

        private void Start()
        {
            // Busca o drone na cena para verificação de proximidade
            var drone = FindFirstObjectByType<DroneFlightController>();
            if (drone != null) _droneRef = drone.transform;

            SetState(CreatureState.Idle);
        }

        private void Update()
        {
            if (CurrentState == CreatureState.Grabbed || CurrentState == CreatureState.Rescued)
            {
                AnimateGrabbedWiggle();
                return;
            }

            CheckDroneProximity();

            switch (CurrentState)
            {
                case CreatureState.Idle:
                    UpdateIdleState();
                    break;
                case CreatureState.Wandering:
                    UpdateWanderState();
                    break;
                case CreatureState.Alert:
                    UpdateAlertState();
                    break;
            }

            ApplyGravity();
        }

        private void SetState(CreatureState newState)
        {
            CurrentState = newState;
            _stateTimer = 0f;

            if (newState == CreatureState.Idle)
            {
                _stateTimer = Random.Range(minIdleDuration, maxIdleDuration);
            }
            else if (newState == CreatureState.Wandering)
            {
                PickNewWanderDestination();
            }
        }

        private void CheckDroneProximity()
        {
            if (_droneRef == null || CurrentState == CreatureState.Alert) return;

            float distToDrone = Vector3.Distance(transform.position, _droneRef.position);
            if (distToDrone < alertDistance && _droneRef.position.y < transform.position.y + 7.0f)
            {
                SetState(CreatureState.Alert);
            }
        }

        private void UpdateIdleState()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                SetState(CreatureState.Wandering);
            }
        }

        private void UpdateWanderState()
        {
            Vector3 direction = (_targetDestination - transform.position);
            direction.y = 0f;

            if (direction.magnitude < 0.5f)
            {
                SetState(CreatureState.Idle);
                return;
            }

            // Rotaciona em direção ao destino
            Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6.0f);

            // Move para a frente
            Vector3 move = transform.forward * walkSpeed * Time.deltaTime;
            _cc.Move(move);

            // Animação procedural de pulinho ao andar
            _hopTimer += Time.deltaTime * 7.0f;
            if (modelRoot != null)
            {
                float hopHeight = Mathf.Abs(Mathf.Sin(_hopTimer)) * 0.12f;
                modelRoot.localPosition = new Vector3(0f, hopHeight, 0f);
            }
        }

        private void UpdateAlertState()
        {
            if (_droneRef != null)
            {
                // Vira para olhar o drone com curiosidade
                Vector3 lookDir = (_droneRef.position - transform.position);
                lookDir.y = 0f;
                if (lookDir.magnitude > 0.1f)
                {
                    Quaternion rot = Quaternion.LookRotation(lookDir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 8.0f);
                }

                // Se o drone se afastar, volta ao estado normal
                if (Vector3.Distance(transform.position, _droneRef.position) > alertDistance * 1.4f)
                {
                    SetState(CreatureState.Idle);
                }
            }
        }

        private void PickNewWanderDestination()
        {
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            _targetDestination = _homeOrigin + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        private void ApplyGravity()
        {
            if (!_cc.isGrounded)
            {
                _cc.Move(Vector3.down * 9.8f * Time.deltaTime);
            }
        }

        public void OnGrabbedByClaw(Transform clawParent)
        {
            SetState(CreatureState.Grabbed);
            _cc.enabled = false;
        }

        public void OnReleased()
        {
            if (CurrentState == CreatureState.Rescued) return;

            _cc.enabled = true;
            SetState(CreatureState.Alert);
            if (modelRoot != null) modelRoot.localPosition = Vector3.zero;
        }

        public void OnRescued()
        {
            SetState(CreatureState.Rescued);
            _cc.enabled = false;

            // Efeito de celebração / encolhimento ou teletransporte para o santuário
            StartCoroutine(RescueSequenceRoutine());
        }

        private IEnumerator RescueSequenceRoutine()
        {
            float elapsed = 0f;
            Vector3 initialScale = transform.localScale;

            // Pula de alegria e depois encolhe suavemente ao entrar na base de resgate
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 1.2f;

                if (modelRoot != null)
                {
                    modelRoot.Rotate(Vector3.up, 360f * Time.deltaTime * 2f);
                }

                if (t > 0.5f)
                {
                    float shrink = (1f - t) * 2f;
                    transform.localScale = initialScale * Mathf.Max(shrink, 0.01f);
                }

                yield return null;
            }

            gameObject.SetActive(false);
        }

        private void AnimateGrabbedWiggle()
        {
            if (modelRoot == null) return;

            // Balanço fofinho enquanto está pendurado no ar
            float wiggle = Mathf.Sin(Time.time * 8.0f) * 10.0f;
            modelRoot.localRotation = Quaternion.Euler(0f, 0f, wiggle);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_homeOrigin, wanderRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, alertDistance);
        }
    }
}
