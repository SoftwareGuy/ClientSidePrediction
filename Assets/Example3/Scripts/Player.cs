using System.Runtime.InteropServices;
using JamesFrowen.CSP.Example3.Inputs;
using Mirage;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamesFrowen.CSP.Example3
{
    public class Player : PredictionBehaviour<Player.NetworkInput, Player.NetworkState>
    {
        [SerializeField] private float _moveSpeed = 5;
        [SerializeField] private float _velocitiyLerp = 0.8f;
        [SerializeField] private float _lookSpeed = 5;
        [SerializeField] private float _jumpInpluse = 10;

        [SerializeField] private Transform _head;
        [SerializeField] private Rigidbody _body;

        [Header("Grounded")]
        [SerializeField] private Transform _groundedChecker;
        [SerializeField] private float _groundedDistance = 0.1f;
        [Tooltip("How long after leaving the ground can the player still jump")]
        [SerializeField] private float _groundedStayTime = 0.2f;

        private InputActions _inputActions;
        private InputActions.PlayerActions _playerInput;

        private NetworkInput _networkInput;
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _inputActions = new InputActions();
            _inputActions.Player.Enable();
            _playerInput = _inputActions.Player;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDestroy()
        {
            _inputActions.Dispose();
            _inputActions = null;
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

            var jump = _playerInput.Jump.IsPressed();
            var shoot = _playerInput.Shoot.IsPressed();
            var look = _playerInput.Look.ReadValue<Vector2>();
            var move = _playerInput.Move.ReadValue<Vector2>();

            // **Accumulate Inputs**

            // if button was pressed this or previous frames
            _networkInput.Jump |= jump;
            _networkInput.Shoot |= shoot;

            // just use move recent move buttons
            _networkInput.Move = move;

            // update look direction then set it
            // 
            UpdateLook(look);
            _networkInput.Yaw = _yaw;
            _networkInput.Pitch = _pitch;

            // todo apply local inputs here
        }

        private void UpdateLook(Vector2 look)
        {
            var change = _lookSpeed * Time.deltaTime;

            _yaw += look.x * change;
            _pitch += look.y * change;
        }

        public override NetworkInput GetInput()
        {
            var temp = _networkInput;

            // clear Accumulated inputs;
            _networkInput = default;

            return temp;
        }
        public override void ApplyInputs(NetworkInputs<NetworkInput> inputs)
        {
            // todo only apply here if not local

            var current = inputs.Current;
            _body.rotation = Quaternion.Euler(0, current.Yaw, 0);
            _head.rotation = Quaternion.Euler(current.Pitch, 0, 0);

            _body.velocity = LerpXZVelocity(_body.velocity, current.Move * _moveSpeed);
            if (JumpPressed(inputs) && IsGrounded())
            {
                _body.velocity += Vector3.up * _jumpInpluse;
            }
        }

        private bool JumpPressed(NetworkInputs<NetworkInput> inputs)
        {
            // was pressed this frame but not last
            return inputs.Current.Jump && !inputs.Previous.Jump;
        }

        private Vector3 LerpXZVelocity(Vector3 velocity, Vector2 change)
        {
            var lerp = Vector2.Lerp(velocity.ToXZ(), change, _velocitiyLerp);
            var result = lerp.FromXZ();
            // keep old y speed
            result.y = velocity.y;
            return result;
        }

        private bool IsGrounded()
        {
            if (Physics.Raycast(_groundedChecker.position, Vector3.down, _groundedDistance))
            {
                State.GroundedTimer = 0;
            }
            else
            {
                State.GroundedTimer += PredictionTime.FixedDeltaTime;
            }

            return State.GroundedTimer <= _groundedStayTime;
        }

        public override void AfterStateChanged()
        {
            _body.position = State.Position;
            _body.velocity = State.Velocity;
        }
        public override void AfterTick()
        {
            State.Position = _body.position;
            State.Velocity = _body.velocity;
        }

        [NetworkMessage]
        public struct NetworkInput
        {
            public NetworkBool Jump;
            public NetworkBool Shoot;
            public Vector2 Move;

            public float Yaw;
            public float Pitch;
        }

        [NetworkMessage, StructLayout(LayoutKind.Explicit, Size = 44)]
        public struct NetworkState
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public Vector3 Velocity;
            [FieldOffset(40)] public float GroundedTimer;
        }
    }
    public static class Vector3Extensions
    {
        public static Vector2 ToXZ(this Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 FromXZ(this Vector2 v) => new Vector3(v.x, 0, v.y);
    }
}
