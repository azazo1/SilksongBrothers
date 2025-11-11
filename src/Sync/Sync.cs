using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers.Sync;

/// <summary>
/// 同步游戏内容接口.
/// </summary>
public abstract class Sync : MonoBehaviour
{
    private float _timeRemainToTrigger;
    protected IConnection? _connection;

    /// <summary>
    /// <see cref="FixedTrigger"/> 期望的调用频率.
    /// </summary>
    protected abstract float TriggerFrequency { get; }

    /// <summary>
    /// 将 Sync 绑定到连接上.
    ///
    /// 当连接断开, Sync 自动失效.
    /// </summary>
    /// <param name="connection">目标连接</param>
    public virtual void Bind(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Sync 解绑.
    /// </summary>
    public abstract void Unbind();

    protected virtual void Update()
    {
        if (_connection?.Connected != true) return;
        _timeRemainToTrigger -= Time.unscaledDeltaTime;
        while (_timeRemainToTrigger < 0)
        {
            FixedTrigger();
            _timeRemainToTrigger += 1.0f / TriggerFrequency;
        }
    }

    /// <summary>
    /// 以 <see cref="FixedTrigger"/> 固定的频率触发.
    /// </summary>
    protected abstract void FixedTrigger();
}
