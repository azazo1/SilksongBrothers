using System;
using System.Threading.Tasks;
using SilksongBrothers.Network;
using SilksongBrothers.Network.Standalone;
using UnityEngine;

namespace SilksongBrothers;

// todo sync time

public class Communicator
{
    private enum CommunicatorState
    {
        Connecting,
        Connected,
        Disconnected,
        Quit,
    }

    private IConnection _connection;
    private readonly PeerRegistry _peerRegistry = new();
    private volatile CommunicatorState _state = CommunicatorState.Connecting;
    private readonly Throttler _heartBeatThrottler = new(10000);
    private long? SyncTimePending;
    public bool Alive => _state != CommunicatorState.Quit;

#pragma warning disable CS8618
    public Communicator()
#pragma warning restore CS8618
    {
        Connect();
        SyncPeerId();
    }

    private void Connect()
    {
        if (ModConfig.NetworkMode == NetworkMode.Standalone)
        {
            _connection = new StandaloneConnection();
        }
        else
        {
            throw new InvalidOperationException("Network mode is not supported");
        }

        SetupHandlers();
        Task.Run(_connection.Establish); // 放在另一个线程执行.
    }


    public void Update()
    {
        if (_state == CommunicatorState.Quit) return;

        if (_heartBeatThrottler.Tick())
        {
            _connection.Send(new HeartbeatPacket());
        }

        _connection.Update();
    }

    public void Quit()
    {
        _state = CommunicatorState.Quit;
        _connection.Send(new PeerQuitPacket());
        _connection.Destroy();
        Utils.Logger?.LogInfo("Communicator quit.");
    }

    private void Reconnect()
    {
        _state = CommunicatorState.Connecting;
        _connection.Destroy();
        Connect();
        // 有可能第一次都没连上, 因此还是需要重新发送一次需要回复的 PeerIdPacket.
        SyncPeerId();
    }

    private void SetupHandlers()
    {
        _connection.AddHandler<PeerIdPacket>(SyncPeerIdResponseHandler);
        _connection.AddHandler<PeerQuitPacket>(PeerQuitHandler);
        _connection.AddHandler<SyncTimePacket>(SyncTimeHandler);
        _connection.OnConnected += () =>
        {
            if (_state == CommunicatorState.Quit) return;
            _state = CommunicatorState.Connected;
            SyncTime();
            SilksongBrothersPlugin.SpawnPopup("Connected to server.");
        };
        _connection.OnConnectFailed += e =>
        {
            if (_state == CommunicatorState.Quit) return;
            _state = CommunicatorState.Disconnected;
            SilksongBrothersPlugin.SpawnPopup($"Failed to connect to server: {e}.", Color.red);
            Reconnect();
        };
        _connection.OnConnectionCrashed += e =>
        {
            if (_state == CommunicatorState.Quit) return;
            _state = CommunicatorState.Disconnected;
            SilksongBrothersPlugin.SpawnPopup($"Connection to server closed: {e}, reconnecting.", Color.yellow);
            Reconnect();
        };
    }

    /// <summary>
    /// 和其他 peers 同步 peer id.
    /// </summary>
    private void SyncPeerId()
    {
        _connection.Send(new PeerIdPacket(true));
    }

    /// <summary>
    /// 和服务端同步时间.
    ///
    /// 在一趟 SyncTime 来回之间发送多个 SyncTime 包可能导致时间同步紊乱.
    /// </summary>
    private void SyncTime()
    {
        var packet = new SyncTimePacket();
        SyncTimePending = packet.Time;
        _connection.Send(packet);
    }

    private void SyncTimeHandler(SyncTimePacket packet)
    {
        long delta;
        if (SyncTimePending != null)
        {
            // 一趟来回消耗的时间, 其中假设服务器响应时间同步包的时候是这趟来回时间的中点.
            delta = (long)(packet.Time - ((Utils.Time - SyncTimePending) / 2 + SyncTimePending));
        }
        else
        {
            delta = packet.Time - Utils.Time;
        }

        SyncTimePending = null;

        Utils.Logger?.LogInfo($"Sync time delta: {delta}");
        Utils.SetTimeOffset(delta);
    }

    private void SyncPeerIdResponseHandler(PeerIdPacket packet)
    {
        if (packet.SrcPeer == null) return;
        if (packet.Version.Major != Utils.Version.Major || packet.Version.Minor != Utils.Version.Minor)
        {
            Utils.Logger?.LogInfo($"Version of peer ({packet.Name}) is incompatible, quitting.");
            Quit();
            return;
        }

        if (_peerRegistry.AddPeer(packet.SrcPeer, packet.Name))
        {
            Utils.Logger?.LogInfo($"Peer discovered: {packet.Name} (id: {packet.SrcPeer})");
        }

        if (packet.NeedResponse)
        {
            _connection.Send(new PeerIdPacket(false));
        }
    }

    private void PeerQuitHandler(PeerQuitPacket packet)
    {
        if (_state == CommunicatorState.Quit) return;
        if (packet.SrcPeer == null) return;
        _peerRegistry.RemovePeer(packet.SrcPeer);
    }
}
