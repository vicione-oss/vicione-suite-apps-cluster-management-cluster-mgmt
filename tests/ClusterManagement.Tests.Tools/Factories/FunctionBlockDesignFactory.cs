using System.Drawing;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Tests.Tools.Factories;

public static class FunctionBlockDesignFactory
{
    public static class Settings
    {
        public const string Settings1FB1 = nameof(Settings1FB1);
        public const string Settings1FB2 = nameof(Settings1FB2);
        public const string Settings2FB2 = nameof(Settings2FB2);
        public const string Settings3FB2 = nameof(Settings3FB2);
        public const string Settings1FB3 = nameof(Settings1FB3);
        public const string Settings2FB3 = nameof(Settings2FB3);
        public const string Settings3FB3 = nameof(Settings3FB3);
        public const string Settings4FB3 = nameof(Settings4FB3);
        public const string Settings1FB4 = nameof(Settings1FB4);
        public const string Settings2FB4 = nameof(Settings2FB4);
        public const string Settings1I3O2 = nameof(Settings1I3O2);
        public const string Settings2I3O2 = nameof(Settings2I3O2);
        public const string Settings3I3O2 = nameof(Settings3I3O2);

        public static readonly Guid Settings1FB1Id = Guid.NewGuid();
        public static readonly Guid Settings1FB2Id = Guid.NewGuid();
        public static readonly Guid Settings2FB2Id = Guid.NewGuid();
        public static readonly Guid Settings3FB2Id = Guid.NewGuid();
        public static readonly Guid Settings1FB3Id = Guid.NewGuid();
        public static readonly Guid Settings2FB3Id = Guid.NewGuid();
        public static readonly Guid Settings3FB3Id = Guid.NewGuid();
        public static readonly Guid Settings4FB3Id = Guid.NewGuid();
        public static readonly Guid Settings1FB4Id = Guid.NewGuid();
        public static readonly Guid Settings2FB4Id = Guid.NewGuid();
        public static readonly Guid Settings1I3O2Id = Guid.NewGuid();
        public static readonly Guid Settings2I3O2Id = Guid.NewGuid();
        public static readonly Guid Settings3I3O2Id = Guid.NewGuid();
    }

    public static class PersistentVariables
    {
        public const string PersistentVariable1FB1 = nameof(PersistentVariable1FB1);
        public const string PersistentVariable2FB1 = nameof(PersistentVariable2FB1);
        public const string PersistentVariable3FB1 = nameof(PersistentVariable3FB1);
        public const string PersistentVariable1FB2 = nameof(PersistentVariable1FB2);
        public const string PersistentVariable1FB3 = nameof(PersistentVariable1FB3);
        public const string PersistentVariable2FB3 = nameof(PersistentVariable2FB3);
        public const string PersistentVariable3FB3 = nameof(PersistentVariable3FB3);
        public const string PersistentVariable4FB3 = nameof(PersistentVariable4FB3);
        public const string PersistentVariable1FB4 = nameof(PersistentVariable1FB4);
        public const string PersistentVariable2FB4 = nameof(PersistentVariable2FB4);

        public static readonly Guid PersistentVariable1FB1Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable2FB1Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable3FB1Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable1FB2Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable1FB3Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable2FB3Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable3FB3Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable4FB3Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable1FB4Id = Guid.NewGuid();
        public static readonly Guid PersistentVariable2FB4Id = Guid.NewGuid();
    }

