/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JamesFrowen.CSP.Alloc;
using JamesFrowen.CSP.Debugging;
using JamesFrowen.DeltaSnapshot;
using Mirage;
using Mirage.Logging;
using Mirage.Serialization;
using UnityEngine;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Controls all objects on server
    /// </summary>
    internal sealed class ServerManager
    {
        private const int MESSAGE_HEADER = 26; // rough guess
        private const int MAX_NOTIFY_SIZE = 1219 - MESSAGE_HEADER;

        private static readonly ILogger logger = LogFactory.GetLogger("JamesFrowen.CSP.ServerManager");
        private static readonly ILogger verbose = LogFactory.GetLogger("JamesFrowen.CSP.ServerManager_Verbose", LogType.Exception);
        private static StringBuilder _debugBuilder = new StringBuilder();

        // keep track of player list ourselves, so that we can use the SendToMany that does not allocate
        private readonly List<INetworkPlayer> _players;
        private readonly Dictionary<INetworkPlayer, PlayerTimeTracker> _playerTracker = new Dictionary<INetworkPlayer, PlayerTimeTracker>();
        private readonly NetworkWorld _world;
        private readonly TickRunner _tickRunner;
        private readonly PredictionTime _time;
        private readonly IPredictionSimulation _simulation;
        private readonly PredictionCollection _behaviours;
        private bool _hostMode;
        private readonly int _bufferSize;
        private readonly IAllocator _allocator;
        private readonly WorldSnapshot _worldSnapshot;

        internal int _lastSim;

        //delta snapshot
        private LogValueTracker _deltaSizeTracker = new LogValueTracker();
        private RingBuffer<WorldStateCopy> _worldStateCopy;
        private NetworkWriter _payloadWriter;
        private DeltaSnapshotWriter _deltaSnapshot;

        // todo expose this value for debugging
        private int _dumpToFileCount = 0;


        public PlayerTimeTracker Debug_FirstPlayertracker => _playerTracker.Values.FirstOrDefault();
        public PredictionCollection Behaviours => _behaviours;

        internal void SetHostMode()
        {
            _hostMode = true;
            foreach (var behaviour in _behaviours.GetBehaviours())
            {
                behaviour.ServerController.SetHostMode();
            }
        }

        public ServerManager(
            IPredictionSimulation simulation,
            TickRunner tickRunner,
            PredictionTime time,
            NetworkWorld world,
            IAllocator allocator,
            IMessageReceiver messageReceiver,
            int bufferSize = PredictionManager.DEFAULT_BUFFER_SIZE
            )
        {
            _bufferSize = bufferSize;
            _allocator = allocator;
            _worldSnapshot = new WorldSnapshot(_allocator, bufferSize);
            _players = new List<INetworkPlayer>();
            _tickRunner = tickRunner;
            _time = time;
            _behaviours = new PredictionCollection(_time);
            _simulation = simulation;
            tickRunner.OnTick += Tick;

            _world = world;
            _world.onSpawn += OnSpawn;
            _world.onUnspawn += OnUnspawn;

            // add existing items
            foreach (var item in world.SpawnedIdentities)
            {
                OnSpawn(item);
            }

            _worldStateCopy = new RingBuffer<WorldStateCopy>(bufferSize);
            _payloadWriter = new NetworkWriter(1300, true);
            for (var i = 0; i < bufferSize; i++)
            {
                _worldStateCopy.Set(i, new WorldStateCopy());
            }
            _deltaSnapshot = new DeltaSnapshotWriter(_allocator);

            if (_dumpToFileCount > 0)
                WorldStateDump.ClearFolder();

            messageReceiver.RegisterHandler<InputState>(HandleInput);
            messageReceiver.RegisterHandler<DeltaWorldStateFragmentedAck>(HandleFragmentedAck);
        }

        public void AddPlayer(INetworkPlayer player)
        {
            _players.Add(player);
            _playerTracker.Add(player, new PlayerTimeTracker());
        }
        public void RemovePlayer(INetworkPlayer player)
        {
            _players.Remove(player);
            _playerTracker.Remove(player);
        }

        private void OnSpawn(NetworkIdentity identity)
        {
            if (logger.LogEnabled()) logger.Log($"OnSpawn for netId={identity.NetId} name={identity.name}");

            _behaviours.Add(identity, out var _, out var foundBehaviours);

            var snapshots = GetBehaviourCache<ISnapshotBehaviour>.GetBehaviours(identity);
            if (snapshots.Count == 0)
                return;

            // allocate here so that state can be used befor first tick
            // in first tick this state will be copied to the GroupSnapshot for that tick
            var snap = _worldSnapshot.CreateAndAdd(identity, snapshots.ToArray(), _allocator);
            snap.SetActivePtr(_time.Tick);

            foreach (var behaviour in foundBehaviours)
            {
                if (logger.LogEnabled()) logger.Log($"Found PredictionBehaviour for {identity.NetId} {behaviour.GetType().Name}");

                behaviour.ServerSetup(_bufferSize);
                if (_hostMode)
                    behaviour.ServerController.SetHostMode();
            }
        }

        private void OnUnspawn(NetworkIdentity identity)
        {
            _behaviours.Remove(identity, out var _, out var _);
            _worldSnapshot.Remove(identity, false);
        }

        public void Tick(int tick)
        {
            if (verbose.LogEnabled()) verbose.Log($"Server tick {tick}");

            _time.Tick = tick;
            _time.Method = UpdateMethod.NetworkFixed;

            _worldSnapshot.CopyFromPreviousTick(tick);
            Simulate(tick);
            _lastSim = tick;
            SendState(tick);

            _time.Method = UpdateMethod.None;
        }

        public void Simulate(int tick)
        {
            var updates = _behaviours.GetUpdates();
            var updateCount = updates.Count;
            for (var i = 0; i < updateCount; i++)
            {
                var update = updates[i];
                // if behaviour run full tick stuff, otherwise just call fixedupdate
                if (update is IPredictionBehaviour behaviour)
                    behaviour.ServerController.Tick(tick);
                else
                    update.NetworkFixedUpdate();
            }

            _simulation.Simulate(_time.FixedDeltaTime);

            var behaviours = _behaviours.GetBehaviours();
            var behaviourCount = behaviours.Count;
            for (var i = 0; i < behaviourCount; i++)
                behaviours[i].AfterTick();
        }

        private void SendState(int tick)
        {
            CopyStateForTick(tick);

            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                var tracker = _playerTracker[player];

                var msg = new DeltaWorldState()
                {
                    Tick = tick,
                    TimeScale = Time.timeScale == 1 ? default(float?) : Time.timeScale,
                    // set client time for each client, 
                    ClientTime = tracker.LastReceivedClientTime,
                };

                if (tracker.ReadyForWorldState)
                    SendPayload(tick, player, msg, tracker);
                else
                    SendNoPayload(tick, player, msg);
            }
        }

        private unsafe void SendPayload(int tick, INetworkPlayer player, DeltaWorldState msg, PlayerTimeTracker tracker)
        {
            var fromTick = CalculateFromTick(tick, tracker);

            var intSize = _worldStateCopy.Get(tick).IntSize;
            // todo cache payload for (fromTick->tick) so we dont need to serialize it for each player (eg if 2 players are on same tick)
            var payload = CreatePayload(fromTick, tick);

            PayloadLogging(fromTick, intSize, payload);

            msg.VsTick = fromTick;
            msg.StateIntSize = intSize;
            msg.DeltaState = payload;

            // todo make mirage AckSystem public so const fields can be used
            if (payload.Count <= MAX_NOTIFY_SIZE)
            {
                var token = TickNotifyToken.GetToken(tracker, tick);
                player.Send(msg, token);
            }
            else
            {
                FragmentSend(player, msg);
            }
        }

        private void FragmentSend(INetworkPlayer player, DeltaWorldState msg)
        {
            if (logger.WarnEnabled()) logger.LogWarning($"Payload size ({msg.DeltaState.Count} bytes) is over max Notify size ({MAX_NOTIFY_SIZE} bytes). using reliable-fragmented send instead");

            // send as reliable, it will be fragmented but will reach the client
            msg.Fragmented = true;
            player.Send(msg);

            // we can then mark this tick as acked because client will 100% get it
        }
        private void HandleFragmentedAck(INetworkPlayer player, DeltaWorldStateFragmentedAck msg)
        {
            var tracker = _playerTracker[player];
            tracker.SetLastAcked(msg.Tick);
        }


        private unsafe void PayloadLogging(int? fromTick, int intSize, ArraySegment<byte> payload)
        {
            if (fromTick == null && intSize != 0 && logger.LogEnabled())
                logger.Log($"Delta Write Vs Zero: Original Size:{intSize * 4} Compressed:{payload.Count}");

            _deltaSizeTracker.AddValue(payload.Count);
            if (_deltaSizeTracker.Values.Count > 60)
            {
                if (logger.LogEnabled())
                {
                    _deltaSizeTracker.Flush(out var avg, out var min, out var max);
                    logger.Log($"Delta Write: Original Size:{intSize * 4} Delta:[avg:{avg:0.0} min:{min} max:{max}]");
                }
                else
                {
                    _deltaSizeTracker.Clear();
                }
            }

            if (verbose.LogEnabled())
            {
                fixed (byte* p = &payload.Array[0])
                {
                    var iPtr = (int*)p;
                    _debugBuilder.Clear();
                    for (var j = 0; j < payload.Count / 4; j++)
                    {
                        if (j != 0)
                            _debugBuilder.Append(" ");
                        _debugBuilder.Append(iPtr[j].ToString("X8"));
                    }

                    if (payload.Count % 4 != 0)
                    {
                        var last = (uint)iPtr[payload.Count / 4];
                        var extraBytes = payload.Count % 4;
                        var mask = ~(uint.MaxValue << (extraBytes * 8));
                        var value = last & mask;

                        if (payload.Count > 4)
                            _debugBuilder.Append(" ");
                        _debugBuilder.Append(last.ToString("X8"));
                    }

                    verbose.Log($"DeltaState:{payload.Count} bytes, Hex:[{_debugBuilder}]");
                }
            }
        }

        private int? CalculateFromTick(int tick, PlayerTimeTracker tracker)
        {
            var fromTick = tracker.LastAckedTick;
            if (fromTick.HasValue)
            {
                // too far part
                if (tick - fromTick.Value >= _worldStateCopy.Count)
                {
                    fromTick = null;
                }
                else
                {
                    var toCopy = _worldStateCopy.Get(tick);
                    var fromCopy = _worldStateCopy.Get(fromTick.Value);

                    if (fromCopy.IntSize != toCopy.IntSize)
                    {
                        fromTick = null;
                    }
                }
            }

            return fromTick;
        }

        private static void SendNoPayload(int tick, INetworkPlayer player, DeltaWorldState msg)
        {
            // null tracker, dont track unless we send world state
            var token = TickNotifyToken.GetToken(null, tick);
            player.Send(msg, token);
        }

        private unsafe ArraySegment<byte> CreatePayload(int? fromTick, int toTick)
        {
            var writer = _payloadWriter;
            writer.Reset();

            var toCopy = _worldStateCopy.Get(toTick);
            if (fromTick.HasValue)
            {
                // todo check distance from/to. If it is too big then we should just send full vs zero
                var fromCopy = _worldStateCopy.Get(fromTick.Value);
                Debug.Assert(toCopy.IntSize == fromCopy.IntSize);
                _deltaSnapshot.WriteDelta(writer, toCopy.IntSize, fromCopy.Ptr, toCopy.Ptr);
            }
            else
            {
                _deltaSnapshot.WriteDeltaVsZero(writer, toCopy.IntSize, toCopy.Ptr);
            }

            return writer.ToArraySegment();
        }

        private unsafe void CopyStateForTick(int tick)
        {
            var snapshots = _worldSnapshot.Snapshots;
            var count = snapshots.Count;
            if (count == 0)
                return;

            var copy = _worldStateCopy.Get(tick);
            CheckSize(snapshots, copy);

            var writePtr = copy.Ptr;
            for (var i = 0; i < count; i++)
            {
                var snapshot = snapshots[i];
                var ptr = snapshot.GetStateAtTick(tick);
                var size = snapshot.IntSizePerTick;
                UnsafeHelper.Copy(ptr, writePtr, size);

                ValidateCopy(writePtr, snapshots, i);

                writePtr += size;

                if (verbose.LogEnabled())
                {
                    Verbose_LogBehaviourState(writePtr, snapshot);
                }
            }

            if (verbose.WarnEnabled())
            {
                var previous = _worldStateCopy.Get(tick - 1);
                if (previous.IntSize != copy.IntSize)
                    verbose.LogWarning($"Delta Write: Size changed. From:{previous.IntSize * 4} To:{copy.IntSize * 4}");
            }

            if (tick < _dumpToFileCount)
            {
                WorldStateDump.ToFile(tick, copy.Ptr, copy.IntSize);
            }
        }

        private static unsafe void ValidateCopy(int* writePtr, IReadOnlyList<IdentitySnapshot> snapshots, int index)
        {
            var header = (IdentitySnapshot.Header*)writePtr;
            if (header->NetId == 0)
            {
                var previous = index > 0 ? snapshots[index - 1] : default;
                var current = snapshots[index];
                var lastBehaviour = previous?.Snapshots.Last();
                throw new Exception($"Write netid as 0 at snapshotPosition for group:[index={index},name={current.name}] previousGroup:[name={previous?.name}] lastBehaviour in previous:{lastBehaviour?.GetType()}");
            }
        }

        private static unsafe void Verbose_LogBehaviourState(int* writePtr, IdentitySnapshot snapshot)
        {
            var startPtr = writePtr - snapshot.IntSizePerTick;
            verbose.Log($"WriteGroup:{snapshot.IntSizePerTick * 4} bytes, netId:{snapshot.Identity.NetId}, Object:{snapshot.Identity.name} Hex:[{*startPtr:X8}]");
            startPtr += 1;

            foreach (var behaviour in snapshot.Snapshots)
            {
                var netBehaviour = (NetworkBehaviour)behaviour;
                Debug.Assert(netBehaviour.NetId <= 0xffffff);
                Debug.Assert(netBehaviour.ComponentIndex <= 0xff);

                _debugBuilder.Clear();
                for (var j = 0; j < behaviour.AllocationSizeInts; j++)
                {
                    if (j != 0)
                        _debugBuilder.Append(" ");

                    var value = startPtr[j];
                    string intStr;
                    // zero is a common value, so avoid format string
                    if (value == 0)
                        intStr = "00000000";
                    else
                        intStr = value.ToString("X8");
                    _debugBuilder.Append(intStr);
                }
                verbose.Log($"WriteState:{behaviour.AllocationSizeInts * 4} bytes, Type:{behaviour.GetType()} Hex:[{_debugBuilder}]");
                startPtr += behaviour.AllocationSizeInts;
            }
        }

        private unsafe void CheckSize(IReadOnlyList<IdentitySnapshot> groups, WorldStateCopy copy)
        {
            var total = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                total += group.IntSizePerTick;
            }

            copy.CheckSize(_allocator, total);
        }

        private void HandleInput(INetworkPlayer player, InputState message)
        {
            var tracker = _playerTracker[player];
            tracker.LastReceivedClientTime = Math.Max(tracker.LastReceivedClientTime, message.ClientTime);
            // check if inputs have arrived in time and in order, otherwise we can't do anything with them.
            if (!ValidateInputTick(tracker, message.Tick))
                return;

            tracker.ReadyForWorldState = message.Ready;

            if (message.Ready)
                HandleReadyInput(player, message, tracker);

            tracker.lastReceivedInput = Mathf.Max(tracker.lastReceivedInput.GetValueOrDefault(), message.Tick);
        }

        private void HandleReadyInput(INetworkPlayer player, InputState message, PlayerTimeTracker tracker)
        {
            var length = message.NumberOfInputs;
            using (var reader = NetworkReaderPool.GetReader(message.Payload, _world))
            {
                // keep reading while there is atleast 1 byte
                // netBehaviour will be alteast 1 byte
                while (reader.CanReadBytes(1))
                {
                    var networkBehaviour = reader.ReadNetworkBehaviour();

                    if (networkBehaviour == null)
                    {
                        if (logger.WarnEnabled()) logger.LogWarning($"Spawned object not found when handling InputMessage message");
                        return;
                    }

                    if (player != networkBehaviour.Owner)
                        throw new InvalidOperationException($"player {player} does not have authority to set inputs for object. Object[Netid:{networkBehaviour.NetId}, name:{networkBehaviour.name}]");

                    if (!(networkBehaviour is IPredictionBehaviour behaviour))
                        throw new InvalidOperationException($"Networkbehaviour({networkBehaviour.NetId}, {networkBehaviour.ComponentIndex}) was not a IPredictionBehaviour");

                    var inputTick = message.Tick;
                    for (var i = 0; i < length; i++)
                    {
                        var t = inputTick - i;
                        behaviour.ServerController.ReadInput(tracker, reader, t, _lastSim);
                    }
                }
            }

        }

        private bool ValidateInputTick(PlayerTimeTracker tracker, int tick)
        {
            // received inputs out of order
            // we can ignore them, input[n+1] will contain input[n], so we would have no new inputs in this packet
            if (tracker.lastReceivedInput > tick)
            {
                if (logger.LogEnabled()) logger.Log($"received inputs out of order, lastReceived:{tracker.lastReceivedInput} new inputs:{tick}");
                return false;
            }

            // if lastTick is before last sim, then it is late and we can't use
            if (tick >= _lastSim)
            {
                if (verbose.LogEnabled()) verbose.Log($"received inputs for {tick}. lastSim:{_lastSim}. early by {tick - _lastSim}");
                return true;
            }

            if (logger.LogEnabled())
            {
                logger.Log($"received inputs <color=red>Late</color> for {tick}, lastSim:{_lastSim}. late by {_lastSim - tick}"
                + (tracker.lastReceivedInput == null ? ". But was at start, so not a problem" : ""));
            }

            return false;
        }

        internal class PlayerTimeTracker : ITickNotifyTracker
        {
            // todo use this to collect metrics about client (eg ping, rtt, etc)
            public double LastReceivedClientTime;
            public int? lastReceivedInput = null;

            public bool ReadyForWorldState;

            public int? LastAckedTick { get; private set; }

            public void SetLastAcked(int tick)
            {
                if (LastAckedTick.HasValue)
                    LastAckedTick = Math.Max(LastAckedTick.Value, tick);
                else
                    LastAckedTick = tick;
            }
            public void ClearLastAcked()
            {
                LastAckedTick = null;
            }
        }
    }
}
