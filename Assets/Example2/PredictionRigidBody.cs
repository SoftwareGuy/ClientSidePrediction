/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using Mirage.Logging;
using UnityEngine;

namespace JamesFrowen.CSP.Example2
{
    public class PredictionRigidBody : PredictionBehaviour<ObjectState>
    {
        private static readonly ILogger logger = LogFactory.GetLogger<PredictionExample2>();

        public float ResimulateLerp = 0.1f;
        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void AfterStateChanged()
        {
            body.position = State.Position;
            body.rotation = State.Rotation;
            body.velocity = State.Velocity;
            body.angularVelocity = State.AngularVelocity;
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

        public override void AfterTick()
        {
            State.Position = body.position;
            State.Rotation = body.rotation;
            State.Velocity = body.velocity;
            State.AngularVelocity = body.angularVelocity;
        }
    }
}
