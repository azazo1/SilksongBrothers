using System;
using MemoryPack;
using SilksongBrothers.Network;
using UnityEngine;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SilksongBrothers;

[MemoryPackable]
[MemoryPackUnion(0, typeof(PeerQuitPacket))]
[MemoryPackUnion(1, typeof(PeerIdPacket))]
[MemoryPackUnion(2, typeof(SyncTimePacket))]
[MemoryPackUnion(3, typeof(HeartbeatPacket))]
[MemoryPackUnion(4, typeof(HornetPositionPacket))]
[MemoryPackUnion(5, typeof(HornetAnimationPacket))]
[MemoryPackUnion(6, typeof(EnemyPosPacket))]
[MemoryPackUnion(7, typeof(EnemyHealthPacket))]
[MemoryPackUnion(8, typeof(EnemyFsmPacket))]
[MemoryPackUnion(9, typeof(AttackRequestPacket))]
[MemoryPackUnion(10, typeof(HostPeerPacket))]
public abstract partial class Packet
{
    /// <summary>
    /// Packet 时的创建时间戳.
    /// </summary>
    [MemoryPackOrder(0)] public long Time = Utils.ServerTime;

    /// <summary>
    /// 是否为实时包.
    /// </summary>
    /// 如果为 true, 那么此包在过期后接收到将被丢弃.
    [MemoryPackOrder(1)] public bool IsRealtime;

    /// <summary>
    /// peer id, 目标发送到的 peers.
    /// </summary>
    /// 当为广播时, 为 null.
    /// 如果需要给服务端发送消息, 提供服务端的 peer id 即可.
    [MemoryPackOrder(2)] public string[]? DstPeer;

    /// <summary>
    /// peer id, 来源 peer.
    /// </summary>
    [MemoryPackOrder(3)] public string SrcPeer;

    protected Packet()
    {
        if (ModConfig.NetworkMode == NetworkMode.Standalone)
        {
            SrcPeer = ModConfig.StandalonePeerId;
        }
        else
        {
            throw new Exception("Unsupported network mode");
        }
    }
}

/// <summary>
/// 由服务器下发给各个 peer, 告知某个 peer 的退出.
/// </summary>
[MemoryPackable]
public partial class PeerQuitPacket : Packet
{
    public string QuitPeer;
}

/// <summary>
/// 向其他 peer 和服务端发送自身的 peer id (放于 SrcPeer),
/// 并携带自身 mod 版本号和玩家姓名.
///
/// 此消息可用广播的方式发送.
///
/// 服务端会根据特定连接第一次发送的此类型消息来标记一个连接的 PeerId.
/// </summary>
[MemoryPackable]
public partial class PeerIdPacket : Packet
{
    /// <summary>
    /// 玩家名.
    /// </summary>
    public string Name;

    /// <summary>
    /// 版本号, 用于检查当前用户和其他用户是否符合版本号要求.
    /// 只要具有相同的 major 和 minor 版本号, 那么 peers 之间就能兼容.
    /// </summary>
    public Version Version = Utils.Version;

    /// <summary>
    /// 标记此包是否需要回复, 防止无尽的递归回复.
    /// </summary>
    public bool NeedResponse;

    public PeerIdPacket(bool needResponse)
    {
        Name = ModConfig.PlayerName;
        NeedResponse = needResponse;
    }
}

/// <summary>
/// 时间同步包, 客户端向服务端发送自身的 unix 时间, 之后服务器向客户端发送同类型的包以返回服务端的 unix 时间,
/// 都储存在 <see cref="Packet.Time"/> 之中.
/// <see cref="Packet.DstPeer"/> 和 <see cref="Packet.SrcPeer"/> 将会被忽视.
///
/// 此包不推荐进行预发送, 即不推荐连接建立之前就将此包放入发送缓冲区, 可能导致较大的误差.
/// </summary>
[MemoryPackable]
public partial class SyncTimePacket : Packet
{
    public SyncTimePacket()
    {
        Time = Utils.Time;
    }
}

/// <summary>
/// 心跳包, 客户端向服务端发送, 其 DstPeer 会被忽略, 服务端不会将心跳包发送给其他 Peer.
/// 服务器在一段时间内没收到客户端的数据包的时候会发送一个心跳包测试客户端.
/// </summary>
[MemoryPackable]
public partial class HeartbeatPacket : Packet;

[MemoryPackable]
public partial class HornetPositionPacket : Packet
{
    public string Scene;
    public float PosX;
    public float PosY;

    /// <summary>
    /// 角色的朝向.
    /// </summary>
    public float ScaleX;

    public float VelocityX;
    public float VelocityY;

    public HornetPositionPacket()
    {
        IsRealtime = true;
    }
}

[MemoryPackable]
public partial class HornetAnimationPacket : Packet
{
    public string CrestName;
    public string ClipName;

    public HornetAnimationPacket()
    {
        IsRealtime = true;
    }
}

[MemoryPackable]
public partial class EnemyPosPacket : Packet
{
    public string Id;
    public Vector3 Pos;
    public string Scene;
    public string Clip;
    public bool FacingLeft;

    public EnemyPosPacket()
    {
        IsRealtime = true;
    }
}

[MemoryPackable]
public partial class EnemyHealthPacket : Packet
{
    public string Id;
    public string Scene;
    public bool InCombat;
    public int Hp;
    public bool IsDead;

    public EnemyHealthPacket()
    {
        IsRealtime = true;
    }
}

[MemoryPackable]
public partial class EnemyFsmPacket : Packet
{
    public string Id;
    public string Scene;
    public string StateName;

    public EnemyFsmPacket()
    {
        IsRealtime = true;
    }
}

[MemoryPackable]
public partial class AttackRequestPacket : Packet
{
    public string EnemyId;
    public SimpleHit Hit;
    public string Scene;

    [MemoryPackable]
    public partial class SimpleHit
    {
        public int DamageDealt;
        public float Direction;
        public float MagnitudeMult;
        public int AttackType;
        public int NailElement;
        public bool NonLethal;
        public bool Critical;
        public bool CanWeakHit;
        public float Multiplier;
        public int DamageScalingLevel;
        public int SpecialType;
        public bool IsHeroDamage;
    }
}

/// <summary>
/// 客户端向服务端发送此包查询哪个 peer 是 host,
/// 服务端返回新创建的此包并附上 host 的 peer id 给请求的客户端,
/// 此情况下服务器不会把请求包发给其他客户端.
///
/// 服务器可在内部随时间切换 host peer, 此时服务器会主动广播此包以告知 host 切换.
///
/// <see cref="Packet.SrcPeer"/> 和 <see cref="Packet.DstPeer"/> 字段将被忽视.
/// </summary>
[MemoryPackable]
public partial class HostPeerPacket : Packet
{
    /// <summary>
    /// 由服务端设置并发送给客户端.
    /// </summary>
    public string? Host;
}
