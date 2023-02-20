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
    internal class StateSender
    {
        // todo what should this really be?
        private const int MESSAGE_HEADER = 26; // rough guess
        private const int MAX_NOTIFY_SIZE = 1219 - MESSAGE_HEADER;

        private static readonly ILogger logger = LogFactory.GetLogger("JamesFrowen.CSP.StateSender");
        private static readonly ILogger verbose = LogFactory.GetLogger("JamesFrowen.CSP.StateSender_Verbose", LogType.Exception);

        private readonly List<INetworkPlayer> _players;
        private readonly Dictionary<INetworkPlayer, PlayerTimeTracker> _playerTracker;
        private readonly IAllocator _allocator;
        private readonly WorldSnapshot _worldSnapshot;

        private readonly RingBuffer<WorldStateCopy> _worldStateCopy;
        private readonly NetworkWriter _payloadWriter;
        private readonly DeltaSnapshotWriter _deltaSnapshot;

        // debugging
        private static readonly StringBuilder debugBuilder = new StringBuilder();
        private readonly LogValueTracker _deltaSizeTracker = new LogValueTracker();
        // todo expose this value for debugging
        private readonly int _dumpToFileCount = 0;

        public StateSender(
            List<INetworkPlayer> players,
            Dictionary<INetworkPlayer, PlayerTimeTracker> playerTracker,
            WorldSnapshot worldSnapshot,
            IAllocator allocator,
            int bufferSize)
        {
            _players = players;
            _playerTracker = playerTracker;
            _worldSnapshot = worldSnapshot;
            _allocator = allocator;

            _worldStateCopy = new RingBuffer<WorldStateCopy>(bufferSize);
            _worldStateCopy.FillWithNew<WorldStateCopy>();
            _payloadWriter = new NetworkWriter(1300, true);
            _deltaSnapshot = new DeltaSnapshotWriter(_allocator);

            if (_dumpToFileCount > 0)
                WorldStateDump.ClearFolder();
        }

        public void SendState(int tick)
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
        public void HandleFragmentedAck(INetworkPlayer player, DeltaWorldStateFragmentedAck msg)
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
                    debugBuilder.Clear();
                    for (var j = 0; j < payload.Count / 4; j++)
                    {
                        if (j != 0)
                            debugBuilder.Append(" ");
                        debugBuilder.Append(iPtr[j].ToString("X8"));
                    }

                    if (payload.Count % 4 != 0)
                    {
                        var last = (uint)iPtr[payload.Count / 4];
                        var extraBytes = payload.Count % 4;
                        var mask = ~(uint.MaxValue << (extraBytes * 8));
                        var value = last & mask;

                        if (payload.Count > 4)
                            debugBuilder.Append(" ");
                        debugBuilder.Append(value.ToString("X8"));
                    }

                    verbose.Log($"DeltaState:{payload.Count} bytes, Hex:[{debugBuilder}]");
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

                debugBuilder.Clear();
                for (var j = 0; j < behaviour.AllocationSizeInts; j++)
                {
                    if (j != 0)
                        debugBuilder.Append(" ");

                    var value = startPtr[j];
                    string intStr;
                    // zero is a common value, so avoid format string
                    if (value == 0)
                        intStr = "00000000";
                    else
                        intStr = value.ToString("X8");
                    debugBuilder.Append(intStr);
                }
                verbose.Log($"WriteState:{behaviour.AllocationSizeInts * 4} bytes, Type:{behaviour.GetType()} Hex:[{debugBuilder}]");
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
    }

    public class StateReceiver
    {

    }

}
