namespace ClusterManagement.Client.DataflowEvents.Models;

/// <summary>
/// The connector colors of the Cluster Editor by value type, so an event looks like its connector there.
/// </summary>
/// <remarks>The editor keeps its mapping internal (<c>ConnectorColor</c> in ViciOne.Ui.ClusterEditor).</remarks>
internal static class ConnectorColors
{
    private const string Bool = "rgb(5, 158, 121)";
    private const string Floating = "rgb(255, 150, 0)";
    private const string Integer = "rgb(190, 236, 121)";
    private const string Text = "rgb(90, 93, 187)";
    private const string Other = "rgb(255, 20, 147)";

    private static readonly Dictionary<string, string> s_colorByTypeName = new(StringComparer.Ordinal)
    {
        [typeof(bool).FullName!] = Bool,
        [typeof(float).FullName!] = Floating,
        [typeof(double).FullName!] = Floating,
        [typeof(decimal).FullName!] = Floating,
        [typeof(long).FullName!] = Integer,
        [typeof(int).FullName!] = Integer,
        [typeof(short).FullName!] = Integer,
        [typeof(sbyte).FullName!] = Integer,
        [typeof(ulong).FullName!] = Integer,
        [typeof(uint).FullName!] = Integer,
        [typeof(ushort).FullName!] = Integer,
        [typeof(byte).FullName!] = Integer,
        [typeof(string).FullName!] = Text,
        [typeof(char).FullName!] = Text,
    };

    /// <summary>
    /// The color for an assembly-qualified value type name.
    /// </summary>
    public static string For(string valueType)
        => s_colorByTypeName.GetValueOrDefault(ValueTypeNames.FullName(valueType), Other);
}
