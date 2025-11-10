using System;
using MemoryPack;

namespace SilksongBrothers.Network;

/// <summary>
/// Communicator 和服务端的连接
/// </summary>
public interface IConnection
{
    bool Connected { get; }

    /// <summary>
    /// 建立和服务器的连接.
    /// </summary>
    void Establish();

    /// <summary>
    /// 断开和服务器的连接.
    /// </summary>
    /// 断开连接之后仍然可以创建新实例重连,
    /// 在 <see cref="Communicator"/> 中发送 <see cref="PeerQuitPacket"/> 才会删除服务器上此客户端的内容.
    void Destroy();

    /// <summary>
    /// 向指定的 peer 发送数据.
    /// </summary>
    /// 其在内部构建 Packet 并向服务器发送数据.
    /// <param name="packet">负载</param>
    /// <param name="dstPeers">目标 peer id, 可指定多个, 当为 null 时为广播.</param>
    /// <param name="realtime">是否是实时包, 见<see cref="Packet.IsRealtime"/></param>
    void Send<T>(T packet, string[]? dstPeers, bool realtime = false) where T : Packet;

    /// <summary>
    /// 添加数据接收处理器.
    /// </summary>
    Action<Packet> AddHandler<T>(Action<T> handler) where T : Packet;

    /// <summary>
    /// 清除指定的接收处理器.
    /// </summary>
    /// 需要传入 AddHandler 的返回值, 否则无法正确清除.
    void RemoveHandler<T>(Action<Packet> handler) where T : Packet;

    /// <summary>
    /// 清除指定类型的接收处理器.
    /// </summary>
    void RemoveHandlersOfType<T>() where T : Packet;

    /// <summary>
    /// 清除所有数据接收处理器.
    /// </summary>
    void RemoveHandlers();

    /// <summary>
    /// 在主线程中调用, 用于处理接收到的 Packet 并分发到 Handlers 中.
    /// </summary>
    void Update();
}
