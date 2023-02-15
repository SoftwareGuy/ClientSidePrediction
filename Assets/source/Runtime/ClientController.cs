/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using Mirage.Logging;
using Mirage.Serialization;
using UnityEngine;
using UnityEngine.Assertions;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Controls 1 behaviour on client only
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TState"></typeparam>
    internal unsafe class ClientController<TInput, TState> : IClientController where TState : unmanaged
    {
        private static readonly ILogger logger = LogFactory.GetLogger("JamesFrowen.CSP.ClientController");
        private readonly PredictionBehaviourBase<TInput, TState> behaviour;
        private NullableRingBuffer<TInput> _inputBuffer;

        private bool hasSimulatedLocally;
        private bool hasBeforeResimulateState;
        private TState beforeResimulateState;

        private int lastInputTick;

        public ClientController(PredictionBehaviourBase<TInput, TState> behaviour, int bufferSize)
        {
            this.behaviour = behaviour;

            // these buffers are small 
            // dont worry about authority, just create one for all objects
            if (behaviour.HasInput)
                _inputBuffer = new NullableRingBuffer<TInput>(bufferSize);
        }

        public void BeforeResimulate()
        {
            // we only want to do store before re-simulatuion state if we have simulated any steps locally.
            // otherwise we just want to apply state from server
            if (hasSimulatedLocally && behaviour.EnableResimulationTransition)
            {
                beforeResimulateState = *behaviour._statePtr;
                hasBeforeResimulateState = true;
            }
        }

        public void AfterResimulate()
        {
            if (hasBeforeResimulateState && behaviour.EnableResimulationTransition)
            {
                var next = *behaviour._statePtr;
                *behaviour._statePtr = behaviour.ResimulationTransition(beforeResimulateState, next);
                behaviour.AfterStateChanged();
                if (behaviour is IDebugPredictionAfterImage debug && debug.ShowAfterImage)
                    debug.CreateAfterImage(&next, new Color(0, 0.4f, 1f));

                beforeResimulateState = default;
                hasBeforeResimulateState = false;
            }
        }

        /// <summary>
        /// From tick N to N+1
        /// </summary>
        /// <param name="tick"></param>
        void IClientController.Simulate(int tick)
        {
            if (behaviour.UseInputs())
            {
                var input = _inputBuffer.GetOrDefault(tick);
                var previous = _inputBuffer.GetOrDefault(tick - 1);
                behaviour.ApplyInputs(new NetworkInputs<TInput>(input, previous));
            }
            behaviour.NetworkFixedUpdate();
            hasSimulatedLocally = true;
        }

        public void InputTick(int tick)
        {
            Assert.IsTrue(behaviour.UseInputs());

            if (lastInputTick != 0 && lastInputTick != tick - 1)
                if (logger.WarnEnabled()) logger.LogWarning($"Inputs ticks called out of order. Last:{lastInputTick} tick:{tick}");
            lastInputTick = tick;

            var thisTickInput = behaviour.GetInput();
            _inputBuffer.Set(tick, thisTickInput);

            if (behaviour is IDebugPredictionLocalCopy debug)
                debug.Copy?.NoNetworkApply(_inputBuffer.GetOrDefault(tick));
        }

        void IClientController.WriteInput(NetworkWriter writer, int tick)
        {
            var input = _inputBuffer.GetOrDefault(tick);
            writer.Write(input);
        }
    }
}
