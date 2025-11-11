using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilksongBrothers.Network;
using SilksongBrothers.Network.Standalone;
using SilksongBrothers.Sync;
using UnityEngine;

namespace SilksongBrothers;

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
    private volatile CommunicatorState _state = CommunicatorState.Connecting;
    private readonly Throttler _heartBeatThrottler = new(10000);
    private long? _syncTimePending;
    private readonly List<BaseSync> _syncs = [];
    public bool Alive => _state != CommunicatorState.Quit;

    public Communicator()
    {
        // syncs 的添加要放在 Connect 之前, 因为 SetupHandlers 在 Connect 中.
        _syncs.Add(SilksongBrothersPlugin.Instance!.gameObject.AddComponent<HornetSync>());
        Connect();
        SyncPeerId();
        PeerRegistry.AddPeerAddedHandler(OnPeerJoin);
        PeerRegistry.AddPeerRemovedHandler(OnPeerQuit);
    }

    private static void OnPeerQuit(Peer peer)
    {
        Utils.Logger?.LogInfo($"Peer quit: {peer.Name}({peer.Id})");
        SilksongBrothersPlugin.SpawnPopup($"Peer quit: {peer.Name}");
    }

    private static void OnPeerJoin(Peer peer)
    {
        Utils.Logger?.LogInfo($"Peer joined: {peer.Name}({peer.Id})");
        SilksongBrothersPlugin.SpawnPopup($"Peer joined: {peer.Name}");
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

    private void DestroyConnection()
    {
        _connection.Destroy();

        foreach (var sync in _syncs)
        {
            sync.Unbind();
        }
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
        DestroyConnection();
        PeerRegistry.RemovePeerAddedHandler(OnPeerJoin);
        PeerRegistry.RemovePeerRemovedHandler(OnPeerQuit);

        foreach (var sync in _syncs)
        {
            UnityEngine.Object.Destroy(sync);
        }

        Utils.Logger?.LogInfo("Communicator quit.");
    }

    ~Communicator()
    {
        Quit();
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

        foreach (var sync in _syncs)
        {
            sync.Bind(_connection);
        }

        _connection.OnConnected += () =>
        {
            if (_state == CommunicatorState.Quit) return;
            _state = CommunicatorState.Connected;
            SyncTime();
            Utils.Logger?.LogInfo($"You: {ModConfig.PlayerName}({new PeerIdPacket(false).SrcPeer ?? ""})");
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
        _syncTimePending = packet.Time;
        _connection.Send(packet);
    }

    private void SyncTimeHandler(SyncTimePacket packet)
    {
        long delta;
        if (_syncTimePending != null)
        {
            // 一趟来回消耗的时间, 其中假设服务器响应时间同步包的时候是这趟来回时间的中点.
            delta = (long)(packet.Time - ((Utils.Time - _syncTimePending) / 2 + _syncTimePending));
        }
        else
        {
            delta = packet.Time - Utils.Time;
        }

        _syncTimePending = null;

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

        PeerRegistry.AddPeer(packet.SrcPeer, packet.Name);

        if (packet.NeedResponse)
        {
            _connection.Send(new PeerIdPacket(false));
        }
    }

    private void PeerQuitHandler(PeerQuitPacket packet)
    {
        if (_state == CommunicatorState.Quit) return;
        if (packet.SrcPeer == null) return;
        PeerRegistry.RemovePeer(packet.SrcPeer);
    }
}
