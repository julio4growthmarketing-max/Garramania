using UnityEngine;

namespace DroneClaw
{
    /// <summary>
    /// Controlador de voo físico e responsivo para o Drone de resgate.
    /// Inclui movimentação 3D, controle de altitude, inclinação dinâmica (tilt/banking),
    /// rotação de hélices e estabilização automática (hover).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFlightController : MonoBehaviour
    {
        [Header("Velocidade e Força")]
        [Tooltip("Velocidade máxima de deslocamento horizontal (X/Z)")]
        public float moveSpeed = 9.0f;

        [Tooltip("Aceleração do movimento horizontal")]
        public float acceleration = 12.0f;

        [Tooltip("Velocidade de subida e descida vertical (Y)")]
        public float verticalSpeed = 6.0f;

        [Tooltip("Velocidade de rotação de curva (Yaw) em graus/segundo")]
        public float yawSpeed = 120.0f;

        [Header("Limites de Voo")]
        public float minAltitude = 1.0f;
        public float maxAltitude = 25.0f;

        [Header("Física de Inclinação (Banking & Tilt)")]
        [Tooltip("Objeto visual que inclina (geralmente o modelo 3D filho do drone)")]
        public Transform droneModelMesh;

        [Tooltip("Ângulo máximo de inclinação ao acelerar (graus)")]
        public float maxTiltAngle = 22.0f;

        [Tooltip("Suavização da inclinação")]
        public float tiltSmoothTime = 0.15f;

        [Header("Hélices")]
        [Tooltip("Transforms das hélices para girarem durante o voo")]
        public Transform[] propellers;
        public float propellerIdleRpm = 1800.0f;
        public float propellerBoostRpm = 3600.0f;

        [Header("Áudio do Motor (Opcional)")]
        public AudioSource engineAudio;
        public float minPitch = 0.8f;
        public float maxPitch = 1.35f;

        // Estado interno
        private Rigidbody _rb;
        private Vector3 _currentVelocity;
        private float _targetAltitude;
        private Vector2 _inputMove;
        private float _inputAltitude;
        private float _inputYaw;
        private Vector3 _tiltVelocity;

        public bool IsGrounded { get; private set; }
        public float CurrentThrottle01 { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false; // Voo controlado por propulsão
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            _targetAltitude = transform.position.y;
            if (_targetAltitude < minAltitude) _targetAltitude = minAltitude + 2f;
        }

        private void Update()
        {
            ReadInput();
            AnimatePropellers();
            UpdateEngineAudio();
        }

        private void FixedUpdate()
        {
            HandleFlightMovement();
            HandleRotation();
            HandleVisualTilt();
        }

        private void ReadInput()
        {
            // Movimentação horizontal padrão (WASD ou Setas)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _inputMove = new Vector2(h, v).normalized;

            // Subida e descida (Espaço para subir, C ou Shift Esquerdo para descer)
            _inputAltitude = 0f;
            if (Input.GetKey(KeyCode.Space)) _inputAltitude += 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.C)) _inputAltitude -= 1f;

            // Rotação de cauda / Yaw (Q para virar à esquerda, E para virar à direita)
            _inputYaw = 0f;
            if (Input.GetKey(KeyCode.Q)) _inputYaw -= 1f;
            if (Input.GetKey(KeyCode.E)) _inputYaw += 1f;

            // Nível de aceleração (0 a 1) para efeitos visuais/sonoros
            float moveMagnitude = _inputMove.magnitude;
            float altMagnitude = Mathf.Abs(_inputAltitude);
            CurrentThrottle01 = Mathf.Clamp01(Mathf.Max(moveMagnitude, altMagnitude, 0.25f));
        }

        private void HandleFlightMovement()
        {
            // Direção de movimento relativa à orientação do drone
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 targetHorizontalVelocity = (forward * _inputMove.y + right * _inputMove.x) * moveSpeed;

            // Controle suave da altitude
            _targetAltitude += _inputAltitude * verticalSpeed * Time.fixedDeltaTime;
            _targetAltitude = Mathf.Clamp(_targetAltitude, minAltitude, maxAltitude);

            // Força vertical para alcançar e manter a altitude alvo (Hovering)
            float altitudeDiff = _targetAltitude - transform.position.y;
            float verticalVelocity = Mathf.Clamp(altitudeDiff * 4.0f, -verticalSpeed, verticalSpeed);

            Vector3 desiredVelocity = new Vector3(targetHorizontalVelocity.x, verticalVelocity, targetHorizontalVelocity.z);

            // Aplica aceleração suave via Rigidbody
            _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);
        }

        private void HandleRotation()
        {
            if (Mathf.Abs(_inputYaw) > 0.01f)
            {
                float turnAngle = _inputYaw * yawSpeed * Time.fixedDeltaTime;
                Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);
                _rb.MoveRotation(_rb.rotation * turnRotation);
            }
        }

        private void HandleVisualTilt()
        {
            if (droneModelMesh == null) return;

            // Calcula inclinação baseada na velocidade local do drone
            Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);

            // Pitch: frente inclina para baixo (X positivo/negativo)
            float targetPitch = Mathf.Clamp(localVel.z * (maxTiltAngle / moveSpeed), -maxTiltAngle, maxTiltAngle);
            // Roll: inclinação lateral (Z positivo/negativo)
            float targetRoll = Mathf.Clamp(-localVel.x * (maxTiltAngle / moveSpeed), -maxTiltAngle, maxTiltAngle);

            Quaternion targetLocalRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
            droneModelMesh.localRotation = Quaternion.Slerp(droneModelMesh.localRotation, targetLocalRot, Time.fixedDeltaTime / tiltSmoothTime);
        }

        private void AnimatePropellers()
        {
            if (propellers == null || propellers.Length == 0) return;

            float speed = Mathf.Lerp(propellerIdleRpm, propellerBoostRpm, CurrentThrottle01);
            float rotationAmount = speed * 360f / 60f * Time.deltaTime;

            for (int i = 0; i < propellers.Length; i++)
            {
                if (propellers[i] != null)
                {
                    // Alterna sentido horário e anti-horário como drones quadricópteros reais
                    float dir = (i % 2 == 0) ? 1f : -1f;
                    propellers[i].Rotate(Vector3.up, rotationAmount * dir, Space.Self);
                }
            }
        }

        private void UpdateEngineAudio()
        {
            if (engineAudio == null) return;

            if (!engineAudio.isPlaying)
            {
                engineAudio.loop = true;
                engineAudio.Play();
            }

            engineAudio.pitch = Mathf.Lerp(minPitch, maxPitch, CurrentThrottle01);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, _targetAltitude, transform.position.z));
        }
    }
}
