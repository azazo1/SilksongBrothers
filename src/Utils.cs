using System;
using System.Reflection;

namespace SilksongBrothers;

using PeerId = string;

public abstract class Utils
{
    private static Version? _version;

    public static Version Version => _version ??= Assembly.GetExecutingAssembly().GetName().Version;

    public static PeerId GeneratePeerId() => Guid.NewGuid().ToString();
}
