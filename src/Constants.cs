using SilksongBrothers.Network;

namespace SilksongBrothers;

public abstract class Constants
{
    public const string ServerId = "[server]";
    public const ushort Port = 14455;
    public const long MaxPacketLen = 1024 * 1024 * 1;
    public const string ModId = "io.github.azazo1.silksongbrothers";
    public const string ModName = "SilksongBrothers";

    /// <summary>
    /// <see cref="IConnection.Update"/> 单次执行最大允许的时间(软性).
    /// </summary>
    public const long ConnectionUpdateMaxDuration = 5;
}
