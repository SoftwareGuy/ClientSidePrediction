/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System.Collections.Generic;

namespace JamesFrowen.CSP
{

    /// <summary>
    /// A Ring buffer where each element can be set be set or not
    /// </summary>
    /// <remarks>
    /// This is useful when you need nullable struct types but dont want to require `where T is struct` on all types using this
    /// </remarks>
    public class NullableRingBuffer<T> : RingBuffer<NullableRingBuffer<T>.Valid>
    {
        public NullableRingBuffer(int size) : base(size) { }

        public new T Get(int index)
        {
            var item = base.Get(index);
            if (item.HasValue)
                return item.Value;
            else
                throw new KeyNotFoundException($"Value at {IndexToBuffer(index)} is null");
        }

        public T GetOrDefault(int index)
        {
            var item = base.Get(index);
            if (item.HasValue)
                return item.Value;
            else
                return default;
        }

        /// <summary>
        /// Does the element at the index have a value?
        /// </summary>
        public bool IsValid(int index)
        {
            var item = base.Get(index);
            return item.HasValue;
        }

        public bool TryGet(int index, out T value)
        {
            var item = base.Get(index);
            if (item.HasValue)
            {
                value = item.Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public void Set(int index, T value)
        {
            base.Set(index, new Valid
            {
                Value = value,
                HasValue = true,
            });
        }

        // we can't use nullable here or we will have to limit T to struct
        // T should probably be limited to struct anyway, but seems like a pain to put `where T : struct`  everywhere
        // todo should we just make T a struct??
        public struct Valid
        {
            public T Value;
            public bool HasValue;
        }
    }
}
