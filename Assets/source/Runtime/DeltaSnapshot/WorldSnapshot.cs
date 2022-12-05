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
using System.Runtime.InteropServices;
using JamesFrowen.CSP;
using JamesFrowen.CSP.Alloc;
using Mirage;
using UnityEngine;

namespace JamesFrowen.DeltaSnapshot
{
    public class WorldSnapshot
    {
        private readonly RingBuffer<TickSnapshot> _snapshots;
        private readonly IAllocator _allocator;

        /// <summary>
        /// All Ideneities spawned in the world, might not be synced with current tick state for Identies that have spawneds this frame
        /// </summary>
        private readonly Dictionary<uint, IdentitySnapshot> _spawned = new Dictionary<uint, IdentitySnapshot>();
        public IReadOnlyDictionary<uint, IdentitySnapshot> Spawned => _spawned;


        public TickSnapshot GetTick(int tick) => _snapshots[tick];

        /// <param name="tickBufferSize">size of ring buffer for snapshots. Thiis is the number of previous snapshots that will be able to be used for delta compression.</param>
        public WorldSnapshot(IAllocator allocator, int tickBufferSize)
        {
            _allocator = allocator;
            _snapshots = new RingBuffer<TickSnapshot>(tickBufferSize);
            for (var i = 0; i < tickBufferSize; i++)
            {
                _snapshots.Set(i, new TickSnapshot());
            }
        }

        public void AddGroup(IdentitySnapshot group)
        {
            Debug.Assert(group.Identity.NetId != 0);
            _spawned.Add(group.Identity.NetId, group);
        }

        public void Remove(NetworkIdentity identity, bool release)
        {
            if (release)
            {
                var group = _spawned[identity.NetId];
                group.Release(_allocator);
            }
            Debug.Assert(identity.NetId != 0);
            _spawned.Remove(identity.NetId);
        }

        public unsafe void BeforeSimulate(int tick)
        {
            var previousTick = _snapshots[tick - 1];
            var nextTick = _snapshots[tick];

            SyncWithStateManager(nextTick);
            CopyFromPrevious(previousTick, nextTick);
            nextTick.SetAsActivePtr();
        }

        /// <summary>
        /// Add/Remove missing groups to tick from StateManager
        /// </summary>
        /// <param name="nextTick"></param>
        public void SyncWithStateManager(TickSnapshot nextTick)
        {
            // remove identities from tick if they have been unspanwed (not in 
            for (var i = nextTick.Identities.Count - 1; i >= 0; i--)
            {
                var group = nextTick.Identities[i];
                if (!_spawned.ContainsKey(group.Identity.NetId))
                {
                    nextTick.RemoveAt(i);
                    group.Release(_allocator);
                }
            }

            var needSorting = false;
            foreach (var group in _spawned.Values)
            {
                // group already in next, we dont need to create/alloc new
                if (nextTick.Lookup.TryGetValue(group.Identity.NetId, out var _))
                    continue;

                needSorting = true;
                var newGroup = IdentitySnapshot.Clone(group, _allocator);
                CopySnapshot(group, newGroup);
                nextTick.Add(newGroup);
            }

            if (needSorting)
                nextTick.Sort();
        }

        /// <summary>
        /// Finds groups in both previous and next, and then copies state from preivous to next
        /// </summary>
        /// <param name="previousTick"></param>
        /// <param name="nextTick"></param>
        private unsafe void CopyFromPrevious(TickSnapshot previousTick, TickSnapshot nextTick)
        {
            for (var i = 0; i < nextTick.Identities.Count; i++)
            {
                var nextGroup = nextTick.Identities[i];
                if (previousTick.Lookup.TryGetValue(nextGroup.Identity.NetId, out var previousGroup))
                {
                    CopySnapshot(previousGroup, nextGroup);
                }
            }
        }

        private unsafe void CopySnapshot(IdentitySnapshot previousGroup, IdentitySnapshot nextGroup)
        {
            var size = nextGroup.IntSize;
            Debug.Assert(size == previousGroup.IntSize);
            UnsafeHelper.Copy(previousGroup.Ptr, nextGroup.Ptr, size);
        }
    }

    /// <summary>
    /// Snapshot of all Identities for a tick
    /// </summary>
    public unsafe class TickSnapshot
    {
        private readonly List<IdentitySnapshot> _identities = new List<IdentitySnapshot>();
        private readonly Dictionary<uint, IdentitySnapshot> _lookup = new Dictionary<uint, IdentitySnapshot>();

        public IReadOnlyList<IdentitySnapshot> Identities => _identities;
        public IReadOnlyDictionary<uint, IdentitySnapshot> Lookup => _lookup;

