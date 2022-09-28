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
        private readonly SnapshotGroupManager _stateManager;
        private readonly RingBuffer<TickSnapshot> _ticks;

        public TickSnapshot GetTick(int tick) => _ticks[tick];

        public WorldSnapshot(SnapshotGroupManager stateManager, int bufferSize)
        {
            _stateManager = stateManager;
            _ticks = new RingBuffer<TickSnapshot>(bufferSize);
            for (var i = 0; i < bufferSize; i++)
            {
                _ticks.Set(i, new TickSnapshot());
            }
        }

        public unsafe void BeforeSimulate(int tick)
        {
            var previousTick = _ticks[tick - 1];
            var nextTick = _ticks[tick];

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
            // remove groups not in StateManager
            for (var i = nextTick.Groups.Count - 1; i >= 0; i--)
            {
                var group = nextTick.Groups[i];
                if (!_stateManager.Groups.ContainsKey(group.Identity.NetId))
                {
                    nextTick.RemoveAt(i);
                    _stateManager.ReleaseGroup(group);
                }
            }

            var needSorting = false;
            foreach (var group in _stateManager.Groups.Values)
            {
                // group already in next, we dont need to create/alloc new
                if (nextTick.Lookup.TryGetValue(group.Identity.NetId, out var _))
                    continue;

                needSorting = true;
                var newGroup = _stateManager.CopyGroup(group, true);
                GroupSnapshot.CopySnapshot(group, newGroup);
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
            for (var i = 0; i < nextTick.Groups.Count; i++)
            {
                var nextGroup = nextTick.Groups[i];
                if (previousTick.Lookup.TryGetValue(nextGroup.Identity.NetId, out var previousGroup))
                {
                    GroupSnapshot.CopySnapshot(previousGroup, nextGroup);
                }
            }
        }
    }

    public unsafe class TickSnapshot
    {
        private List<GroupSnapshot> _groups = new List<GroupSnapshot>();
        private Dictionary<uint, GroupSnapshot> _lookup = new Dictionary<uint, GroupSnapshot>();

        public IReadOnlyList<GroupSnapshot> Groups => _groups;
        public IReadOnlyDictionary<uint, GroupSnapshot> Lookup => _lookup;

        public void Add(GroupSnapshot group)
        {
            _groups.Add(group);
            Debug.Assert(group.Identity.NetId != 0);
            _lookup.Add(group.Identity.NetId, group);
        }
        public void RemoveAt(int index)
        {
            var group = _groups[index];
            _groups.RemoveAt(index);
            Debug.Assert(group.Identity.NetId != 0);
            _lookup.Remove(group.Identity.NetId);
        }

        public void Sort()
        {
            _groups.Sort(GroupCompare);
        }

        private static int GroupCompare(GroupSnapshot x, GroupSnapshot y)
        {
            var xId = x.Identity.NetId;
            var yId = y.Identity.NetId;
            return xId.CompareTo(yId);
        }

        public void SetAsActivePtr()
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                group.SetAsActivePtr();
            }
        }
    }

    public class SnapshotGroupManager
    {
        private readonly Dictionary<uint, GroupSnapshot> _groups = new Dictionary<uint, GroupSnapshot>();
        private readonly ISnapshotAllocator _alloc;

        public IReadOnlyDictionary<uint, GroupSnapshot> Groups => _groups;

        public SnapshotGroupManager(ISnapshotAllocator alloc)
        {
            _alloc = alloc;
        }

        public GroupSnapshot CreateGroup(NetworkIdentity identity, ISnapshotBehaviour[] snapshots, bool allocate)
        {
            var intSize = 0;
            foreach (var snapshot in snapshots)
            {
                intSize += snapshot.AllocationSizeInts;
            }

            // +1 for netid
            intSize += GroupSnapshot.Header.INT_SIZE;

            var group = new GroupSnapshot(identity, snapshots, intSize);
            if (allocate)
                AllocateForGroup(group);

            return group;
        }
        public GroupSnapshot CopyGroup(GroupSnapshot group, bool allocate)
        {
            var clone = new GroupSnapshot(group.Identity, group.Snapshots, group.IntSize);
            if (allocate)
                AllocateForGroup(clone);

            return clone;
        }

        public unsafe void AllocateForGroup(GroupSnapshot group)
        {
            if (group.Identity.NetId == 0)
                throw new ArgumentException($"Identity must be spawned before Allocating for group");
            if (group.Ptr != null)
                throw new InvalidOperationException($"Group already been allocated");

            _alloc.Allocate(group, group.IntSize * 4);
            group.SetHeader();
        }

        public void AddGroup(GroupSnapshot group)
        {
            Debug.Assert(group.Identity.NetId != 0);
            _groups.Add(group.Identity.NetId, group);
        }

        public void ReleaseGroup(GroupSnapshot group)
        {
            _alloc.Release(group);
        }

        public void Remove(NetworkIdentity identity, bool release)
        {
            if (release)
            {
                var group = _groups[identity.NetId];
                ReleaseGroup(group);
            }
            Debug.Assert(identity.NetId != 0);
            _groups.Remove(identity.NetId);
        }
    }

    public unsafe class GroupSnapshot : IHasAllocatedPointer
    {
        public readonly NetworkIdentity Identity;
        public readonly ISnapshotBehaviour[] Snapshots;
        public readonly int IntSize;

        public void* Ptr { get; set; }

        public GroupSnapshot(NetworkIdentity identity, ISnapshotBehaviour[] snapshots, int intSize)
        {
            Identity = identity;
            Snapshots = snapshots;
            IntSize = intSize;
        }

        public string name => $"Group for {Identity.name} ({Identity.NetId})";

        public void SetAsActivePtr()
        {
            var ptr = (int*)Ptr;
            // skip netId
            ptr += Header.INT_SIZE;

            var end = ptr + IntSize;
            for (var i = 0; i < Snapshots.Length; i++)
            {
                if (ptr > end)
                    throw new InvalidOperationException("Failing to split up pointer, getting past end of buffer");

                Snapshots[i].Ptr = ptr;
                ptr += Snapshots[i].AllocationSizeInts;
            }
        }

        public void SetHeader()
        {
            if (Identity.NetId == 0)
                throw new InvalidOperationException($"NetId zero for {Identity.name}");

            var hPtr = (Header*)Ptr;
            hPtr->NetId = Identity.NetId;

            //Debug.Assert(Identity.IsPrefab);
            //hPtr->PrefabHash = Identity.PrefabHash;
        }

        public static void CopySnapshot(GroupSnapshot previousGroup, GroupSnapshot nextGroup)
        {
            var size = nextGroup.IntSize;
            Debug.Assert(size == previousGroup.IntSize);
            UnsafeHelper.Copy(previousGroup.Ptr, nextGroup.Ptr, size);
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
