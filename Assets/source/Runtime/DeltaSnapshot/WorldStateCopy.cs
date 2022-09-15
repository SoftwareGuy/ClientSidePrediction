/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using JamesFrowen.CSP.Alloc;

namespace JamesFrowen.DeltaSnapshot
{
    internal class WorldStateCopy : IHasAllocatedPointer
    {
        public string name => "WorldStateCopy";

        unsafe void* IHasAllocatedPointer.Ptr
        {
            get => Ptr;
            set => Ptr = (int*)value;
        }
        public unsafe int* Ptr { get; private set; }
        /// <summary>
        /// Size in ints (32 bits)
        /// </summary>
        public int IntSize;

        internal unsafe void CheckSize(ISnapshotAllocator allocator, int intCount)
        {
            if (IntSize == intCount)
                return;

            if (Ptr != null)
                allocator.Release(this);

            // *4 because int
            allocator.Allocate(this, intCount * 4);
            IntSize = intCount;
        }
    }
}
