using JamesFrowen.CSP.Example3.Inputs;

namespace JamesFrowen.CSP.Example3
{
    public abstract class PlayerInputBase<TInput, TState> : PredictionBehaviour<TInput, TState> where TState : unmanaged
    {
        private InputActions _inputActions;
        protected InputActions.PlayerActions _playerInput;

        protected TInput _networkInput;

        protected virtual void Awake()
        {
            _inputActions = new InputActions();
            _inputActions.Player.Enable();
            _playerInput = _inputActions.Player;
        }

        protected virtual void OnDestroy()
        {
            _inputActions.Dispose();
            _inputActions = null;
        }

        public override TInput GetInput()
        {
            var temp = _networkInput;

            // clear Accumulated inputs;
            _networkInput = default;

            return temp;
        }
    }
}
