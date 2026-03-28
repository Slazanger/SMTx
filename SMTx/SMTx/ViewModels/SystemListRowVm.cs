namespace SMTx.ViewModels;

public sealed class SystemListRowVm
{
    public SystemListRowVm(string name, string secLabel, string secIndicatorBrush)
    {
        Name = name;
        SecLabel = secLabel;
        SecIndicatorBrush = secIndicatorBrush;
    }

    public string Name { get; }
    public string SecLabel { get; }
    public string SecIndicatorBrush { get; }
}
