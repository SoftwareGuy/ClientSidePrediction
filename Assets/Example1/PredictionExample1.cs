/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System.Runtime.InteropServices;
using Mirage;
using Mirage.Logging;
using UnityEngine;

namespace JamesFrowen.CSP.Example1
{
    public class PredictionExample1 : PredictionBehaviour<InputState, ObjectState>, IDebugPredictionLocalCopy
    {
        private static readonly ILogger logger = LogFactory.GetLogger<PredictionExample1>();

        private Rigidbody body;
        private const float speed = 15;

        protected void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void ApplyInputs(NetworkInputs<InputState> inputs)
        {
            var previous = inputs.Previous;
            var current = inputs.Current;

            var move = current.Horizontal * new Vector3(1, .25f /*small up force so it can move along floor*/, 0);
            body.AddForce(speed * move, ForceMode.Acceleration);
            if (current.jump && !previous.jump)
            {
                body.AddForce(Vector3.up * 10, ForceMode.Impulse);
            }
        }

        public override void NetworkFixedUpdate()
        {
            // stronger gravity when moving down
            float gravity = body.velocity.y < 0 ? 3 : 1;
            body.AddForce(gravity * Physics.gravity, ForceMode.Acceleration);
            body.velocity += (gravity * Physics.gravity) * PredictionTime.FixedDeltaTime;

            body.rotation = Quaternion.identity;
            body.angularVelocity = Vector3.zero;
        }

        public override void AfterTick()
        {
            State.Position = body.position;
            State.Velocity = body.velocity;
        }

        public override void AfterStateChanged()
        {
            body.position = State.Position;
            body.velocity = State.Velocity;
        }


        public override InputState GetInput()
        {
            return new InputState(
                right: Input.GetKey(KeyCode.D),
                left: Input.GetKey(KeyCode.A),
                jump: Input.GetKey(KeyCode.Space)
            );
        }

        #region IDebugPredictionLocalCopy
        private PredictionExample1 _copy;
        IDebugPredictionLocalCopy IDebugPredictionLocalCopy.Copy { get => _copy; set => _copy = (PredictionExample1)value; }

        void IDebugPredictionLocalCopy.Setup(IPredictionTime time)
        {
            PredictionTime = time;
        }

        private InputState noNetworkPrevious;
        void IDebugPredictionLocalCopy.NoNetworkApply(object _input)
        {
            var input = (InputState)_input;
            ApplyInputs(new NetworkInputs<InputState>(input, noNetworkPrevious));
            NetworkFixedUpdate();
            gameObject.scene.GetPhysicsScene().Simulate(PredictionTime.FixedDeltaTime);
            noNetworkPrevious = input;
        }
        #endregion
    }

    [NetworkMessage]
    public struct InputState
    {
        public readonly bool jump;
        public readonly bool left;
        public readonly bool right;

        public InputState(bool right, bool left, bool jump)
        {
            this.jump = jump;
            this.left = left;
            this.right = right;
        }

        public int Horizontal => (right ? 1 : 0) - (left ? 1 : 0);
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct ObjectState
    {
        [FieldOffset(0)] public Vector3 Position;
        [FieldOffset(12)] public Vector3 Velocity;
    }
}
