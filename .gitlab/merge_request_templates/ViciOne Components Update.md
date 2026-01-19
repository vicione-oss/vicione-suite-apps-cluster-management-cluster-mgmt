# Procedure

- [ ] Update [merge request template](.gitlab/merge_request_templates/ViciOne%20Components%20Update.md) according to dependency graph
- [ ] Update [engine host](#engine-host)
- [ ] If engine host ships a new managed engine: Update [managed engine](#managed-engine)
- If managed engine ships a new engine:
  - [ ] Update [cluster editor](#cluster-editor)
  - [ ] Update [cluster builder](#cluster-builder)
  - [ ] Update [engine](#engine)
- [ ] If any of the above components are updated: [System tests](#system-tests) must pass
- [ ] Update [data ports](#data-ports)
- [ ] Update [Anna](#anna)
- [ ] Update [FunctionBlocks](#functionblocks)

## Engine Host

[<Version>](<Link to vicione-oss/vicione/runtime/engine-host release>)

- Update version in files
  - [ ] [ClusterManagementOptionsExtensions](src/ClusterManagement.Backend/Extensions/ClusterManagementOptionsExtensions.cs)
  - [ ] [ClusterManagementOptionsExtensionsTests](tests/ClusterManagement.Backend.Tests/Extensions/ClusterManagementOptionsExtensionsTests.cs)
  - [ ] [GetEngineHostPackageInfoConsumerTests](tests/ClusterManagement.Backend.Tests/EngineHosts/Consumers/GetEngineHostPackageInfoConsumerTests.cs)
- If API has changes:
  - [ ] Copy all DTOs from [CommunicationContracts](src/ClusterManagement.Backend/EngineHosts/CommunicationContracts) into a new subdirectory `v[Version]` (e.g. for _v0.31.0_: `v0310`)
- [ ] Update supported version range in [tests](tests/ClusterManagement.Backend.Tests/EngineHosts/MqttEngineHostCommunicationFacts.cs)
- [ ] Update IDs and properties of communications in [WorkflowTranslator](src/ClusterManagement.Backend/Engines/WorkflowTranslator.cs)
- [ ] Apply changes to [CommunicationContracts](src/ClusterManagement.Backend/EngineHosts/CommunicationContracts)
- If API has changes:
  - [ ] Implement and test migration logic in [EngineHosts](src/ClusterManagement.Backend/EngineHosts)
    ```csharp
    internal static class MigrationV0310
    {
        internal static CommunicationContracts.v0310.DeployRequest Execute(CommunicationContracts.DeployRequest deployRequest) => new();
    }
    ```
  - [ ] Register migrations in [Communication](src/ClusterManagement.Backend/EngineHosts/MqttEngineHostCommunication.cs)
    ```csharp
    new Migration<IDeployRequest, CommunicationContracts.DeployRequest, CommunicationContracts.v0310.DeployRequest>(SemVersion.Parse("0.31.0", SemVersionStyles.Any), MigrationV0310.Execute),
    ```
  - [ ] Update migration [tests](tests/ClusterManagement.Backend.Tests/EngineHosts/MqttEngineHostCommunicationFacts.cs) to ensure supported version range

## Managed Engine

[<Version>](<Link to vicione-oss/vicione/runtime/managed-engine release>)

- If API has changes:
  - [ ] Copy all DTOs from [CommunicationContracts](src/ClusterManagement.Backend/Engines/CommunicationContracts) into a new subdirectory `v[Version]` (e.g. for _v0.31.0_: `v0310`)
- [ ] Update supported version range in [tests](tests/ClusterManagement.Backend.Tests/EngineHosts/MqttEngineHostCommunicationFacts.cs)
- [ ] Apply changes to [CommunicationContracts](src/ClusterManagement.Backend/Engines/CommunicationContracts)
- If API has changes:
  - [ ] Implement and test migration logic in [Engines](src/ClusterManagement.Backend/Engines)
    ```csharp
    internal static class MigrationV0310
    {
        internal static CommunicationContracts.v0310.StartParameter Execute(StartParameter startParameter) => new();
    }
    ```
  - [ ] Register migrations in [Communication](src/ClusterManagement.Backend/EngineHosts/MqttEngineHostCommunication.cs)
    ```csharp
    new Migration<IStartParameter, CommunicationContracts.StartParameter, CommunicationContracts.v0310.StartParameter>(SemVersion.Parse("0.31.0", SemVersionStyles.Any), MigrationV0310.Execute),
    ```
  - [ ] Update migration [tests](tests/ClusterManagement.Backend.Tests/EngineHosts/MqttEngineHostCommunicationFacts.cs) to ensure supported version range

## Cluster Editor

[<Version>](<Link to vicione-oss/vicione/suite-apps/cluster-management/cluster-editor release>)

- Update NuGet packages
  - [ ] `ViciOne.Ui.ClusterEditor`
  - [ ] `ViciOne.TreeBuilder`

## Cluster Builder

[<Version>](<Link to vicione-oss/vicione/suite-apps/cluster-management/cluster-builder release>)

- Update NuGet packages
  - [ ] `ViciOne.Cluster.Builder`

## Engine

[<Version>](<Link to vicione-oss/vicione/runtime/engine release>)

- Update NuGet packages
  - [ ] `ViciOne.Engine.*`
  - [ ] `ViciOne.Core.*`

## System Tests

- Execute `git clean -xfd` to remove all existing downloaded modules, functionblocks, etc.
- Start external MQTT broker
- Configure [settings](tests/ClusterManagement.Tests.Tools/ClusterManagementTools.cs) if needed
- Configure [path](src/ClusterManagement.Backend/EngineHosts/Extensions/IServiceCollectionExtensions.cs) to executable of engine host
- Optional: remove all items in [packages.json](src/ClusterManagement.Backend/Packages/packages.json) to speed up some the tests (no load/unload)

## Data Ports

[<Version>](<Link to vicione-oss/vicione/runtime-libs/dps release>)

- Update NuGet packages
  - [ ] `ViciOne.Suite.DataPort`
- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated Data Ports
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated Data Ports

## Anna

[<Version>](<Link to vicione-oss/addons/anna/anna release>)

- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated FunctionBlocks and DataPorts
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated FunctionBlocks and DataPorts

## FunctionBlocks

### General FunctionBlocks

[<Version>](<Link to vicione-oss/vicione/runtime-libs/basic-functionblocks release>)

- [ ] Update version in [tests](tests/ClusterManagement.Client.Tests/TestDataFactory.cs)
- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated FunctionBlocks
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated FunctionBlocks

### IoT driver

[<Version>](<Link to vicione-oss/vicione/runtime-libs/iot-core-driver release>)

- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated FunctionBlocks
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated FunctionBlocks

### Suite FunctionBlocks

[<Version>](<Link to vicione-oss/vicione/runtime-libs/dataport-functionblock release>)

- Update NuGet packages
  - [ ] `ViciOne.Suite.System.DataPort`
- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated FunctionBlocks
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated FunctionBlocks

### moneo FunctionBlocks

[<Version>](<Link to vicione-oss/addons/moneo/functionblocks release>)

- [ ] Update [packages.json](src/ClusterManagement.Backend/Packages/packages.json) with new/maintained/outdated FunctionBlocks
- [ ] Update [packages.edge-s.json](src/ClusterManagement.Backend/Packages/packages.edge-s.json) with new/maintained/outdated FunctionBlocks
