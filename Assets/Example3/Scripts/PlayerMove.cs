using System.Runtime.InteropServices;
using Mirage;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class PlayerMove : PlayerInputBase<PlayerMove.NetworkInput, PlayerMove.NetworkState>
    {
        // after Look
        public const int ORDER = PlayerLook.ORDER + 1;
        public override int Order => ORDER;


        [SerializeField] private float _moveSpeed = 5;
        [SerializeField] private float _velocitiyLerp = 0.8f;

        [SerializeField] private float _jumpInpluse = 10;

        [SerializeField] private Transform _head;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private GroundChecker _groundChecker;


        public override void InputUpdate()
        {
            base.InputUpdate();

            var jump = _playerInput.Jump.WasPressedThisFrame();
            var move = _playerInput.Move.ReadValue<Vector2>();

            // **Accumulate Inputs**

            // if button was pressed this or previous frames
            _networkInput.Jump |= jump;

            // just use move recent move buttons
            _networkInput.Move = move;

            // todo apply local inputs here
        }

        public override void ApplyInputs(NetworkInputs<NetworkInput> inputs)
        {
            // todo only apply here if not local

            var current = inputs.Current;

            var headRotation = _head.rotation;
            _body.rotation = Quaternion.Euler(0, headRotation.eulerAngles.y, 0);
            // set head rotation back to same value, this will cancel out the body rotation because head is a child transform
            _head.rotation = headRotation;

            _body.velocity = LerpXZVelocity(_body.velocity, current.Move, _moveSpeed, _body.rotation);
            // make sure to check grounded each tick so that the timer is updated
            var grounded = _groundChecker.IsGrounded(ref State.GroundedTimer, PredictionTime.FixedDeltaTime);
            if (grounded && JumpPressed(inputs))
            {
                _body.velocity += Vector3.up * _jumpInpluse;
            }
        }

        private bool JumpPressed(NetworkInputs<NetworkInput> inputs)
        {
            // was pressed this frame but not last
            return inputs.Current.Jump && !inputs.Previous.Jump;
        }

        private Vector3 LerpXZVelocity(Vector3 velocity, Vector2 input, float speed, Quaternion rotation)
        {
            var moveDirection = rotation * input.FromXZ();
            var targetVelocity = moveDirection * speed;

            var result = Vector3.Lerp(velocity, targetVelocity, _velocitiyLerp);
            // keep old y speed
            result.y = velocity.y;
            return result;
        }



        private void OnGUI()
        {
            if (!HasState)
                return;

            GUILayout.Space(100);
            GUILayout.Label($"Grounded Timer: {State.GroundedTimer}");
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
            public Vector2 Move;
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
