/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System.Collections.Generic;

namespace JamesFrowen.CSP.Debugging
{
    internal class LogValueTracker
    {
        public readonly List<int> Values = new List<int>();

        public void AddValue(int value)
        {
            Values.Add(value);
        }

        public void Clear() => Values.Clear();

        /// <summary>
        /// Calculates metrics then clears values
        /// </summary>
        /// <param name="avg"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void Flush(out float avg, out int min, out int max)
        {
            var sum = 0;
            min = int.MaxValue;
            max = int.MinValue;
            var count = Values.Count;
            for (var i = 0; i < count; i++)
            {
                var value = Values[i];
                if (value < min)
                    min = value;
                if (value > max)
                    max = value;

                sum += value;
            }
            avg = (float)sum / count;

            Values.Clear();
        }
    }
}
