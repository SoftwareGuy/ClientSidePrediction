using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class FlashTargetOnHit : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private Renderer _renderer;

        [SerializeField] private float _flashDuration = 2;
        [SerializeField] private Gradient _color;

        private Material _material;
        private float _hitTimer;
        private bool _updateColor;

        private void Awake()
        {
            _health.OnHarm += OnHarm;
        }

        private void OnHarm()
        {
            _hitTimer = 0;
            // start updating color
            _updateColor = true;
        }

        private void LateUpdate()
        {
            _hitTimer += Time.deltaTime;

            if (_updateColor)
            {
                SetColor(_color.Evaluate(_hitTimer / _flashDuration));
            }

            // stop updating color
            // do this check after updating color so it is set to 100%
            if (_hitTimer > _flashDuration)
                _updateColor = false;
        }

        private void SetColor(Color color)
        {
            if (_material == null)
                _material = _renderer.material;

            _material.color = color;
        }
    }
}
