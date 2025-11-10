using System;
using System.Threading.Tasks;
using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers;

public class Communicator
{
    public enum CommunicatorState
    {
        Connecting,
        Connected,
        Disconnected,
        Reconnecting,
        Quit,
    }

    private IConnection _connection;
    private readonly PeerRegistry _peerRegistry = new();
    public CommunicatorState State { get; private set; } = CommunicatorState.Connecting;
    public bool Alive => State != CommunicatorState.Quit;

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
        if (State == CommunicatorState.Quit) return;
        if (!_connection.Connected && State != CommunicatorState.Connecting &&
            State != CommunicatorState.Reconnecting)
        {
            Reconnect();
        }

        _connection.Update();
    }

    public void Quit()
    {
        State = CommunicatorState.Quit;
        _connection.Send(new PeerQuitPacket());
        _connection.Destroy();
        Utils.Logger?.LogInfo("Communicator quit.");
    }

    private void Reconnect()
    {
        State = CommunicatorState.Reconnecting;
        _connection.Destroy();
        Connect();
        _connection.Send(new PeerIdPacket(false)); // 向服务器和其他 peer 报告自身的 peer id.
    }

    private void SetupHandlers()
    {
        _connection.AddHandler<PeerIdPacket>(SyncPeerIdResponseHandler);
        _connection.AddHandler<PeerQuitPacket>(PeerQuitHandler);
        _connection.OnConnected += () =>
        {
            if (State == CommunicatorState.Quit) return;
            State = CommunicatorState.Connected;
            SilksongBrothersPlugin.SpawnPopup("Connected to server.");
        };
        _connection.OnConnectFailed += e =>
        {
            if (State == CommunicatorState.Quit) return;
            State = CommunicatorState.Disconnected;
            SilksongBrothersPlugin.SpawnPopup($"Failed to connected to server: {e}.", Color.red);
        };
    }

    /// <summary>
    /// 和其他 peers 同步 peer id.
    /// </summary>
    private void SyncPeerId()
    {
        _connection.Send(new PeerIdPacket(true));
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
        if (State == CommunicatorState.Quit) return;
        if (packet.SrcPeer == null) return;
        _peerRegistry.RemovePeer(packet.SrcPeer);
    }
}
