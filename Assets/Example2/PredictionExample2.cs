/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Mirage;
using Mirage.Logging;
using Mirage.Serialization;
using UnityEngine;

namespace JamesFrowen.CSP.Example2
{
    public class PredictionExample2 : PredictionBehaviour<InputState, ObjectState>, IDebugPredictionLocalCopy, IDebugPredictionAfterImage
    {
        public float ResimulateLerp = 0.1f;
        [SerializeField] private float speed = 15;
        private static readonly ILogger logger = LogFactory.GetLogger<PredictionExample2>();

        private Rigidbody body;

        protected void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void ApplyInputs(NetworkInputs<InputState> inputs)
        {
            var current = inputs.Current;

            // normalised so that speed isn't faster if moving diagonal
            var move = new Vector3(x: current.Horizontal, y: 0, z: current.Vertical).normalized;

            var topOfCube = transform.position + Vector3.up * .5f;
            body.AddForceAtPosition(speed * move, topOfCube, ForceMode.Acceleration);
        }

        public override void AfterStateChanged()
        {
            body.position = State.Position;
            body.rotation = State.Rotation;
            body.velocity = State.Velocity;
            body.angularVelocity = State.AngularVelocity;
        }

        public override void AfterTick()
        {
            State.Position = body.position;
            State.Rotation = body.rotation;
            State.Velocity = body.velocity;
            State.AngularVelocity = body.angularVelocity;
        }

        public override ObjectState ResimulationTransition(ObjectState before, ObjectState after)
        {
            var t = ResimulateLerp;
            ObjectState state = default;
            state.Position = Vector3.Lerp(before.Position, after.Position, t);
            state.Rotation = Quaternion.Slerp(before.Rotation, after.Rotation, t);
            state.Velocity = Vector3.Lerp(before.Velocity, after.Velocity, t);
            state.AngularVelocity = Vector3.Lerp(before.AngularVelocity, after.AngularVelocity, t);
            return state;
        }

        public override InputState GetInput()
        {
            return new InputState(
                horizontal: (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                vertical: (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
            );
        }


        #region IDebugPredictionLocalCopy
        private PredictionExample2 _copy;
        IDebugPredictionLocalCopy IDebugPredictionLocalCopy.Copy { get => _copy; set => _copy = (PredictionExample2)value; }

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

        #region IDebugPredictionAfterImage
        [SerializeField] private bool _afterImage;
        private static Transform AfterImageParent;
        void IDebugPredictionAfterImage.CreateAfterImage(object _state, Color color)
        {
            if (!_afterImage) return;
            if (AfterImageParent == null)
                AfterImageParent = new GameObject("AfterImage").transform;

            var state = (ObjectState)_state;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = AfterImageParent;
            var mat = GetComponent<Renderer>().sharedMaterial;
            var renderer = cube.GetComponent<Renderer>();
            renderer.material = Instantiate(mat);
            _ = changeColorOverTime(cube, renderer.material, color);
            cube.transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        private async Task changeColorOverTime(GameObject cube, Material material, Color baseColor)
        {
            var a = baseColor;
            var b = baseColor;
            a.a = 0.4f;
            b.a = 0f;

            var start = Time.time;
            var end = start + 1;
            while (end > Time.time)
            {
                var t = (end - Time.time);
                // starts at t=1, so a is end point
                var color = Color.Lerp(b, a, t * t);
                material.color = color;
                await Task.Yield();
            }

            Destroy(material);
            Destroy(cube);
        }
        #endregion
    }

    [NetworkMessage]
    public struct InputState
    {
        [BitCountFromRange(-1, 1)] public readonly int Horizontal;
        [BitCountFromRange(-1, 1)] public readonly int Vertical;

        public InputState(int horizontal, int vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 52)]
    public struct ObjectState
    {
        [FieldOffset(0)] public Vector3 Position;
        [FieldOffset(12)] public Quaternion Rotation;
        [FieldOffset(28)] public Vector3 Velocity;
        [FieldOffset(40)] public Vector3 AngularVelocity;
    }
}
