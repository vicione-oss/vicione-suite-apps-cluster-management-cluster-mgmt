using ViciOne.Tree.Builder.Rules;
using ViciOne.Tree.Builder.Rules.Yaml;

namespace ClusterManagement.Tests.Tools.Factories;

public static class RulesetFactory
{
    public static Ruleset CreateTestRulesetMqtt()
        => CreateTestRuleset(ClusterManagementTools.GetEmbeddedMqttRulesetYaml());

    public static Ruleset CreateTestRulesetMqttWithEnvelopeChildren()
        => CreateTestRuleset(ClusterManagementTools.GetEmbeddedMqttEnvelopeChildrenRulesetYaml());

    public static Ruleset CreateTestRulesetMqttInboundOnly()
        => CreateTestRuleset(ClusterManagementTools.GetEmbeddedMqttInboundOnlyRulesetYaml());

    public static Ruleset CreateTestRuleset(string rulesetYaml)
    {
        // RulesDeserializer resolves datatypes, uitypes and the like relative to the ruleset's own
        // path and fails validation without them, so the YAML has to reach it as a real file. The
        // tree builder takes no file abstraction, which leaves nothing to mock.
        var temporaryFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temporaryFile, rulesetYaml);
            return RulesDeserializer.Deserialize(temporaryFile);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    public static Ruleset CreateInvalidRuleset()
    {
        var ruleset = CreateTestRulesetMqtt();
        ruleset.Root = null; // a ruleset without a root node fails validation

        return ruleset;
    }
}
