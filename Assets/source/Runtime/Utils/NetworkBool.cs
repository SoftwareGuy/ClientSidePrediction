/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mirage.Serialization;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Use instead of bool in network state
    /// <para>Bools are not a blittable type so maybe not be the same on all platforms</para>
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct NetworkBool
    {
        [FieldOffset(0)] private byte _value;

        public bool Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value == 1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(NetworkBool value) => value.Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NetworkBool(bool value) => new NetworkBool() { Value = value };

        public T? GetNullable<T>(ref T field) where T : struct
        {
            return Value ? field : default(T?);
        }
        public void SetNullable<T>(ref T field, T? value) where T : struct
        {
            Value = value.HasValue;
            if (value.HasValue)
            {
                field = value.Value;
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }


    public static class NetworkBoolExtensions
    {
        public static void Write(this NetworkWriter writer, NetworkBool value)
        {
            writer.WriteBoolean(value);
        }
        public static NetworkBool Read(this NetworkReader reader)
        {
            return reader.ReadBoolean();
        }
    }
}