    public static class Connectors
    {
        public const string ConnectorFB1I1 = "ConnectorFB1Input1";
        public const string ConnectorFB1O1 = "ConnectorFB1Output1";
        public const string ConnectorFB2I1 = "ConnectorFB2Input1";
        public const string ConnectorFB2I2 = "ConnectorFB2Input2";
        public const string ConnectorFB2I3 = "ConnectorFB2Input3";
        public const string ConnectorFB2I4 = "ConnectorFB2Input4";
        public const string ConnectorFB2O1 = "ConnectorFB2Output1";
        public const string ConnectorFB3I1 = "ConnectorFB3Input1";
        public const string ConnectorFB3O1 = "ConnectorFB3Output1";
        public const string ConnectorFB3O2 = "ConnectorFB3Output2";
        public const string ConnectorFB4I1 = "ConnectorFB4Input1";
        public const string ConnectorFB4I2 = "ConnectorFB4Input2";
        public const string ConnectorFB4O1 = "ConnectorFB4Output1";
        public const string ConnectorFB4O2 = "ConnectorFB4Output2";
        public const string ConnectorI3O2I1 = "ConnectorI3O2Input1";
        public const string ConnectorI3O2I2 = "ConnectorI3O2Input2";
        public const string ConnectorI3O2I3 = "ConnectorI3O2Input3";
        public const string ConnectorI3O2O1 = "ConnectorI3O2Output1";
        public const string ConnectorI3O2O2 = "ConnectorI3O2Output2";

        public static readonly Guid ConnectorFB1I1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB1O1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB2I1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB2I2Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB2I3Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB2I4Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB2O1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB3I1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB3O1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB3O2Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB4I1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB4I2Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB4O1Id = Guid.NewGuid();
        public static readonly Guid ConnectorFB4O2Id = Guid.NewGuid();
        public static readonly Guid ConnectorI3O2I1Id = Guid.NewGuid();
        public static readonly Guid ConnectorI3O2I2Id = Guid.NewGuid();
        public static readonly Guid ConnectorI3O2I3Id = Guid.NewGuid();
        public static readonly Guid ConnectorI3O2O1Id = Guid.NewGuid();
        public static readonly Guid ConnectorI3O2O2Id = Guid.NewGuid();
    }

    public static FunctionBlockDesign Create1I1O1(string designName)
    {
        var design = FunctionBlockDesignBuilder.Create(name: designName);
        design.AddSetting<DateTime>(Settings.Settings1FB1Id, Settings.Settings1FB1);
        design.AddVariable<long>(PersistentVariables.PersistentVariable1FB1Id, PersistentVariables.PersistentVariable1FB1);
        design.AddVariable<double>(PersistentVariables.PersistentVariable2FB1Id, PersistentVariables.PersistentVariable2FB1);
        design.AddVariable<bool>(PersistentVariables.PersistentVariable3FB1Id, PersistentVariables.PersistentVariable3FB1);
        design.AddInput<long>(Connectors.ConnectorFB1I1Id, Connectors.ConnectorFB1I1);
        design.AddOutput<long>(Connectors.ConnectorFB1O1Id, Connectors.ConnectorFB1O1);

        return design;
    }

    public static FunctionBlockDesign Create2I4O1(string designName)
    {
        var design = FunctionBlockDesignBuilder.Create(name: designName);
        design.AddSetting<string>(Settings.Settings1FB2Id, Settings.Settings1FB2);
        design.AddSetting<double>(Settings.Settings2FB2Id, Settings.Settings2FB2);
        design.AddSetting<Point>(Settings.Settings3FB2Id, Settings.Settings3FB2);
        design.AddVariable<bool>(PersistentVariables.PersistentVariable1FB2Id, PersistentVariables.PersistentVariable1FB2);
        design.AddInput<bool>(Connectors.ConnectorFB2I1Id, Connectors.ConnectorFB2I1);
        design.AddInput<bool>(Connectors.ConnectorFB2I2Id, Connectors.ConnectorFB2I2);
        design.AddInput<long>(Connectors.ConnectorFB2I3Id, Connectors.ConnectorFB2I3);
        design.AddInput<double>(Connectors.ConnectorFB2I4Id, Connectors.ConnectorFB2I4);
        design.AddOutput<double>(Connectors.ConnectorFB2O1Id, Connectors.ConnectorFB2O1);

        return design;
    }

