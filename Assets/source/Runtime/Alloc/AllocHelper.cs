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
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WINDOWS || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            // We're a Windows, Mac or Linux platform: Use native's memset function. 
            memset((IntPtr)ptr, 0, (UIntPtr)byteLength);
#else
            // Fail-safe for all the other platforms (mobile, console (?), ...)
            ZeroMemoryFallback(ptr, byteLength);
#endif
        }

        /// <summary>
        /// Zeroes memory in the native world. Beware, improper use of this
        /// functionality will likely make things explode.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="length"></param>
        private static unsafe void ZeroMemoryFallback(IntPtr dst, int length)
        {
            var ptr = (byte*)dst;
            for (var i = 0; i < length; i++)
            {
                ptr[i] = 0;
            }
        }

        #region Platform-dependent Imports
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [SuppressUnmanagedCodeSecurity]
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        static extern IntPtr memset(IntPtr dest, int value, UIntPtr byteCount);
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        [SuppressUnmanagedCodeSecurity]
        [DllImport("libc", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        static extern IntPtr memset(IntPtr dest, int value, UIntPtr byteCount);    
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [SuppressUnmanagedCodeSecurity]
        [DllImport("libc", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        static extern IntPtr memset(IntPtr dest, int value, UIntPtr byteCount);  
#endif
        #endregion
    }
}
