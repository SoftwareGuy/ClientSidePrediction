/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using Mirage;
using Mirage.Logging;
using Mirage.Serialization;
using UnityEngine;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Controls 1 behaviour on server and host
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TState"></typeparam>
    internal class ServerController<TInput, TState> : IServerController where TState : unmanaged
    {
        private static readonly ILogger logger = LogFactory.GetLogger("JamesFrowen.CSP.ServerController");
        private readonly PredictionBehaviourBase<TInput, TState> behaviour;
        private readonly int _bufferSize;
        private readonly ServerManager _manager;
        private NullableRingBuffer<TInput> _inputBuffer;
        private (int tick, TInput input) lastValidInput;
        private int? lastReceived = null;
        private bool hostMode;
        void IServerController.SetHostMode()
        {
            hostMode = true;
        }

        public ServerController(ServerManager manager, PredictionBehaviourBase<TInput, TState> behaviour, int bufferSize)
        {
            _manager = manager;
            this.behaviour = behaviour;
            _bufferSize = bufferSize;

            if (behaviour.UseInputs())
                _inputBuffer = new NullableRingBuffer<TInput>(bufferSize);
            else // listen just incase auth is given late
                behaviour.Identity.OnOwnerChanged.AddListener(OnOwnerChanged);
        }

        private void OnOwnerChanged(INetworkPlayer newOwner)
        {
            // create buffer and remove listener
            _inputBuffer = new NullableRingBuffer<TInput>(_bufferSize);
            behaviour.Identity.OnOwnerChanged.RemoveListener(OnOwnerChanged);
        }

        void IServerController.ReadInput(ServerManager.PlayerTimeTracker tracker, NetworkReader reader, int inputTick)
        {
            var input = reader.Read<TInput>();
            // if new, and after last sim
            if (inputTick > tracker.lastReceivedInput && inputTick > _manager._lastSim)
            {
                lastReceived = tracker.lastReceivedInput;
                _inputBuffer.Set(inputTick, input);
            }
        }

        void IServerController.Tick(int tick)
        {
            var hasInputs = behaviour.UseInputs();
            if (hasInputs)
            {
                // hostmode + host client has HasAuthority
                if (hostMode && behaviour.HasAuthority)
                {
                    var thisTickInput = behaviour.GetInput();
                    _inputBuffer.Set(tick, thisTickInput);
                }

                getValidInputs(tick, out var input, out var previous);
                behaviour.ApplyInputs(new NetworkInputs<TInput>(input, previous));
            }

            behaviour.NetworkFixedUpdate();

            if (hasInputs)
                _inputBuffer.Clear(tick - 1);
        }

        private void getValidInputs(int tick, out TInput input, out TInput previous)
        {
            input = default;
            previous = default;
            // dont need to do anything till first is received
            // skip check hostmode, there are always inputs for hostmode
            if (!hostMode && (lastReceived == null))
                return;

            getValidInput(tick, out var currentValid, out input);
            getValidInput(tick - 1, out var _, out previous);
            if (currentValid)
            {
                lastValidInput = (tick, input);
            }
        }

        private void getValidInput(int tick, out bool valid, out TInput input)
        {
            valid = _inputBuffer.TryGet(tick, out input);
            if (!valid)
            {
                if (logger.LogEnabled()) logger.Log($"No inputs for {tick}");
                input = behaviour.MissingInput(lastValidInput.input, lastValidInput.tick, tick);
            }
        }

        void IServerController.ReceiveHostInput<TInput2>(int tick, TInput2 _input)
        {
            // todo check Alloc from boxing
            if (_input is TInput input)
            {
                _inputBuffer.Set(tick, input);
            }
            else
            {
                throw new InvalidOperationException("Input type didn't match");
            }
        }
    }
}
