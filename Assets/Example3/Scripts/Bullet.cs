using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class Bullet : MonoBehaviour
    {
        public Shooter Shooter { get; set; }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Shooter.IsServer)
                return;

            if (collision.gameObject.TryGetComponent<Health>(out var health))
            {
                health.Harm(Shooter.Damage);
            }

            GameObject.Destroy(gameObject);
        }
    }
}
