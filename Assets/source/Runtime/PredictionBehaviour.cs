/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using JamesFrowen.DeltaSnapshot;
using Mirage;
using Mirage.Events;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Base class for Client side prediction for objects without input, like physics objects in a scene.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public abstract class PredictionBehaviour<TState> : PredictionBehaviourBase<NoValues, TState> where TState : unmanaged
    {
        public sealed override bool HasInput => false;
        public sealed override NoValues GetInput() => throw new NotSupportedException();
        public sealed override NoValues MissingInput(NoValues previous, int previousTick, int currentTick) => throw new NotSupportedException();
        public sealed override void ApplyInputs(NetworkInputs<NoValues> inputs) => throw new NotSupportedException();
    }

    /// <summary>
    /// Base class for Client side prediction for objects with input, like player objects with movement.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public abstract class PredictionBehaviour<TInput, TState> : PredictionBehaviourBase<TInput, TState> where TState : unmanaged
    {
        public sealed override bool HasInput => true;
    }

    public abstract unsafe class PredictionBehaviourBase<TInput, TState> : SnapshotBehaviour<TState>, IPredictionBehaviour where TState : unmanaged
    {
        public virtual int Order => 0;

        private ClientController<TInput, TState> _clientController;
        private ServerController<TInput, TState> _serverController;
        private readonly AddLateEvent _onPredictionSetup = new AddLateEvent();

        IClientController IPredictionBehaviour.ClientController => _clientController;
        IServerController IPredictionBehaviour.ServerController => _serverController;


        /// <summary>
        /// Invoked at the end of IPredictionBehaviour setup methods.
        /// <para>
        /// <see cref="PredictionTime"/> and other properties will be set and ready to use when this event is called.
        /// </para>
        /// </summary>
        public IAddLateEvent OnPredictionSetup => _onPredictionSetup;

        /// <summary>
        /// Is this object on a client that does not have authority (excluding host)
        /// <para>This can be used to check if state should be update or interpolated instead</para>
        /// </summary>
        public bool IsRemoteClient => !IsServer && IsClient && !HasAuthority;

        public IPredictionTime PredictionTime { get; set; }

        /// <summary>
        /// Used to disable input for this object
        /// <para>This should be false for non player objects</para>
        /// </summary>
        public abstract bool HasInput { get; }
        /// <summary>
        /// Called on Client to get inputs
        /// </summary>
        /// <returns></returns>
        public abstract TInput GetInput();

        /// <summary>
        /// Called on Server if inputs are missing
        /// </summary>
        /// <param name="previous">the previous valid input</param>
        /// <param name="previousTick">what tick the previous valid was</param>
        /// <param name="currentTick">the current missing tick</param>
        /// <returns></returns>
        public virtual TInput MissingInput(TInput previous, int previousTick, int currentTick)
        {
            // default is just to return previous input.
            // chances are that the player is pressing the same keys as they were last frame

            // for example they press space for jump, that will be true for multiple frames
            // this should be used without ApplyInputs to check if jump key pressed this tick but not previous

            return previous;
        }

        /// <summary>
        /// Called on Server and on clients with authority
        /// <para>Called before <see cref="NetworkFixedUpdate"/></para>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="previous"></param>
        public abstract void ApplyInputs(NetworkInputs<TInput> inputs);

        /// <summary>
        /// Called before all network updates for the frame
        /// <para>Should be used to poll inputs from unity and store for <see cref="GetInput"/></para>
        /// <para>Only called on clients</para>
        /// </summary>
        /// <param name="fixedDelta"></param>
        public virtual void InputUpdate() { }

        /// <summary>
        /// Modify the objects state. Called on all objects, use <see cref="ApplyInputs(TInput, TInput)"/> for effects on owned objects
        /// <para>Applies any physics/state logic to object here</para>
        /// <para>For example any custom gravity, drag, etc</para>
        /// <para>Called once per tick on server and client, and for each resimulation step on client</para>
        /// </summary>
        /// <param name="fixedDelta"></param>
        public virtual void NetworkFixedUpdate() { }

        /// <summary>
        /// Called after all network updates for the frame
        /// <para>Use state here to update renderering, animation, or other visual effects</para>
        /// <para>Only called on clients</para>
        /// </summary>
        /// <param name="fixedDelta"></param>
        public virtual void VisualUpdate() { }

        /// <summary>
        /// Called after the state values are updated
        /// <para>Use to set other (physics state) properties from the state value of this behaviour. For example setting transform position and rotation</para>
        /// <para>Called after ResimulationTransition</para>
        /// </summary>
        public virtual void AfterStateChanged() { }

        /// <summary>
        /// Called after FixedUpdate and Physics.sim
        /// <para>use to update state from any non-network state, like using transform or rigidbody to set state.position</para>
        /// </summary>
        public virtual void AfterTick() { }

        /// <summary>
        /// Used to disable ResimulationTransition
        /// <para>ResimulationTransition requires the state to be gathered before and after resimulation. set this property to false to avoid that</para>
        /// </summary>
        public virtual bool EnableResimulationTransition => true;

        /// <summary>
        /// Used to smooth movement on client after Resimulation
        /// <para>Call <see cref="ApplyState"/> using to set new position or Leave empty function for no smoothing</para>
        /// </summary>
        /// <param name="before">state before resimulation</param>
        /// <param name="after">state after resimulation</param>
        public virtual TState ResimulationTransition(TState before, TState after)
        {
            return after;
            // by default nothing
            // after state will already be applied nothing needs to happen

            // you can override this function to apply moving between state before-re-simulatution and after.
        }

        void IPredictionBehaviour.ServerSetup(int bufferSize)
        {
            _serverController = new ServerController<TInput, TState>(this, bufferSize);

            _onPredictionSetup.Invoke();
        }
        void IPredictionBehaviour.ClientSetup(int bufferSize, ClientInterpolation clientInterpolation)
        {
            _clientController = new ClientController<TInput, TState>(this, bufferSize);
            ClientInterpolation = clientInterpolation;

            _onPredictionSetup.Invoke();
        }

        void IPredictionBehaviour.CleanUp()
        {
            PredictionTime = null;
            _serverController = null;
            _clientController = null;
            ClientInterpolation = null;

            _onPredictionSetup.Reset();
        }
    }

    internal static class PredictionBehaviourExtensions
    {
        /// <summary>
        /// Does the objects have inputs, and have control (eg server or authority)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        public static bool UseInputs(this IPredictionBehaviour behaviour)
        {
            var nb = (NetworkBehaviour)behaviour;

            // if no inputs implemented, then just return early
            if (!behaviour.HasInput)
                return false;

            // is server and object has an owner
            // note: this mean un-owned objects can't be controlled by anyone expect the server
            if (nb.IsServer)
            {
                return nb.Owner != null;
            }
            // is client and has authority over the object, like the player object
            else if (nb.IsClient)
            {
                return nb.HasAuthority;
            }

            return false;
        }

        /// <summary>
        /// Does the objects have inputs, and have control (eg server or authority)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        public static bool UseInputs<T>(this T behaviour) where T : NetworkBehaviour, IPredictionBehaviour
        {
            // if no inputs implemented, then just return early
            if (!behaviour.HasInput)
                return false;

            // is server and object has an owner
            // note: this mean un-owned objects can't be controlled by anyone expect the server
            if (behaviour.IsServer)
            {
                return behaviour.Owner != null;
            }
            // is client and has authority over the object, like the player object
            else if (behaviour.IsClient)
            {
                return behaviour.HasAuthority;
            }

            return false;
        }
    }
}
