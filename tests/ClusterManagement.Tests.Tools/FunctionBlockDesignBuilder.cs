using ClusterManagement.Backend.DeployCluster;
using ViciOne.Core.Contracts.DataModel;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Core.Dataflow.DataModel.Editors;
using ViciOne.Core.Dataflow.Pooling;
using ViciOne.Suite.System.Contracts;

namespace ClusterManagement.Tests.Tools;

public static class FunctionBlockDesignBuilder
{
    private static PoolingFactory PoolingFactory => new([]);

    public static FunctionBlockDesign Create(Guid? id = default, string? name = default)
    {
        FunctionBlockDesignEditor editor = new(PoolingFactory);
        editor.Design.Id = id ?? Guid.NewGuid();
        editor.SetName(name ?? nameof(FunctionBlockDesign));
        foreach (var runMode in Enum.GetValues<RunMode>())
            editor.AddValidRunMode(runMode);
        return editor.Design;
    }

    public static FunctionBlockDesign CreateDataPortFunctionBlockDesign()
    {
        var dataPortFunctionBlockDesign = Create(DataPortTransformer.DataPortFbDesignId, "DataPort");
        dataPortFunctionBlockDesign.AddSetting<List<NodeLink>>(DataPortTransformer.NodeLinksSettingId);
        dataPortFunctionBlockDesign.AddSetting<Dictionary<Guid, OutputConfiguration>>(DataPortTransformer.OutputConfigurationsSettingId);
        dataPortFunctionBlockDesign.AddSetting<Dictionary<Guid, ITransferMode>>(DataPortTransformer.TransferModesSettingId);

        return dataPortFunctionBlockDesign;
    }

    public static SettingDesign<T> AddSetting<T>(this FunctionBlockDesign design, Guid? id = default, string? name = default)
    {
        id ??= Guid.NewGuid();
        SettingDesignEditor<T> editor = new(name ?? $"Setting{design.Settings.Count}", PoolingFactory);
        editor.Setting.Id = id.Value;
        new FunctionBlockDesignEditor(design).AddSetting(editor);
        return editor.Setting;
    }

    public static PersistentVariableDesign<T> AddVariable<T>(this FunctionBlockDesign design, Guid? id = default, string? name = default)
    {
        id ??= Guid.NewGuid();
        PersistentVariableDesignEditor<T> editor = new(name ?? $"Variable{design.PersistentVariables.Count}");
        editor.PersistentVariable.Id = id.Value;
        new FunctionBlockDesignEditor(design).AddPersistentVariable(editor);
        return editor.PersistentVariable;
    }

    public static ConnectorDesignInput<T> AddInput<T>(this FunctionBlockDesign design, Guid? id = default, string? name = default)
    {
        id ??= Guid.NewGuid();
        ConnectorDesignInputEditor<T> editor = new(name ?? $"Input{design.ProcessDataInputs.Count}", PoolingFactory);
        editor.Design.Id = id.Value;
        new FunctionBlockDesignEditor(design).AddProcessDataInput(editor);
        return editor.Design;
    }

    public static ConnectorDesignOutput<T> AddOutput<T>(this FunctionBlockDesign design, Guid? id = default, string? name = default)
    {
        id ??= Guid.NewGuid();
        ConnectorDesignOutputEditor<T> editor = new(name ?? $"Output{design.ProcessDataOutputs.Count}");
        editor.Design.Id = id.Value;
        new FunctionBlockDesignEditor(design).AddProcessDataOutput(editor);
        return editor.Design;
    }
}
