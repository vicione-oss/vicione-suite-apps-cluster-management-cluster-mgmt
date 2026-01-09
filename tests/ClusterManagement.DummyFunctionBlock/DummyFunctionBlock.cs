using ViciOne.Core.Runtime;
using ViciOne.Core.Runtime.Attributes;

namespace ClusterManagement;

[FunctionBlockDesign(DesignId, "28F20FFF-B58B-4A2D-B8B9-BB8DAD659072", "958C697C-C4C3-4624-8E41-92CF9F44316F")]
public sealed class DummyFunctionBlock : RuntimeFunctionBlock<DummyFunctionBlock>
{
    public const string DesignId = "E74E4B29-AC93-44C6-9248-7379726C8CC0";
    public static readonly Guid DesignGuid = Guid.Parse(DesignId);
}
