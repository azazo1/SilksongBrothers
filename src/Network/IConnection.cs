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
    /// 自身的 Peer Id.
    /// </summary>
    string? PeerId { get; }

    Action OnConnected { get; set; }
    Action<Exception> OnConnectFailed { get; set; }
    Action<Exception> OnConnectionCrashed { get; set; }

    /// <summary>
    /// 建立和服务器的连接.
    /// </summary>
    void Establish();

    /// <summary>
    /// 断开和服务器的连接.
    ///
    /// 可能需要阻塞等待所有包的发送完毕才能返回.
    /// </summary>
    void Destroy();

    /// <summary>
    /// 向指定的 peer 发送数据, 调用此方法不会产生 io 操作, 而是会将 packet 加入发送队列, 即使连接还未建立.
    /// 其在内部构建 Packet 并向服务器发送数据.
    /// </summary>
    /// <param name="packet">负载</param>
    void Send<T>(T packet) where T : Packet;

    /// <summary>
    /// 添加数据接收处理器.
    /// </summary>
    Action<Packet> AddHandler<T>(Action<T> handler) where T : Packet;

    /// <summary>
    /// 清除指定的接收处理器.
    /// 需要传入 AddHandler 的返回值, 否则无法正确清除.
    /// </summary>
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
