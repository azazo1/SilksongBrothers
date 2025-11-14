using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers.Sync;

/// <summary>
/// 同步游戏内容接口.
/// </summary>
public abstract class BaseSync : MonoBehaviour
{
    private float _timeRemainToTrigger;
    protected IConnection? Connection;

    /// <summary>
    /// <see cref="FixedTrigger"/> 期望的调用频率.
    /// </summary>
    protected abstract float TriggerFrequency { get; }

    /// <summary>
    /// 将 Sync 绑定到连接上.
    ///
    /// 当连接断开, Sync 自动失效(<see cref="Unbind"/> 被调用), 可以再次调用 Bind 使 Sync 重新生效.
    /// </summary>
    /// <param name="connection">目标连接</param>
    public virtual void Bind(IConnection connection)
    {
        Connection = connection;
    }

    /// <summary>
    /// Sync 解绑.
    /// </summary>
    public virtual void Unbind()
    {
        Connection = null;
    }

    protected virtual void Update()
    {
        if (Connection?.Connected != true) return;
        _timeRemainToTrigger -= Time.unscaledDeltaTime;
        if (_timeRemainToTrigger >= 0) return;
        FixedTrigger();
        _timeRemainToTrigger = 1.0f / TriggerFrequency;
        // 以下方法可能出现爆发式发送, 不好.
        // while (_timeRemainToTrigger < 0)
        // {
        //     FixedTrigger();
        //     _timeRemainToTrigger += 1.0f / TriggerFrequency;
        // }
    }

    /// <summary>
    /// 以小于等于指定频率 <see cref="TriggerFrequency"/> 触发.
    /// </summary>
    protected abstract void FixedTrigger();
}
