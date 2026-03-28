using EVEStandard.Enumerations;

namespace SMTx.Eve.Connectors.Options;

public sealed class EveEsiOptions
{
    public const string SectionName = "EveEsi";

    /// <summary>Per CCP user-agent guidance; include app name and contact.</summary>
    public string UserAgent { get; set; } = "SMTx/1.0 (EVE companion; contact: unknown)";

    public DataSource DataSource { get; set; } = DataSource.Tranquility;

    public CompatibilityDate CompatibilityDate { get; set; } = CompatibilityDate.v2025_12_16;

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
