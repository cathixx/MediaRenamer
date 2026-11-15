using System.Reflection;
using MediaRenamer.Core.Updates;

namespace MediaRenamer;

/// <summary>Version of the running application.</summary>
internal static class AppInfo
{
    private static readonly string InformationalVersion =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "snapshot";

    /// <summary>Version for display, e.g. <c>1.2.3</c>, or <c>DEVELOP</c> for local builds.</summary>
    public static string DisplayVersion => ReleaseVersion?.ToString(3) ?? "DEVELOP";

    /// <summary>Release version, or <c>null</c> for local builds (<c>0.0.0-snapshot</c>), which never update.</summary>
    public static Version? ReleaseVersion => UpdateService.ParseVersion(InformationalVersion);
}
