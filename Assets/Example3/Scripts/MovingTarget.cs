using System.Collections.Generic;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class MovingTarget : PredictionBehaviour<MovingTarget.NetworkState>
    {
        [SerializeField] private List<Transform> _points;
        [SerializeField] private float _speed = 2;

        private int _index;

        private Vector3 Target => _points[_index].position;

        private void Awake()
        {
            transform.position = Target;
            IncrementIndex();
        }

        public override void NetworkFixedUpdate()
        {
            if (!IsServer)
                return;

            var movementLeft = _speed * PredictionTime.FixedDeltaTime;
            var positon = transform.position;
            while (movementLeft > 0)
            {
                var target = Target;
                var distance = Vector3.Distance(positon, target);
                positon = Vector3.MoveTowards(positon, target, movementLeft);

                // if we have extra movement, increment to next index
                if (movementLeft > distance)
                {
                    IncrementIndex();
                }

                movementLeft -= distance;
            }
            transform.position = positon;
        }
        public override void AfterStateChanged()
        {
            transform.position = State.Position;
        }
        public override void AfterTick()
        {
            State.Position = transform.position;
        }


        private void IncrementIndex()
        {
            _index++;
            if (_index >= _points.Count)
                _index = 0;
        }

        public struct NetworkState
        {
            public Vector3 Position;
        }
    }
}
