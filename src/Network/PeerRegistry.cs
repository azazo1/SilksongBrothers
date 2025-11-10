using System.Collections.Generic;

namespace SilksongBrothers.Network;

public class Peer(string id, string name)
{
    public string? Scene;

    public string Id { get; set; } = id;

    public string Name { get; set; } = name;

    // position
    // facing
    // state (animation 状态)
}

public class PeerRegistry
{
    /// <summary>
    /// peer id => Peer
    /// </summary>
    private readonly Dictionary<string, Peer> _peers = [];

    public void AddPeer(string id, string name)
    {
        if (_peers.TryGetValue(id, out var peer))
        {
            peer.Name = name;
            _peers[id] = peer;
        }
        else
        {
            _peers[id] = new Peer(id, name);
        }
    }

    public void RemovePeer(string id)
    {
        _peers.Remove(id);
    }
}
