using Mirage;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class Shooter : NetworkBehaviour
    {
        [SerializeField] private GameObject _bullet;
        [SerializeField] private float _damage = 1;

        [SerializeField] private Vector3 _force = Vector3.forward * 5;
        [SerializeField] private Transform _origin;

        public float Damage => _damage;

        public void Shoot()
        {
            var clone = Instantiate(_bullet);
            var body = clone.GetComponent<Rigidbody>();
            ApplyForce(body);

            var bullet = clone.GetComponent<Bullet>();
            bullet.Shooter = this;
        }

        private void ApplyForce(Rigidbody body)
        {
            var rotation = _origin.rotation;
            body.position = _origin.position;
            body.rotation = rotation;
            body.velocity = rotation * _force;
        }
    }
}
