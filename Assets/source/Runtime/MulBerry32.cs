/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using UnityEngine;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Creates random numbers from a seed value, can be used to create by Client Side Prediction to create same values on client and server
    /// <para>
    /// For example: using previous state to get next "random" state
    /// </para>
    /// </summary>
    public static class RNG
    {
        // MulBerry32 is under public domain
        // see: https://gist.github.com/tommyettinger/46a874533244883189143505d203312c
        public static uint MulBerry32(uint z)
        {
            z += 0x6D2B79F5;
            z = (z ^ z >> 15) * (1 | z);
            z ^= z + (z ^ z >> 7) * (61 | z);
            return z ^ z >> 14;
        }

        /// <summary>
        /// Value from 0 to 1
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static unsafe float Next(float v)
        {
            return MulBerry32(*(uint*)&v) / uint.MaxValue;
        }

        public static unsafe float Next(float v, float min, float max)
        {
            return min + ((max - min) * Next(v));
        }

        public static Vector3 InsideUnitSphere(float seed)
        {
            var x = Next(seed);
            var y = Next(x);
            var z = Next(y);
            return new Vector3(x, y, z).normalized;
        }
    }
}
