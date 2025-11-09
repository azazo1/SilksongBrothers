
using System;
using MemoryPack;

namespace SilksongBrothers.Network;

/// <summary>
/// Communicator 和服务端的连接
/// </summary>
public interface IConnection
{
    /// <summary>
    /// 唯一标识一个同伴, 用于断线重连和自我识别.
    /// </summary>
    string PeerId { get; }

    /// <summary>
    /// 建立和服务器的连接.
    /// </summary>
    void Establish();

    /// <summary>
    /// 断开和服务器的连接.
    /// </summary>
    /// 此方法会清除服务器上此 peer 的数据.
    void Destroy();

    // 在其内部构建 Packet 并向服务器发送数据.
    /// <summary>
    /// 向指定的 peer 发送数据.
    /// </summary>
    /// <param name="obj">负载</param>
    /// <param name="dstPeer">目标 peer id</param>
    /// <returns>是否成功发送</returns>
    bool SendTo<T>(T obj, string dstPeer) where T: IMemoryPackable<T>;

    /// <summary>
    /// 向所有其他 peer 发送数据.
    /// </summary>
    /// <param name="obj">负载</param>
    /// <param name="realtime">是否是实时包, 见<see cref="Packet.RealTime"/></param>
    void Broadcast<T>(T obj, bool realtime = false) where T: IMemoryPackable<T>;

    /// <summary>
    /// 添加数据接收处理器.
    /// </summary>
    void AddHandler<T>(Action<T> handler) where T: IMemoryPackable<T>;

    /// <summary>
    /// 删除指定的数据接收处理器.
    /// </summary>
    void RemoveHandler<T>(Action<T> handler) where T: IMemoryPackable<T>;

    /// <summary>
    /// 清除指定类型的接收处理器.
    /// </summary>
    void RemoveHandlersOfType<T>() where T : IMemoryPackable<T>;

    /// <summary>
    /// 清除所有数据接收处理器.
    /// </summary>
    void RemoveHandlers();
}
