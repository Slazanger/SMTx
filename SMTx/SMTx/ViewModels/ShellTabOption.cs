using SMTx.Models;

namespace SMTx.ViewModels;

public sealed class ShellTabOption
{
    public ShellTabOption(MainShellTab tab, string displayName)
    {
        Tab = tab;
        DisplayName = displayName;
    }

    public MainShellTab Tab { get; }
    public string DisplayName { get; }
}
