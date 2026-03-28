using Microsoft.Extensions.Configuration;

namespace SMTx.Eve;

/// <summary>Set by each executable head before Avalonia initializes the main UI.</summary>
public static class EveConfigurationAccessor
{
    public static Func<IConfiguration?> Provider { get; set; } = () => null;
}
