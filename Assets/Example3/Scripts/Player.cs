using System.Runtime.InteropServices;
using Mirage;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class Player : PredictionBehaviour<Player.NetworkInput, Player.NetworkState>
    {
        [SerializeField] private float _speed;
        [SerializeField] private float _jumpForce;
        [Tooltip("How many jumps can a player make in a row")]
        [SerializeField] private int _jumpCount;



        public override void InputUpdate()
        {
            base.InputUpdate();

            //var mouse = Input.GetAxis();

        }
        public override NetworkInput GetInput()
        {
            throw new System.NotImplementedException();
        }
        public override void ApplyInputs(NetworkInputs<NetworkInput> inputs)
        {
            throw new System.NotImplementedException();
        }

        public override void NetworkFixedUpdate()
        {
            base.NetworkFixedUpdate();
        }



        [NetworkMessage]
        public struct NetworkInput
        {
            public int Buttons;
            public float Yaw;
            public float Pitch;

        }
        [NetworkMessage, StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct NetworkState
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public Quaternion Rotation;
            [FieldOffset(28)] public int CurrentJumpCount;
        }
    }
}
