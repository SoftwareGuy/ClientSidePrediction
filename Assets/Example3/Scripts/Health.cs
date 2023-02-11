using System;
using Mirage;
using UnityEngine;

namespace JamesFrowen.CSP.Example3
{
    public class Health : PredictionBehaviour<Health.NetworkState>
    {
        [SerializeField] private float _startingHealth = 10;

        private void Awake()
        {
            OnPredictionSetup.AddListener(Setup);
        }

        private void Setup()
        {
            State.Health = _startingHealth;
            _clientHealth = _startingHealth;
        }

        public event Action OnHarm;
        public event Action OnDeath;

        private float _clientHealth;

        public void Harm(float value)
        {
            Debug.Assert(IsServer);

            State.Health -= value;
            CallEvents();
        }

        private void CallEvents()
        {
            OnHarm?.Invoke();
            if (State.Health < 0)
                Dead();
        }

        private void Dead()
        {
            OnDeath?.Invoke();
        }

        public override void AfterStateChanged()
        {
            if (_clientHealth != State.Health)
            {
                CallEvents();
            }
        }

        [NetworkMessage]
        public struct NetworkState
        {
            public float Health;
        }
    }
}
