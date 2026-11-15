using System.Reflection;
using MediaRenamer.Core.Updates;

namespace MediaRenamer;

/// <summary>Version of the running application.</summary>
internal static class AppInfo
{
    private static readonly string InformationalVersion =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "snapshot";

    /// <summary>Version for display, e.g. <c>1.2.3</c> or <c>0.0.0-snapshot</c> (without the commit hash).</summary>
    public static string DisplayVersion
    {
        get
        {
            var plus = InformationalVersion.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? InformationalVersion[..plus] : InformationalVersion;
        }
    }

    /// <summary>Release version, or <c>null</c> for local builds (<c>0.0.0-snapshot</c>), which never update.</summary>
    public static Version? ReleaseVersion => UpdateService.ParseVersion(InformationalVersion);
}
