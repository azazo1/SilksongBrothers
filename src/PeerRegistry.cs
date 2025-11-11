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
    private static Action<Peer> OnPeerAdded { get; set; } = _ => { };

    /// <summary>
    /// peer 名称更新.
    /// </summary>
    private static Action<Peer> OnPeerUpdated { get; set; } = _ => { };

    /// <summary>
    /// peer 被去除.
    /// </summary>
    private static Action<Peer> OnPeerRemoved { get; set; } = _ => { };

    /// <summary>
    /// peer id => Peer
    /// </summary>
    private static readonly Dictionary<string, Peer> Peers = [];

    public static bool AddPeer(string id, string name)
    {
        if (Peers.TryGetValue(id, out var peer))
        {
            peer.Name = name;
            Peers[id] = peer;
            OnPeerUpdated.Invoke(peer);
            return false;
        }

        peer = new Peer(id, name);
        Peers[id] = peer;
        OnPeerAdded.Invoke(peer);
        return true;
    }

    public static void RemovePeer(string id)
    {
        Peers.Remove(id);
    }

    public static Peer? Query(string id)
    {
        Peers.TryGetValue(id, out var peer);
        return peer;
    }

    public static void AddPeerAddedHandler(Action<Peer> handler)
    {
        OnPeerAdded += handler;
    }

    public static void AddPeerUpdatedHandler(Action<Peer> handler)
    {
        OnPeerUpdated += handler;
    }

    public static void AddPeerRemovedHandler(Action<Peer> handler)
    {
        OnPeerRemoved += handler;
    }

    public static void RemovePeerAddedHandler(Action<Peer> handler)
    {
        OnPeerAdded -= handler;
    }

    public static void RemovePeerUpdatedHandler(Action<Peer> handler)
    {
        OnPeerUpdated -= handler;
    }

    public static void RemovePeerRemovedHandler(Action<Peer> handler)
    {
        OnPeerRemoved -= handler;
    }
}
