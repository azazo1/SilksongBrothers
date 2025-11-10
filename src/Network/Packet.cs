using MemoryPack;

namespace SilksongBrothers.Network;

[MemoryPackable]
[MemoryPackUnion(0, typeof(PeerQuitPacket))]
[MemoryPackUnion(1, typeof(PeerIdPacket))]
public abstract partial class Packet
{
    /// <summary>
    /// Packet 时的创建时间戳.
    /// </summary>
    [MemoryPackOrder(0)] public long Time = Utils.Time;

    /// <summary>
    /// 是否为实时包.
    /// </summary>
    /// 如果为 true, 那么此包在过期后接收到将被丢弃.
    [MemoryPackOrder(1)] public bool IsRealtime = false;

    /// <summary>
    /// peer id, 目标发送到的 peers.
    /// </summary>
    /// 当为广播时, 为 null.
    /// 如果需要给服务端发送消息, 提供服务端的 peer id 即可.
    [MemoryPackOrder(2)] public string[]? DstPeer;

    /// <summary>
    /// peer id, 来源 peer.
    /// </summary>
    /// 服务器下发 packet 时会重新写入 SrcPeer 为其来源连接对应的 peer id.
    ///
    /// 客户端发送时除了 PeerIdPacket 之外可以不填写.
    [MemoryPackOrder(3)] public string? SrcPeer;
}

[MemoryPackable]
public partial class PeerQuitPacket : Packet;

/// <summary>
/// 向其他 peer 和服务端发送自身的 peer id.
/// </summary>
///
/// 此消息可用广播的方式发送.
///
/// 服务端会根据特定连接第一次发送的此类型消息来标记一个连接的 PeerId.
[MemoryPackable]
public partial class PeerIdPacket : Packet
{
    /// <summary>
    /// 玩家名.
    /// </summary>
    public string Name;

    /// <summary>
    /// 标记此包是否需要回复, 防止无尽的递归回复.
    /// </summary>
    public bool NeedResponse;

    public PeerIdPacket(bool needResponse)
    {
        if (ModConfig.NetworkMode == NetworkMode.Standalone)
        {
            SrcPeer = ModConfig.StandalonePeerId;
        }

        Name = ModConfig.PlayerName;
        NeedResponse = needResponse;
    }
}
