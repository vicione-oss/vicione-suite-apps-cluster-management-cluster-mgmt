using ViciOne.TreeBuilder.Rules;
using ViciOne.TreeBuilder.Rules.Yaml;

namespace ClusterManagement.Tests.Tools.Factories;

public static class RulesetFactory
{
    public static Ruleset CreateTestRulesetMqtt()
    {
        // we have the embedded mqtt yaml
        var mqttRulesetYaml = ClusterManagementTools.GetEmbeddedMqttRulesetYaml();
        var temporaryFile = Path.GetTempFileName();
        File.WriteAllText(temporaryFile, mqttRulesetYaml);

        // .. but we can't deserialize it to something useful if we don't pass
        // a path here. Otherwise we won't get the datatypes, uitypes etc. and
        // the ruleset validation will fail. The TreeBuilder does not use file
        // abstractions or interfaces so it's hard to mock anything
        var ruleset = RulesDeserializer.Deserialize(temporaryFile);
        File.Delete(temporaryFile);

        return ruleset;
    }
}
