using UnityEngine;

namespace DroneClaw
{
    /// <summary>
    /// Câmera em terceira pessoa dinâmica que acompanha o drone de resgate.
    /// Inclui suavização de movimento, enquadramento dinâmico quando a garra é lançada,
    /// e prevenção contra colisão com obstáculos.
    /// </summary>
    public class DroneChaseCamera : MonoBehaviour
    {
        [Header("Alvo")]
        public Transform target;

        [Header("Configuração de Enquadramento")]
        [Tooltip("Distância horizontal atrás do drone")]
        public float defaultDistance = 5.0f;

        [Tooltip("Altura da câmera acima do drone")]
        public float defaultHeight = 2.4f;

        [Tooltip("Suavização de posição")]
        public float positionDamping = 6.0f;

        [Tooltip("Suavização de rotação")]
        public float rotationDamping = 8.0f;

        [Header("Modo Foco na Garra (Descida)")]
        [Tooltip("Distância extra quando a garra estiver descendo para melhor visualização")]
        public float winchExtraDistance = 1.5f;

        [Tooltip("Altura extra quando a garra estiver descendo")]
        public float winchExtraHeight = 1.8f;

        [Header("Evitar Colisão")]
        public LayerMask obstacleLayers;
        public float minCollisionDistance = 1.2f;

        private float _currentDistance;
        private float _currentHeight;
        private bool _isClawDeployed;

        private void Start()
        {
            _currentDistance = defaultDistance;
            _currentHeight = defaultHeight;

            if (target != null)
            {
                transform.position = CalculateTargetPosition(defaultDistance, defaultHeight);
                transform.LookAt(target.position + Vector3.up * 0.5f);
            }
        }

        public void SetClawDeployed(bool deployed)
        {
            _isClawDeployed = deployed;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Ajusta distâncias desejadas dependendo do estado do guincho
            float targetDist = defaultDistance + (_isClawDeployed ? winchExtraDistance : 0f);
            float targetHgt = defaultHeight + (_isClawDeployed ? winchExtraHeight : 0f);

            _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * 3.0f);
            _currentHeight = Mathf.Lerp(_currentHeight, targetHgt, Time.deltaTime * 3.0f);

            Vector3 desiredPosition = CalculateTargetPosition(_currentDistance, _currentHeight);

            // Verificação de raio para evitar que a câmera atravesse árvores ou paredes
            Vector3 targetPivot = target.position + Vector3.up * 1.0f;
            Vector3 dir = desiredPosition - targetPivot;
            if (Physics.Raycast(targetPivot, dir.normalized, out RaycastHit hit, dir.magnitude, obstacleLayers))
            {
                desiredPosition = hit.point - dir.normalized * 0.3f;
            }

            // Suaviza a posição
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionDamping);

            // Olha suavemente para o drone (com foco ligeiramente abaixo quando a garra está descendo)
            Vector3 lookTarget = target.position + Vector3.up * (_isClawDeployed ? -0.5f : 0.5f);
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);
        }

        private Vector3 CalculateTargetPosition(float distance, float height)
        {
            // Posiciona atrás do drone na rotação Y dele
            Vector3 backDirection = -target.forward;
            backDirection.y = 0f;
            backDirection.Normalize();

            return target.position + backDirection * distance + Vector3.up * height;
        }
    }
}
