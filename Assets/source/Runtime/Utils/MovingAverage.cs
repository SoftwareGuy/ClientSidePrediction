/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;

namespace JamesFrowen.CSP
{
    /// <summary>
    /// Calcualtes the average of the previous N values
    /// </summary>
    public class MovingAverage
    {
        private readonly float[] _values;
        private int _index;

        /// <summary>
        /// how many values are stored in ring buffer
        /// <para>this value is needed for first loop of inserts</para>
        /// </summary>
        private int _countInBuffer;

        public MovingAverage(int size)
        {
            _values = new float[size];
        }

        public void Add(float value)
        {
            _values[_index] = value;
            _index++;
            if (_index >= _values.Length)
                _index = 0;

            _countInBuffer = Math.Max(_index, _countInBuffer);
        }

        public (float average, float stdDev) GetAverageAndStandardDeviation()
        {
            var average = GetAverage();
            var stdDev = calculateStandardDeviation(average);
            return (average, stdDev);
        }

        public float GetAverage()
        {
            if (_countInBuffer == 0)
                return 0;

            var sum = 0f;
            for (var i = 0; i < _countInBuffer; i++)
            {
                sum += _values[i];
            }

            return sum / _countInBuffer;
        }

        public float GetStandardDeviation()
        {
            var average = GetAverage();
            return calculateStandardDeviation(average);
        }

        private float calculateStandardDeviation(float average)
        {
            if (_countInBuffer < 2)
                return 0;

            var sum = 0f;
            for (var i = 0; i < _countInBuffer; i++)
            {
                var diff = _values[i] - average;
                sum += diff * diff;
            }

            var sqStdDev = sum / (_countInBuffer - 1);
            return (float)Math.Sqrt(sqStdDev);
        }

        public void Reset()
        {
            _index = 0;
            _countInBuffer = 0;
        }
    }
}