        public void Add(IdentitySnapshot group)
        {
            _identities.Add(group);
            Debug.Assert(group.Identity.NetId != 0);
            _lookup.Add(group.Identity.NetId, group);
        }
        public void RemoveAt(int index)
        {
            var group = _identities[index];
            _identities.RemoveAt(index);
            Debug.Assert(group.Identity.NetId != 0);
            _lookup.Remove(group.Identity.NetId);
        }

        public void Sort()
        {
            _identities.Sort(GroupCompare);
        }

        private static int GroupCompare(IdentitySnapshot x, IdentitySnapshot y)
        {
            var xId = x.Identity.NetId;
            var yId = y.Identity.NetId;
            return xId.CompareTo(yId);
        }

        public void SetAsActivePtr()
        {
            for (var i = 0; i < _identities.Count; i++)
            {
                var group = _identities[i];
                group.SetAsActivePtr();
            }
        }
    }


    /// <summary>
    /// An allocation for a single NetworkIdentity for a single tick
    /// </summary>
    public unsafe class IdentitySnapshot : IHasAllocatedPointer
    {
        public readonly NetworkIdentity Identity;
        public readonly ISnapshotBehaviour[] Snapshots;
        public readonly int IntSize;

        public void* Ptr { get; set; }
        public string name => $"Group for {Identity.name} ({Identity.NetId})";


        /// <summary>
        /// Creates a group and sets up <paramref name="behaviours"/> with pointer offset and manager reference
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="identity"></param>
        /// <param name="behaviours"></param>
        /// <param name="allocator">Leaeve as null to now allocate right away. Call <see cref="Allocate(IAllocator)"/> to allocate later</param>
        /// <returns></returns>
        public static IdentitySnapshot Create(NetworkIdentity identity, ISnapshotBehaviour[] behaviours, IAllocator allocator = null)
        {
            // +1 for netid
            var intSize = IdentitySnapshot.Header.INT_SIZE;
            foreach (var behaviour in behaviours)
            {
                // set offset of this behaviour to be the size of all previous
                behaviour.PtrIntOffset = intSize;

                intSize += behaviour.AllocationSizeInts;
            }

            var snapshot = new IdentitySnapshot(identity, behaviours, intSize);
            if (allocator != null)
                snapshot.Allocate(allocator);

            return snapshot;
        }

        /// <summary>
        /// Creates a new snapshot from an existing one
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="allocator">Leaeve as null to now allocate right away. Call <see cref="Allocate(IAllocator)"/> to allocate later</param>
        /// <returns></returns>
        public static IdentitySnapshot Clone(IdentitySnapshot snapshot, IAllocator allocator = null)
        {
            var clone = new IdentitySnapshot(snapshot.Identity, snapshot.Snapshots, snapshot.IntSize);
            if (allocator != null)
                clone.Allocate(allocator);

            return clone;
        }

        private IdentitySnapshot(NetworkIdentity identity, ISnapshotBehaviour[] snapshots, int intSize)
        {
            Identity = identity;
            Snapshots = snapshots;
            IntSize = intSize;
        }


        /// <summary>
        /// Call this after allocation PTR to set up the header
        /// </summary>
        public void SetHeader()
        {
            if (Identity.NetId == 0)
                throw new InvalidOperationException($"NetId zero for {Identity.name}");

            var hPtr = (Header*)Ptr;
            hPtr->NetId = Identity.NetId;

            // todo does header need prefab hash so client can spawn it if missing?
            //Debug.Assert(Identity.IsPrefab);
            //hPtr->PrefabHash = Identity.PrefabHash;
        }

        public void SetAsActivePtr()
        {
            for (var i = 0; i < Snapshots.Length; i++)
            {
                Snapshots[i].Ptr = (int*)Ptr + Snapshots[i].PtrIntOffset;
            }
        }

        public unsafe void Allocate(IAllocator allocator)
        {
            if (Identity.NetId == 0)
                throw new ArgumentException($"Identity must be spawned before Allocating for group");
            if (Ptr != null)
                throw new InvalidOperationException($"Group already been allocated");

            allocator.Allocate(this, IntSize * 4);
            SetHeader();
        }
        public unsafe void Release(IAllocator allocator)
        {
            allocator.Release(this);
        }

        [StructLayout(LayoutKind.Explicit, Size = INT_SIZE)]
        public struct Header
        {
            public const int INT_SIZE = 1;

            [FieldOffset(0)] public uint NetId;
        }
    }

    public unsafe class UnsafeHelper
    {
        public static void Copy(void* from, void* to, int intCount)
        {
            Copy((int*)from, (int*)to, intCount);
        }
        public static void Copy(int* from, int* to, int intCount)
        {
            for (var j = 0; j < intCount; j++)
            {
                to[j] = from[j];
            }
        }
    }
}
