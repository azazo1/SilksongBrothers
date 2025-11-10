using System;
using SilksongBrothers.Network;

namespace SilksongBrothers;

public class Communicator
{
    private IConnection _connection;
    private readonly PeerRegistry _peerRegistry = new();

#pragma warning disable CS8618
    public Communicator()
#pragma warning restore CS8618
    {
        Connect();
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

        _connection.Establish();
        SetupHandlers();
    }

    public void Update()
    {
        if (!_connection.Connected)
        {
            Reconnect();
        }

        _connection.Update();
    }

    public void Quit()
    {
        _connection.Send(new PeerQuitPacket(), null);
        _connection.Destroy();
    }

    private void Reconnect()
    {
        _connection.Destroy();
        Connect();
        _connection.Send(new PeerIdPacket(false), null); // 向服务器和其他 peer 报告自身的 peer id.
    }

    private void SetupHandlers()
    {
        _connection.AddHandler<PeerIdPacket>(SyncPeerIdResponseHandler);
        _connection.AddHandler<PeerQuitPacket>(PeerQuitHandler);
    }

    /// <summary>
    /// 和其他 peers 同步 peer id.
    /// </summary>
    public void SyncPeerId()
    {
        _connection.Send(new PeerIdPacket(true), null);
    }

    private void SyncPeerIdResponseHandler(PeerIdPacket packet)
    {
        if (packet.SrcPeer == null) return;
        _peerRegistry.AddPeer(packet.SrcPeer, packet.Name);
        if (packet.NeedResponse)
        {
            _connection.Send(new PeerIdPacket(false), null);
        }
    }

    private void PeerQuitHandler(PeerQuitPacket packet)
    {
        if (packet.SrcPeer == null) return;
        _peerRegistry.RemovePeer(packet.SrcPeer);
    }
}
