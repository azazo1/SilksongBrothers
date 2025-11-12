using System;
using System.Collections.Generic;

namespace SilksongBrothers;

public class Peer(string id, string name)
{
    public string Id { get; set; } = id;

    public string Name { get; set; } = name;
}

public static class PeerRegistry
{
    /// <summary>
    /// 新 peer 加入.
    /// </summary>
    private static volatile Action<Peer> _onPeerAdded = _ => { };

    /// <summary>
    /// peer 名称更新, 回调为新的 Peer 和旧名字.
    /// </summary>
    private static volatile Action<(Peer, string)> _onPeerRenamed = _ => { };

    /// <summary>
    /// peer 被去除.
    /// </summary>
    private static volatile Action<Peer> _onPeerRemoved = _ => { };

    /// <summary>
    /// peer id => Peer
    /// </summary>
    private static readonly Dictionary<string, Peer> Peers = [];

    public static void AddPeer(string id, string name)
    {
        if (Peers.TryGetValue(id, out var peer))
        {
            var oldName = peer.Name;
            peer.Name = name;
            Peers[id] = peer;
            _onPeerRenamed.Invoke((peer, oldName));
        }

        peer = new Peer(id, name);
        Peers[id] = peer;
        _onPeerAdded.Invoke(peer);
    }

    public static void RemovePeer(string id)
    {
        if (!Peers.TryGetValue(id, out var peer)) return;
        _onPeerRemoved.Invoke(peer);
        Peers.Remove(id);
    }

    public static Peer? Query(string id)
    {
        Peers.TryGetValue(id, out var peer);
        return peer;
    }

    public static void AddPeerAddedHandler(Action<Peer> handler)
    {
        _onPeerAdded += handler;
    }

    public static void AddPeerRenamedHandler(Action<(Peer, string)> handler)
    {
        _onPeerRenamed += handler;
    }

    public static void AddPeerRemovedHandler(Action<Peer> handler)
    {
        _onPeerRemoved += handler;
    }

    public static void RemovePeerAddedHandler(Action<Peer> handler)
    {
        _onPeerAdded -= handler;
    }

    public static void RemovePeerUpdatedHandler(Action<(Peer, string)> handler)
    {
        _onPeerRenamed -= handler;
    }

    public static void RemovePeerRemovedHandler(Action<Peer> handler)
    {
        _onPeerRemoved -= handler;
    }
}
