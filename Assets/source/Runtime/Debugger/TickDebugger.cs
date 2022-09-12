using System;
using Mirage;

namespace JamesFrowen.CSP.Debugging
{
    public class TickDebugger : NetworkBehaviour
    {
        //public int ClientDelay;

        private TickRunner tickRunner;

        private ClientTickRunner ClientRunner => (ClientTickRunner)tickRunner;

        private double latestClientTime;
        private int clientTick;
        private int serverTick;
        private ExponentialMovingAverage diff = new ExponentialMovingAverage(10);
        private TickDebuggerOutput gui;

        private void Awake()
        {
            Identity.OnStartClient.AddListener(OnStartClient);
            Identity.OnStartServer.AddListener(OnStartServer);
            gui = GetComponent<TickDebuggerOutput>();
        }
        private void Update()
        {
            tickRunner.OnUpdate();

            gui.IsServer = IsServer;
            gui.IsClient = IsClient;

            gui.ClientTick = clientTick;
            gui.ServerTick = serverTick;
            gui.Diff = diff.Var;

            if (IsClient)
            {
                gui.ClientTimeScale = ClientRunner.TimeScale;
#if DEBUG
                gui.ClientDelayInTicks = ClientRunner.Debug_DelayInTicks;
                (var average, var stdDev) = ClientRunner.Debug_RTT.GetAverageAndStandardDeviation();
                gui.ClientRTT = average;
                gui.ClientJitter = stdDev;
#endif
            }
        }

        private void OnStartServer()
        {
            tickRunner = new TickRunner();
            tickRunner.OnTick += ServerTick;
        }

        private void ServerTick(int tick)
        {
            serverTick = tick;
            ToClient_StateMessage(tick, latestClientTime);
        }

        private void OnStartClient()
        {
            tickRunner = new ClientTickRunner(
                movingAverageCount: 50 * 5// 5 seconds
                );
            tickRunner.OnTick += ClientTick;
            NetworkTime.PingInterval = 0;
        }

        private void ClientTick(int tick)
        {
            clientTick = tick;
            ToServer_InputMessage(tick, tickRunner.UnscaledTime);
        }

        [ClientRpc(channel = Channel.Unreliable)]
        public void ToClient_StateMessage(int tick, double clientTime)
        {
            tickRunner.OnMessage(tick, clientTime);
            serverTick = tick;
            diff.Add(clientTick - serverTick);
        }


        [ServerRpc(channel = Channel.Unreliable)]
        public void ToServer_InputMessage(int tick, double clientTime)
        {
            clientTick = tick;
            diff.Add(clientTick - serverTick);
            latestClientTime = Math.Max(latestClientTime, clientTime);
        }
    }
}
