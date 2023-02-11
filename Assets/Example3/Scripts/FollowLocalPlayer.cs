using Mirage;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class FollowLocalPlayer : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private Transform _holder;
        [SerializeField] private float _lerp = 0.8f;
        [SerializeField] private Vector3 _offset;
        [SerializeField] private Vector3 _offsetRotation;
        private Transform _target;

        private void LateUpdate()
        {
            if (_target == null)
                FindTarget();

            // if still null;
            if (_target == null)
                return;

            FollowTarget(_target);
        }

        private void FindTarget()
        {
            if (!_client.IsConnected)
                return;

            var character = _client.Player?.Identity;
            if (character == null)
                return;

            var targetChild = character.GetComponentInChildren<FollowTarget>();
            if (targetChild != null)
                _target = targetChild.transform;
            else
                _target = character.transform;
        }

        private void FollowTarget(Transform target)
        {
            var offset = target.rotation * _offset;
            _holder.position = Vector3.Lerp(_holder.position, target.position + offset, _lerp);
            _holder.rotation = target.rotation;
            _holder.Rotate(_offsetRotation);
        }
    }
}
