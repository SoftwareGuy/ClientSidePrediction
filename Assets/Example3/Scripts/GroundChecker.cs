using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class GroundChecker : MonoBehaviour
    {
        [Header("Grounded")]
        [SerializeField] private Transform _groundedChecker;
        [SerializeField] private float _groundedDistance = 0.1f;
        [Tooltip("How long after leaving the ground can the player still jump")]
        [SerializeField] private float _groundedStayTime = 0.2f;
        [SerializeField] private LayerMask _layerMask = ~0; // all

        /// <summary>
        /// this should be called every fixedupdate to update timer
        /// </summary>
        /// <returns></returns>
        public bool IsGrounded(ref float timer, float deltaTime)
        {
            if (Physics.Raycast(_groundedChecker.position, Vector3.down, _groundedDistance, _layerMask))
            {
                timer = 0;
            }
            else
            {
                timer += deltaTime;
            }

            return timer <= _groundedStayTime;
        }
    }
}
