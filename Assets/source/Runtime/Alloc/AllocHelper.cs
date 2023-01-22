/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.Runtime.InteropServices;

namespace JamesFrowen.CSP.Alloc
{
    public static class AllocHelper
    {
        public static void ZeroMemory(IntPtr ptr, int byteLength)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            RtlZeroMemory(ptr, new UIntPtr((uint)byteLengthh));
#else
            ZeroNonWindwos(ptr, byteLengthh);
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll")]
        private static extern void RtlZeroMemory(IntPtr dst, UIntPtr length);
#endif

        private static unsafe void ZeroNonWindwos(IntPtr dst, int length)
        {
            var ptr = (byte*)dst;
            for (var i = 0; i < length; i++)
            {
                ptr[i] = 0;
            }
        }
    }
}
