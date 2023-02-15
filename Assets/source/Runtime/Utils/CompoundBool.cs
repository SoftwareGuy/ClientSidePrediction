/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Use to store multiple bools in a single byte
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct CompoundBool
    {
        [FieldOffset(0)] public byte Raw;

        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                CheckRange(index);
#endif
                return (Raw & (1 << index)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if DEBUG
                CheckRange(index);
#endif
                Raw = (byte)((Raw & ~(1 << index)) | ((value ? 1 : 0) << index));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckRange(int index)
        {
            if (index < 0) ThrowOutOfRange(index);
            if (index >= 8) ThrowOutOfRange(index);
        }

        private static void ThrowOutOfRange(int index)
        {
            throw new IndexOutOfRangeException($"Index for {nameof(CompoundBool)} was out of range, Index:{index}");
        }
    }
}
