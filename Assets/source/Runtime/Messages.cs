/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using Mirage;
using Mirage.Serialization;

namespace JamesFrowen.CSP
{
    [NetworkMessage]
    internal struct DeltaWorldState
    {
        public int Tick;
        public int? VsTick;
        /// <summary>
        /// Time scale on server, null if default value of 1
        /// </summary>
        public float? TimeScale;

        /// <summary>
        /// Send the last received time back to the client
        /// <para>This will be used by the client to caculate its local time</para>
        /// </summary>
        public double ClientTime;

        /// <summary>
        /// Size of state before delta
        /// </summary>
        public int StateIntSize;
        public bool Fragmented;
        public ArraySegment<byte> DeltaState;
    }

    [NetworkMessage]
    internal struct DeltaWorldStateFragmentedAck
    {
        public int Tick;
    }

    /// <summary>
    /// All inputs for client
    /// </summary>
    [NetworkMessage]
    internal struct InputState
    {
        public int Tick;
        public double ClientTime;
        public bool Ready;

        /// <summary>
        /// How many inputs were sent in payload
        /// </summary>
        [BitCountFromRange(1, 8)]
        public int NumberOfInputs;

        /// <summary>
        /// collection of <see cref="InputMessage"/>
        /// </summary>
        public ArraySegment<byte> Payload;
    }

    public enum SimulationMode
    {
        Physics3D,
        Physics2D,
        Local3D,
        Local2D,
    }
}
