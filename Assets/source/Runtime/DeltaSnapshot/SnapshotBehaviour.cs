/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using JamesFrowen.CSP.Alloc;
using Mirage;

namespace JamesFrowen.DeltaSnapshot
{
    public unsafe interface ISnapshotBehaviour : IHasAllocatedPointer
    {
        /// <summary>
        /// Allocation size in ints (32 bit)
        /// </summary>
        int AllocationSizeInts { get; }

        uint NetId { get; }

        /// <summary>
        /// Offset of pointer from main allocation
        /// </summary>
        int PtrIntOffset { get; set; }
    }

    public abstract unsafe class SnapshotBehaviour<TState> : NetworkBehaviour, ISnapshotBehaviour where TState : unmanaged
    {
        public bool HasState => _statePtr != null;

        internal TState* _statePtr;

        protected TState* StatePtr => _statePtr;

        // todo test if this throws NRE when ptr is 0
        protected ref TState State
        {
            get
            {
                if (_statePtr == null)
                    ThrowNullState();

                return ref *_statePtr;
            }
        }

        private void ThrowNullState()
        {
            throw new SnapshotException($"state pointer is null for '{GetType().Name}' on [netid={Identity.NetId} name='{name}']");
        }


        void* IHasAllocatedPointer.Ptr
        {
            get => _statePtr;
            set => _statePtr = (TState*)value;
        }

        int ISnapshotBehaviour.PtrIntOffset { get; set; }

        // round up to nearest 32 bit;
        public int AllocationSizeInts => (sizeof(TState) + 3) / 4;
    }
}
