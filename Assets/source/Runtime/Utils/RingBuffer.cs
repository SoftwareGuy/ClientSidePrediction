/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

namespace JamesFrowen.CSP
{
    /// <summary>
    /// A Ring buffer where each element can be set be set or not
    /// </summary>
    /// <remarks>
    /// This is useful when you need nullable struct types but dont want to require `where T is struct` on all types using this
    /// </remarks>
    public class RingBuffer<T>
    {
        private readonly int _size;
        private readonly T[] _buffer;

        public int Count => _size;

        /// <summary>
        /// Can be used to loop over all items when order is not required
        /// </summary>
        public T[] All => _buffer;

        public RingBuffer(int size)
        {
            _size = size;
            _buffer = new T[size];
        }

        protected int IndexToBuffer(int index)
        {
            //negative
            if (index < 0)
                index += _size;
            return index % _size;
        }

        public T this[int index] => Get(index);

        public T Get(int index)
        {
            return _buffer[IndexToBuffer(index)];
        }

        public void Set(int index, T value)
        {
            _buffer[IndexToBuffer(index)] = value;
        }

        public void Clear(int index)
        {
            _buffer[IndexToBuffer(index)] = default;
        }
    }
}
