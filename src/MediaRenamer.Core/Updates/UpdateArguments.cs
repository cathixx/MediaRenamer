using System.Globalization;

namespace MediaRenamer.Core.Updates;

/// <summary>
/// Command line options with which the previous version starts the downloaded new executable, so it completes the
/// update (UP-4): <c>--complete-update=&lt;target&gt; --previous-process=&lt;id&gt;</c>, followed by the original
/// arguments.
/// </summary>
public static class UpdateArguments
{
    private const string TargetOption = "--complete-update=";
    private const string ProcessOption = "--previous-process=";

    /// <summary>Arguments for the new executable.</summary>
    public static IReadOnlyList<string> Create(string target, int previousProcessId, IEnumerable<string> arguments) =>
        [
            TargetOption + target,
            ProcessOption + previousProcessId.ToString(CultureInfo.InvariantCulture),
            .. arguments.Where(a => !IsUpdateOption(a)),
        ];

    /// <summary>
    /// Returns whether the arguments ask to complete an update, with the target executable, the process to wait for and
    /// the original arguments.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> arguments, out string target, out int? previousProcessId,
        out IReadOnlyList<string> originalArguments)
    {
        target = arguments.FirstOrDefault(a => a.StartsWith(TargetOption, StringComparison.Ordinal))?[TargetOption.Length..]
                 ?? string.Empty;
        var process = arguments.FirstOrDefault(a => a.StartsWith(ProcessOption, StringComparison.Ordinal));
        previousProcessId = process is not null
                            && int.TryParse(process.AsSpan(ProcessOption.Length), NumberStyles.None,
                                CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
        originalArguments = arguments.Where(a => !IsUpdateOption(a)).ToList();
        return target.Length > 0;
    }

    private static bool IsUpdateOption(string argument) =>
        argument.StartsWith(TargetOption, StringComparison.Ordinal)
        || argument.StartsWith(ProcessOption, StringComparison.Ordinal);
}
