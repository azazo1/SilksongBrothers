using System.Collections.Generic;

namespace SilksongBrothers;

public class Peer(string id, string name)
{
    public string? Scene;

    public string Id { get; set; } = id;

    public string Name { get; set; } = name;

    // hornet:
    // position
    // facing
    // state (animation 状态)
    // enemy:
    // ...
}

public class PeerRegistry
{
    /// <summary>
    /// peer id => Peer
    /// </summary>
    private readonly Dictionary<string, Peer> _peers = [];

    public bool AddPeer(string id, string name)
    {
        if (_peers.TryGetValue(id, out var peer))
        {
            peer.Name = name;
            _peers[id] = peer;
            return false;
        }
        else
        {
            _peers[id] = new Peer(id, name);
            return true;
        }
    }

    public void RemovePeer(string id)
    {
        _peers.Remove(id);
    }
}
