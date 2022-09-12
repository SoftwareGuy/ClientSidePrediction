/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.IO;
using UnityEngine;

namespace JamesFrowen.CSP.Debugging
{
    internal static unsafe class WorldStateDump
    {
        private static byte[] buffer;

        private static string PathFromTick(int tick) => Path.Combine(Application.persistentDataPath, "WorldState", $"{tick:D4}.data");

        public static void ToFile(int tick, int* ptr, int intSize)
        {
            if (buffer == null)
                buffer = new byte[intSize];
            if (buffer.Length < intSize * 4)
                Array.Resize(ref buffer, intSize * 4);

            fixed (byte* bPtr = &buffer[0])
            {
                var b = (int*)bPtr;

                for (var i = 0; i < intSize; i++)
                {
                    b[i] = ptr[i];
                }
            }

            var path = PathFromTick(tick);
            CheckDir(path);
            File.WriteAllBytes(path, buffer);
        }

        private static void CheckDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir))
                return;

            Directory.CreateDirectory(dir);
        }

        public static byte[] FromFile(int tick)
        {
            var path = PathFromTick(tick);
            return File.ReadAllBytes(path);
        }

        public static void ClearFolder()
        {
            var path = PathFromTick(0);
            var dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
                return;

            Directory.Move(dir, dir + "_prev");
        }
    }
}