    public static FunctionBlockDesign Create4I2O2(string designName)
    {
        var design = FunctionBlockDesignBuilder.Create(name: designName);
        design.AddSetting<long>(Settings.Settings1FB4Id, Settings.Settings1FB4);
        design.AddSetting<bool>(Settings.Settings2FB4Id, Settings.Settings2FB4);
        design.AddVariable<int>(PersistentVariables.PersistentVariable1FB4Id, PersistentVariables.PersistentVariable1FB4);
        design.AddVariable<double>(PersistentVariables.PersistentVariable2FB4Id, PersistentVariables.PersistentVariable2FB4);
        design.AddInput<bool>(Connectors.ConnectorFB4I1Id, Connectors.ConnectorFB4I1);
        design.AddInput<bool>(Connectors.ConnectorFB4I2Id, Connectors.ConnectorFB4I2);
        design.AddOutput<bool>(Connectors.ConnectorFB4O1Id, Connectors.ConnectorFB4O1);
        design.AddOutput<long>(Connectors.ConnectorFB4O2Id, Connectors.ConnectorFB4O2);

        return design;
    }

    internal static FunctionBlockDesign Create3I3O2(string designName)
    {
        var design = FunctionBlockDesignBuilder.Create(name: designName);
        design.AddSetting<DateTime>(Settings.Settings1FB3Id, Settings.Settings1FB3);
        design.AddSetting<string>(Settings.Settings2FB3Id, Settings.Settings2FB3);
        design.AddSetting<string>(Settings.Settings3FB3Id, Settings.Settings3FB3);
        design.AddSetting<bool>(Settings.Settings4FB3Id, Settings.Settings4FB3);
        design.AddVariable<long>(PersistentVariables.PersistentVariable1FB3Id, PersistentVariables.PersistentVariable1FB3);
        design.AddVariable<double>(PersistentVariables.PersistentVariable2FB3Id, PersistentVariables.PersistentVariable2FB3);
        design.AddVariable<bool>(PersistentVariables.PersistentVariable3FB3Id, PersistentVariables.PersistentVariable3FB3);
        design.AddVariable<long>(PersistentVariables.PersistentVariable4FB3Id, PersistentVariables.PersistentVariable4FB3);
        design.AddInput<long>(Connectors.ConnectorFB3I1Id, Connectors.ConnectorFB3I1);
        design.AddOutput<long>(Connectors.ConnectorFB3O1Id, Connectors.ConnectorFB3O1);
        design.AddOutput<long>(Connectors.ConnectorFB3O2Id, Connectors.ConnectorFB3O2);

        return design;
    }

    internal static FunctionBlockDesign CreateI3O2(string designName)
    {
        var functionBlockDesignI3O2 = FunctionBlockDesignBuilder.Create(name: designName);
        functionBlockDesignI3O2.AddSetting<string>(Settings.Settings1I3O2Id, Settings.Settings1I3O2);
        functionBlockDesignI3O2.AddSetting<double>(Settings.Settings2I3O2Id, Settings.Settings2I3O2);
        functionBlockDesignI3O2.AddSetting<Point>(Settings.Settings3I3O2Id, Settings.Settings3I3O2);
        functionBlockDesignI3O2.AddInput<bool>(Connectors.ConnectorI3O2I1Id, Connectors.ConnectorI3O2I1);
        functionBlockDesignI3O2.AddInput<bool>(Connectors.ConnectorI3O2I2Id, Connectors.ConnectorI3O2I2);
        functionBlockDesignI3O2.AddInput<long>(Connectors.ConnectorI3O2I3Id, Connectors.ConnectorI3O2I3);
        functionBlockDesignI3O2.AddOutput<double>(Connectors.ConnectorI3O2O1Id, Connectors.ConnectorI3O2O1);
        functionBlockDesignI3O2.AddOutput<double>(Connectors.ConnectorI3O2O2Id, Connectors.ConnectorI3O2O2);

        return functionBlockDesignI3O2;
    }
}
