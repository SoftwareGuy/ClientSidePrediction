using System.Runtime.InteropServices;
using Mirage;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamesFrowen.CSP.Example3
{
    public class PlayerLook : PlayerInputBase<PlayerLook.NetworkInput, PlayerLook.NetworkState>
    {
        public const int ORDER = 0;
        public override int Order => ORDER;

        [SerializeField] private float _lookSpeed = 5;
        [SerializeField] private float _maxPitch = 70;
        [SerializeField] private bool _flipY = true;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _gun;
        [SerializeField] private Shooter _shooter;

        [SerializeField] private Transform _cameraProxy;
        [SerializeField] private Vector3 _cameraOffset;

        private float _yaw;
        private float _pitch;

        protected override void Awake()
        {
            base.Awake();

            Cursor.lockState = CursorLockMode.Locked;
        }


#if UNITY_EDITOR
        private void Update()
        {
            // re-lock the mouse if `l` is pressed in editor
            if (Keyboard.current.lKey.isPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
#endif

        public override void InputUpdate()
        {
            base.InputUpdate();

            // use WasPressedThisFrame instead of IsPressed
            // otherwise we see problems if 2 fixed updates run without InputUpdate (2nd fixcdupdate will see no button press because input is reset to default in GetInput)
            var shoot = _playerInput.Shoot.WasPressedThisFrame();
            var look = _playerInput.Look.ReadValue<Vector2>();

            _networkInput.Shoot |= shoot;

            // update look direction then set it
            // 
            UpdateLook(look);
            _networkInput.Pitch = _pitch;
            _networkInput.Yaw = _yaw;

            // todo apply local inputs here
        }

        private void UpdateLook(Vector2 look)
        {
            var change = _lookSpeed * Time.deltaTime;

            _yaw += look.x * change;

            var flip = _flipY ? -1 : 1;
            var newPitch = _pitch + (look.y * flip * change);
            _pitch = Mathf.Clamp(newPitch, -_maxPitch, _maxPitch);
        }

        public void LateUpdate()
        {
            // do this in lateUpdate after everything else has been moved
            // otherwise the parent objects of _cameraProxy might mess up the rotation

            // camera is rotated around head based on _pitch/_yaw
            _cameraProxy.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            _cameraProxy.position = _cameraProxy.parent.position + (_cameraProxy.rotation * _cameraOffset);
        }

        public override void ApplyInputs(NetworkInputs<NetworkInput> inputs)
        {
            // todo only apply here if not local

            _head.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            _gun.rotation = _head.rotation;

            if (ShootPressed(inputs))
            {
                _shooter.Shoot();
            }
        }

        private bool ShootPressed(NetworkInputs<NetworkInput> inputs)
        {
            // was pressed this frame but not last
            return inputs.Current.Shoot && !inputs.Previous.Shoot;
        }

        [NetworkMessage]
        public struct NetworkInput
        {
            public NetworkBool Shoot;

            public float Yaw;
            public float Pitch;
        }

        [NetworkMessage, StructLayout(LayoutKind.Explicit, Size = 44)]
        public struct NetworkState
        {
        }
    }
}
