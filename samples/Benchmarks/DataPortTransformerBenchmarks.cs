using System.Buffers;
using BenchmarkDotNet.Attributes;
using ClusterManagement.Backend.DeployCluster;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using NSubstitute;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using DataPort = ViciOne.Cluster.Model.DataPort;

namespace Benchmarks;

[MemoryDiagnoser]
public class DataPortTransformerBenchmarks
{
    private readonly FunctionBlockDesign _dataPortFunctionBlockDesign;
    private readonly ClusterDependencyResolver _dependencyResolver;
    private readonly ISuiteRulesetProvider _rulesetProvider;

    private DataPort _dataPort = null!;

    [Params(1, 5, 10, 100)]
    public int Breadth { get; set; }
    [Params(1, 5, 10, 100)]
    public int Depth { get; set; }

    public DataPortTransformerBenchmarks()
    {
        _dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.CreateDataPortFunctionBlockDesign();

        _dependencyResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([_dataPortFunctionBlockDesign,]));

        _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
        _rulesetProvider.GetRuleset(Constants.DataPortCategory, Arg.Any<string>()).Returns(RulesetFactory.CreateTestRulesetMqtt());
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _dataPort = new() { Id = Guid.NewGuid(), DesignId = DataPortTransformer.DataPortFbDesignId.ToString(), Name = "Empty", RulesetId = "TCP", };

        var nodes = ArrayPool<IList<DataPortTreeNode>>.Shared.Rent(Breadth);
        Array.Fill(nodes, _dataPort.TreeNodes, 0, Breadth);
        for (var d = 0; d < Depth; d++)
        {
            for (var b = 0; b < Breadth; b++)
            {
                DataPortTreeNode node = new() { Id = Guid.NewGuid(), Name = $"{d}{b}", };
                nodes[b].Add(node);
                nodes[b] = node.Children;
            }
        }
        ArrayPool<IList<DataPortTreeNode>>.Shared.Return(nodes);
    }

    [Benchmark(Baseline = true)]
    public void Current()
    {
        using var builder = new ClusterBuilder(_dependencyResolver);
        DataPortTransformer.TransformToFunctionBlock(_dataPort, builder, _rulesetProvider);
    }

    [Benchmark]
    public void Experimental()
    {
        using var builder = new ClusterBuilder(_dependencyResolver);
        DataPortTransformer.TransformToFunctionBlock(_dataPort, builder, _rulesetProvider);
    }
}
