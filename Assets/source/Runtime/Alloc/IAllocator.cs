/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;

namespace JamesFrowen.CSP.Alloc
{
    public unsafe interface IAllocator : IDisposable
    {
        void Allocate(IHasAllocatedPointer owner, int byteCount);
        void Release(IHasAllocatedPointer owner);
    }

    public unsafe interface IHasAllocatedPointer
    {
        string name { get; }
        void* Ptr { get; set; }
    }
}
