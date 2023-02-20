/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;
using System.Collections.Generic;
using Mirage;
using Mirage.Logging;
using UnityEngine;

namespace JamesFrowen.CSP
{
    internal static class GetBehaviourCache<T>
    {
        private static readonly List<T> cache = new List<T>();

        public static List<T> GetComponentsInChildren(GameObject gameObject, bool includeInactive)
        {
            cache.Clear();
            gameObject.GetComponentsInChildren(includeInactive, cache);
            return cache;
        }

        public static List<T> GetBehaviours(NetworkIdentity identity)
        {
            cache.Clear();
            foreach (var behaviour in identity.NetworkBehaviours)
            {
                if (behaviour is T t)
                {
                    cache.Add(t);
                }
            }
            return cache;
        }
    }
    internal class PredictionCollection
    {
        private static readonly ILogger logger = LogFactory.GetLogger<PredictionCollection>();

        private readonly HashSet<NetworkIdentity> _gameObjects = new HashSet<NetworkIdentity>();
        private readonly List<IPredictionUpdates> _sortedUpdates = new List<IPredictionUpdates>();
        private readonly List<IPredictionBehaviour> _sortedBehaviours = new List<IPredictionBehaviour>();
        private readonly IPredictionTime _time;

        private bool _needsSorting;

        public PredictionCollection(IPredictionTime time)
        {
            _time = time;
        }

        public void Add(NetworkIdentity identity,
            out IReadOnlyList<IPredictionUpdates> newUpdates,
            out IReadOnlyList<IPredictionBehaviour> newBehaviours)
        {
            if (_gameObjects.Contains(identity))
                throw new InvalidOperationException($"Already added {identity.name}");

            _gameObjects.Add(identity);

            var updates = GetBehaviourCache<IPredictionUpdates>.GetBehaviours(identity);
            var behaviours = GetBehaviourCache<IPredictionBehaviour>.GetBehaviours(identity);

            newUpdates = updates;
            newBehaviours = behaviours;

            if (updates.Count == 0 && behaviours.Count == 0)
                return;

            _needsSorting = true;

            foreach (var update in updates)
            {
                update.PredictionTime = _time;
            }
            _sortedUpdates.AddRange(updates);
            _sortedBehaviours.AddRange(behaviours);
        }

        public void Add(IPredictionUpdates update)
        {
            _needsSorting = true;

            update.PredictionTime = _time;
            _sortedUpdates.Add(update);
        }

        public void Add(IEnumerable<IPredictionUpdates> updates)
        {
            _needsSorting = true;

            foreach (var update in updates)
            {
                update.PredictionTime = _time;
            }
            _sortedUpdates.AddRange(updates);
        }

        public void Remove(NetworkIdentity identity,
            out IReadOnlyList<IPredictionUpdates> removedUpdates,
            out IReadOnlyList<IPredictionBehaviour> removedBehaviours)
        {
            var updates = GetBehaviourCache<IPredictionUpdates>.GetBehaviours(identity);
            var behaviours = GetBehaviourCache<IPredictionBehaviour>.GetBehaviours(identity);

            removedUpdates = updates;
            removedBehaviours = behaviours;

            if (updates.Count == 0 && behaviours.Count == 0)
                return;

            _needsSorting = true;

            foreach (var obj in updates)
            {
                obj.PredictionTime = null;
                _sortedUpdates.Remove(obj);
            }

            foreach (var obj in behaviours)
                _sortedBehaviours.Remove(obj);
        }

        public void Remove(IEnumerable<IPredictionUpdates> updates)
        {
            _needsSorting = true;

            foreach (var obj in updates)
            {
                obj.PredictionTime = null;
                _sortedUpdates.Remove(obj);
            }
        }

        /// <summary>
        /// Checks if sorting needs to happen, will sort if it does
        /// </summary>
        private void CheckSort()
        {
            if (!_needsSorting)
                return;
            _needsSorting = false;

            _sortedUpdates.Sort(CompareValues);
            _sortedBehaviours.Sort(CompareValues);
        }

        private static int CompareValues(IPredictionUpdates x, IPredictionUpdates y)
        {
            var a = x.Order;
            var b = y.Order;
            return a.CompareTo(b);
        }
        private static int CompareValues(IPredictionBehaviour x, IPredictionBehaviour y)
        {
            var a = x.Order;
            var b = y.Order;
            return a.CompareTo(b);
        }


        public IReadOnlyList<IPredictionBehaviour> GetBehaviours()
        {
            CheckSort();
            return _sortedBehaviours;
        }
        public IReadOnlyList<IPredictionUpdates> GetUpdates()
        {
            CheckSort();
            return _sortedUpdates;
        }
    }
}
