# Changelog

## 2.2.0 - Unreleased

### Changed

- Updated .NET to version `10.0.8`
- `ViciOne.Suite.Sdk` packages, update to version `2.0.1`
- `ViciOne.Ui.ClusterEditor` package, update to `1.1.0`
- Added authorization requirement to have full access to control panels

## 2.1.0 - 2026-05-12

### Changed

- `ViciOne.Ui.ClusterEditor` package, update to `1.0.0-ci2515372500`
- `ViciOne.Driver.IoTCore`, updated to version `2.0.1`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `4.10.0`
- `SaveClusterDialog`, all fields get disabled while saving in progress

### Fixed

- Thread-safety race conditions in event subscriptions when rapidly changing tree selections
- 'NullReferenceException' and 'InvalidOperationException' in 'DataflowEventBroker' by adding SafeInvokeHandler and ReaderWriterLockSlim protection
- Creates two new cluster versions on save in cluster editor

## 2.0.0 - 2026-04-16

### Added

- Pass DataPort name to DataPort communication
- Pass DataPort DesignId to DataPort communication
- Added a 'Get Packages' button to DebugEngine Page
- Added `ResourcesDownloaded` event, published when all backend resource downloads have completed
- Added `GetResourcesDownloadStatusRequest` query to retrieve whether backend resources have finished downloading

### Changed

- Improve async cancellation handling on client controls
- Prevented local reloading after the cluster was saved
- Updated .NET to version `10.0.6`
- `AspNetCore.SassCompiler` package, update version to `1.99.0`
- `MQTTnet` package, update to version `5.1.0.1559`
- `System.IO.Abstractions`, updated to version `22.1.1`
- `ViciOne.Cluster.Builder` package, update version to `1.0.0`
- `ViciOne.Suite.Sdk` packages, update to version `2.0.0`
- `ViciOne.Ui.ClusterEditor` package, update to `1.0.0-ci2457140352`
- `ViciOne.Ui.Design`, update to `2.0.3`
- `ViciOne.Ui.Localization` package, update version to `3.3.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `4.7.0`
- `ViciOne.TreeBuilder` package, update version to `1.1.1`
- Update Engine Host to `1.0.0`
- Update Managed Engine to `1.0.0`
- Update `ViciOne.Core.Dataflow.DataModel.Generation` to `1.0.0`
- Update `ViciOne.Core.Runtime` to `1.0.0`
- Update `ViciOne.Suite.DataPort.Http.Client` to `0.30.0`
- Update `ViciOne.Suite.DataPort.InfluxDb` to `0.30.0`
- Update `ViciOne.Suite.DataPort.MySql` to `0.30.0`
- Update `ViciOne.Suite.DataPort.Mqtt` to `0.30.0`
- Update `ViciOne.Suite.DataPort.OpcUa.Client` to `0.30.0`
- Update `ViciOne.Suite.DataPort.OpcUa.Server` to `0.30.0`
- Update `ViciOne.Suite.DataPort.PostgreSql` to `0.30.0`
- Update `ViciOne.Suite.DataPort.Sqlite` to `0.30.0`
- Update `ViciOne.Suite.DataPort.SqlServer` to `0.30.0`
- Update `ifm.Anna.FunctionBlocks` to `1.0.0`
- Update `ViciOne.Suite.DataPort.Anna` to `1.0.0`
- Update `ViciOne.Analysis` to `1.0.0`
- Update `ViciOne.Constant` to `1.0.0`
- Update `ViciOne.Converter` to `1.0.0`
- Update `ViciOne.FlowControl` to `1.0.0`
- Update `ViciOne.Logic` to `1.0.0`
- Update `ViciOne.Math` to `1.0.0`
- Update `ViciOne.Time` to `1.0.0`
- Update `ViciOne.Driver.IoTCore` to `2.0.0`
- Update `ViciOne.Suite.System.DataPort` to `1.0.0`
- Update `ifm.moneo.Counter.Pieces` to `1.0.0`
- Update `ifm.moneo.Counter.Runtime` to `1.0.0`
- Update `ifm.moneo.DataFormatter` to `1.0.0`
- Update `ifm.moneo.Math.DecimalToIntegral` to `1.0.0`

### Fixed

- Transfer all involved DataPort nodes to external communication
- Design loader continues loading when individual packages fail to download
- Cluster editor and packages settings now wait for backend resources to finish loading before opening
- Can now uninstall packages that failed to load

## 1.4.1 - 2026-02-16

### Added

- Add update available icon to `PackagesControlPanel`

## 1.4.0 - 2026-02-13

### Added

- Allow manual management of cluster packages

### Changed

- Enabled virtual scrolling for `DataflowEventsPage`
- Moved commands `AddIodd`, `DeleteIodd` to namespace `Iodds.Commands`
- Moved events `IoddChanged`, `IoddError` and `AutoIoddDownloadChanged` to namespace `Iodds.Events`
- Moved all Iodd related requests to namespace `Iodds.Requests`
- `ViciOne.Driver.IoTCore`, updated to version `1.21.1`

### Fixed

- Ignore dataflow events without content
- Log missing UserProperties in dataflow event instead of throwing
- Remove option `OverwritePackagesFiles`

## 1.3.0 - 2026-01-09

### Added

- Use of `ViciOne.Ui.Design`
- Localization support for `DataflowEventsPage`
- Add Client Id to MQTT
- `DeployCluster` command, added `DeletePreviousVersionOnSuccess` that defaults to `false`

### Changed

- `DeployCluster` removes created cluster version on rollback
- Prevent deployments without FunctionBlocks
- Update Engine Host to `0.30.0`
- `AspNetCore.SassCompiler` package, update version to `1.94.2`
- `MassTransit.EntityFrameworkCore` package, update version to `8.5.7`
- `.NET` packages, update version to `9.0.11`
- `System.IO.Abstractions`, updated to version `22.1.0`
- `ViciOne.Suite.Sdk` packages, update version to `1.1.0`
- `ViciOne.Ui.Design`, update to `1.1.1`
- `ViciOne.Ui.ClusterEditor`, update to `0.17.0`
- `ViciOne.Ui.Localization` package, update version to `2.35.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `3.10.0`
- `ViciOne.Ui.TreeEditor` package, update to version `0.12.0`

## 1.2.7 - 2025-11-14

### Added

- Add display of errors when get/reset saga state actions fail to `ClusterManagementPage`

### Changed

- Delete obsolete MQTT retain messages on cluster update
- Clear existing deployment states on startup for standalone systems

## 1.2.6 - 2025-11-06

### Changed

- `ViciOne.Driver.IoTCore`, updated to version `1.21.0`

## 1.2.5 - 2025-10-28

### Added

- Reconnect loop for event MQTT client

### Changed

- Ignore additional directories in iodd zip files
- `ViciOne.Driver.IoTCore`, updated to version `1.20.1`

## 1.2.4 - 2025-10-23

### Changed

- `ViciOne.Driver.IoTCore`, updated to version `1.19.0`

### Fixed

- `DeployCluster` removes created cluster version on rollback
- Fix HTTP client issue on `ResetSaga` and `GetSaga` Actions

## 1.2.3 - 2025-10-08

### Fixed

- Resolve startup error caused by malformed package format
- Prevent deployment of an engine host if the executable is missing

## 1.2.2 - 2025-09-19

### Changed

- `ViciOne.Driver.IoTCore`, updated to version `1.18.3`
- Overhauled `SaveClusterDialog` content area

### Fixed

- Consider all additional connectors to get source data type for links

## 1.2.1 - 2025-09-12

### Added

- Show a banner if uploaded IODD archive is wrong

### Changed

- `ViciOne.Driver.IoTCore`, updated to version `1.18.2`
- Update Engine Host to `0.28.0`
- Adjust quality of service levels appropriate to the use case of each MQTT communication

## 1.2.0 - 2025-09-08

### Added

- Add `ClusterUpdateRejected` event, published on deploy commands when cluster is in deployment process
- Add `Duration`,`State`, `Reason` properties to `ClusterUpdateFailed` event
- Add `InitializeFailed` property to `DeployClusterState`
- Add `GetDeployClusterStateRequest` request, to get the state of running cluster deployments

### Changed

- Use always latest supported version of Engine Host
- Make `EngineHostVersion` setting optional to allow newer versions of engine hosts
- Only load packages that are configured, not all from the file system
- Adjust quality of service levels of MQTT communication
- `System.IO.Abstractions`, updated to version `22.0.15`
- `ViciOne.Driver.IoTCore`, updated to version `1.18.1`
- `ViciOne.Analysis`, updated to version `0.33.0`
- `ViciOne.Constant`, updated to version `0.33.0`
- `ViciOne.Converter`, updated to version `0.33.0`
- `ViciOne.FlowControl`, updated to version `0.33.0`
- `ViciOne.Logic`, updated to version `0.33.0`
- `ViciOne.Math`, updated to version `0.33.0`
- `ViciOne.Time`, updated to version `0.33.0`
- `ViciOne.Suite.DataPort.Anna` dataport, updated to version `0.37.0`
- `ifm.Anna.FunctionBlocks`, updated to version `0.37.0`

### Fixed

- Packages will now always be updated when upgrading to a new module version

### Removed

- Remove `EngineHostExecutablePathOverride` from settings
- Remove `EngineHostExecutable` from settings

## 1.1.0 - 2025-08-01

### Changed

- `ViciOne.Suite.DataPort.Anna` dataport, updated to version `0.36.0`
- `ifm.Anna.FunctionBlocks`, updated to version `0.36.0`
- `ViciOne.Driver.IoTCore`, updated to version `1.17.0`

## 1.0.0 - 2025-07-25

### Changed

- Limit max pending MQTT messages in `EngineHost` and `ManagedEngine`
- Restrict Management and Editor controls to full access
- Renamed options property `DataPortSet` to `PackageSet`
- Log to journald with custom fields instead of syslog in `EngineHost` and `ManagedEngine`
- `MassTransit.EntityFrameworkCore` package, update version to `8.5.1`
- `ViciOne.Suite.Sdk` packages, update version to `1.0.0`
- `ViciOne.Ui.Localization` package, update to `2.31.0`
- `IoT Core` dependency, update to `1.16.1`
- `ViciOne.Suite.DataPort.Anna` dataport, updated to version `0.35.0`

### Removed

- Removed `FunctionBlockSet` property from options
- Removed `IoddArchiveName` property from options
- Removed `IoddLoader` background service

### Fixed

- Ensure accessible permissions of the engine host under Linux

## 0.36.0 - 2025-07-15

### Changed

- `IoT Core`, update to `1.16.0`
- `ViciOne.Ui.ClusterEditor`, update to `0.16.0`
- `EngineHost`, updated to version `0.27.0`
- `ViciOne.Suite.DataPort.Anna` dataport, updated to version `0.34.0`
- `ViciOne.Suite.DataPorts` package, update version to `0.28.0`
- `moneo FunctionBlocks`, update to `0.33.0`

### Removed

- Remove PackageApi, PackageUser and PackagePassword from module settings

### Fixed

- Fix parameter handover in InMemoryPackageStore

### Changed

- `MassTransit.EntityFrameworkCore` package, update version to `8.5.1`
- `.NET` packages, update version to `9.0.7`
- `ViciOne.Suite.Sdk` packages, update version to `1.0.0.1760749`
- `ViciOne.Ui.Localization` package, update version to `2.31.0`

## 0.35.0 - 2025-07-09

### Added

- Provide default cluster version constant
- Provide journal tile for engine host

### Changed

- Deploy active cluster on startup of standalone instances
- `ViciOne.Suite.Sdk` package, update version to `0.31.0`

### Removed

- `ViciOne.Ui.Blazor.Components` package, removed direct reference

### Fixed

- Prohibit deleting default cluster

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.30.5.1743591`

## 0.34.5 - 2025-06-19

### Added

- Add OPC UA data ports to edge S packages

### Changed

- Load latest FunctionBlock package from filesystem
- `AspNetCore.SassCompiler` package, update version to `1.89.3`
- `BenchmarkDotNet` package, update version to `0.15.2`
- `MassTransit.EntityFrameworkCore` package, update version to `8.5.0`
- `.NET` packages, update version to `9.0.6`
- `ViciOne.Suite.Sdk` packages, update version to `0.30.4`
- `ViciOne.Ui.Localization` package, update version to `2.29.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `3.6.0`

### Fixed

- Ensure localizations of satellite references for culture 'de' get published

## 0.34.4 - 2025-06-11

### Fixed

- Insert correct MQTT DataPort version to packages.edge-s.json

## 0.34.3 - 2025-06-05

### Fixed

- Fix cluster editor settings persistence
- Do not use `--no-warn` option when stopping engine hosts

## 0.34.2 - 2025-06-04

### Fixed

- Saving general settings for the cluster editor now works
- "Central Broker"-tag is now protected as intended

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.30.3`

## 0.34.1 - 2025-05-30

### Added

- Stop engine hosts and engines on restore and reset

### Changed

- `IoT Core`, update to `1.15.1`
- `ViciOne.Suite.Sdk` packages, update version to `0.30.2`
- `ViciOne.Ui.Blazor.Components` package, update version to `3.8.1`
- `ViciOne.Ui.Localization` package, update version to `2.27.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `3.5.0`

## 0.34.0 - 2025-05-27

### Added

- Provide `IIoddStore` interface to interact with IODD files
- Provide `IoddService` to monitor and maintain `IIoddStore`
- Add `IoddControlPanel` to browse, upload, download and delete IODD files

### Changed

- `AspNetCore.SassCompiler` package, update version to `1.89.0`
- `.NET` packages, update version to `9.0.5`
- `ViciOne.Suite.Sdk` packages, update version to `0.30.0`
- `ViciOne.Ui.Localization` package, update version to `2.26.0`
- `EngineHost`, updated to version `0.26.0`
- `ManagedEngine`, updated to version `0.61.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.15.0`
- `ViciOne.Cluster.Builder` package, update version to `0.10.0`
- `ViciOne.Core` packages, update version to `0.48.0`
- `Core FunctionBlocks`, update to `0.32.0`
- `Suite FunctionBlocks`, update to `0.14.0`
- `moneo FunctionBlocks`, update to `0.32.0`
- `IoT Core`, update to `1.15.0`
- `Anna`, updated to version `0.33.0`

### Removed

- Removed `IoddProvider`

## 0.33.5 - 2025-05-13

### Fixed

- Skip downloading IODD definitions if they are already available

## 0.33.4 - 2025-05-08

### Changed

- Updated Core FunctionBlocks to `0.31.1`

## 0.33.3 - 2025-05-06

### Fixed

- Return no engine host states if directory is missing

## 0.33.2 - 2025-05-06

### Removed

- Removed `ISuiteRulesetStore` interface

## 0.33.1 - 2025-05-05

- `ViciOne.Driver.IoTCore` FunctionBlock, update version to `1.14.0`

## 0.33.0 - 2025-04-30

### Added

- Support generic variants of package files

### Changed

- Reduce engine database write operations
- Change `EngineLifetimeService` class, publish events on startup
- Unify packages lists from functionblocks and data ports to just packages

- `AspNetCore.SassCompiler` package, update version to `1.87.0`
- `.NET` packages, update version to `9.0.4`
- `System.IO.Abstractions` packages, update to version `22.0.14`
- `ViciOne.Cluster.Builder` package, update version to `0.9.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.14.0`
- `ViciOne.Suite.Sdk` packages, update version to `0.29.0`
- `ViciOne.Ui.Localization` package, update version to `2.25.0`

### Fixed

- `GetFunctionBlocks` logs an error when the dependency is a data port
- Prevent runtime types from loading in packages load context
- Hide system data port package

## 0.32.2 - 2025-03-21

### Added

- Add options flag `HideClusterEditorNavTile`

## 0.32.1 - 2025-03-20

### Changed

- `ViciOne.Driver.IoTCore` FunctionBlock, update version to `1.13.3`

## 0.32.0 - 2025-03-19

### Changed

- `ShouldRun` flag on engine deployment is not set through engine lifetime service
- Hold FunctionBlock design instances in InMemoryDesignStore
- Load cluster dependencies in one context

- `AspNetCore.SassCompiler` package, update version to `1.86.0`
- `MassTransit.EntityFrameworkCore` package, update version to `8.4.0`
- `.NET` packages, update version to `9.0.3`
- `System.IO.Abstractions` packages, update to version `22.0.12`
- `ViciOne.Suite.Sdk` packages, update version to `0.28.0`
- `ViciOne.Ui.Localization` package, update version to `2.22.1`

### Removed

- Remove ClientDesignProvider (legacy unmaintained WASM part)
- Remove GetDesignsConsumer

## 0.31.3 - 2025-03-07

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.27.3`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.13.0`
- `ViciOne.Driver.IoTCore` FunctionBlock, update version to `1.13.2`

## 0.31.2 - 2025-03-07

### Added

- `ViciOne.ManagedEngine.Serialization` package, added version `0.59.0` temporary till next EngineHost update

### Changed

- Fix usage of additional assemblies on cluster serialization

## 0.31.1 - 2025-03-06

### Changed

- Updated Core FunctionBlocks to `0.31.0`

## 0.31.0 - 2025-03-06

### Changed

- `ViciOne.Cluster.Builder` package, update version to `0.8.0`
- `ViciOne.Core.Dataflow.DataModel.Generation` package, update version to `0.47.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.12.0`
- `EngineHost`, updated to version `0.25.0`

### Fixed

- Support custom data types
- Fix cluster serializer initialization on seeding the default cluster

## 0.30.0 - 2025-02-27

### Changed

- `AspNetCore.SassCompiler` package, update version to `1.85.0`
- `MQTTnet` package, update to version `5.0.1.1416`
- `ViciOne.Suite.Sdk` packages, update version to `0.27.1`
- `ViciOne.Driver.IoTCore` package, update version to `1.13.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.11.0`
- `ViciOne.Ui.Localization` package, update version to `2.21.0`

### Fixed

- Fixed spellings in cluster editor settings

### Removed

- `ClusterManagementOptions` class, removed properties `EngineHostsDirectory`. `EngineHostVersionsDirectory`, `EngineHostArchitecture`, `FunctionBlockDirectory`, `IoddDirectory` and `SnapshotDirectory`

## 0.29.1 - 2025-02-14

### Changed

- `MassTransit.EntityFrameworkCore` package, update version to `8.3.6`
- `.NET` packages, update version to `9.0.2`
- `ViciOne.Driver.IoTCore` package, update version to `1.12.0`
- `ViciOne.Suite.DataPort.Anna` dataport, updated to version `0.29.0`
- `ViciOne.Cluster.Builder` package, update version to `0.7.0`
- `ViciOne.Suite.Sdk` packages, update version to `0.26.4`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.10.0`
- `ViciOne.Ui.Localization` package, update version to `2.18.0`
- `ACX.FunctionBlocks.BooleanToDouble` functionblock, added version `0.29.0` to edge-s setup
- `EngineHost`, updated to version `0.24.0`

## 0.29.0 - 2025-02-07

### Changed

- `EngineHost`, updated to version `0.23.0`
- Use compression on message content of `GetActiveClusterResponse`, `CommitClusterChange` and `UpdateDeployments`

- `ViciOne.Cluster.Builder` package, update version to `0.6.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.9.0`
- `ViciOne.Ui.Localization` package, update version to `2.14.0`

## 0.28.0 - 2025-02-05

### Added

- Add optional `FunctionBlockSet` configuration to `ClusterManagementOptions`
- Add optional `DataPortSet` configuration to `ClusterManagementOptions`
- Add FunctionBlock set 'edge-s'
- Add DataPort set 'edge-s'

### Changed

- Increase upload file size on Cluster import to 50Mb
- Allow compressed cluster json to be imported as zip file
- `GetClusterResponse` class, replaced property `ClusterJson` with `CompressedCluster` to contain compressed byte array instead json string
- `ClusterSerializer` class, added methods - `Compress` and `Decompress`

- `.NET` packages, update version to `9.0.1`
- `AspNetCore.SassCompiler` package, update version to `1.83.4`
- `MassTransit.EntityFrameworkCore` package, update version to `8.3.5`
- `System.IO.Abstractions` package, update version to `21.3.1`
- `ViciOne.Suite.Sdk` packages, update version to `0.26.0`
- `ViciOne.Cluster.Builder` package, update version to `0.5.0.1535707-ci`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.9.0.1545981`
- `ViciOne.Ui.Localization` package, update version to `2.13.0`

### Fixed

- Fixed initial display of FunctionBlocks that were already imported in the cluster
- Fixed restore of ValueType for `DataPortTreeNodes` linked to connectors on Cluster import
- Fixed support of multiple `yamls` for same DataPort dependency
- Fixed event handler leak in `LoadFunctionBlockDialog`

## 0.27.0 - 2025-01-13

### Changed

- `CMS.Anna.FunctionBlocks` FunctionBlock, update version to `0.27.0`
- `ClusterManagement.Backend` module, update to use `CrossSourcePoolingStrategy`

### Fixed

- `CommitClusterChangeConsumer` class, update change event to use incremented version

## 0.26.0 - 2024-12-20

### Changed

- `.NET` packages, update version to `9.0.0`
- `AspNetCore.SassCompiler` package, update version to `1.83.0`
- `MassTransit.EntityFrameworkCore` package, update version to `8.3.4`
- `Semver` package, update version to `3.0.0`
- `System.IO.Abstractions` package, update version to `21.1.7`
- `ViciOne.Suite.Sdk` packages, update version to `0.25.0`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.8.0`
- `ViciOne.Ui.Localization` package, update version to `2.0.0`
- `ViciOne.Suite.System.DataPort` package, update version to `0.12.0`
- `ViciOne.TreeBuilder` package, update version to `0.5.0`
- `ViciOne.Cluster.Builder` package, update version to `0.5.0`
- `EngineHost`, update version to `0.22.0`

## 0.25.0 - 2024-12-02

### Changed

- `ViciOne.Ui.ClusterEditor` package, update version to `0.7.0`
- `ViciOne.Suite.Sdk` packages, update version to `0.20.0`

## 0.24.0 - 2024-11-19

### Changed

- `AspNetCore.SassCompiler` package, update version to `1.80.6`
- `System.IO.Abstractions` package, update version to `21.1.3`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.6.0`
- `ViciOne.Suite.Sdk` packages, update version to `0.19.0`

### Removed

- Remove `DesignKind` from `ClusterManagement.Public`

## 0.23.0 - 2024-11-06

### Added

- Add functionblock `ACX.FunctionBlocks.ConstantString`

### Changed

- Change ModuleId to `ViciOne.Suite.ClusterManagement`
- `ViciOne.Ui.ClusterEditor` package, update version to `0.5.0`
- `ViciOne.Suite.DataPorts` package, update version to `0.22.0`
- `Core FunctionBlocks` package, update version to `0.27.0`
- `ViciOne.Driver.IoTCore` package, update version to `1.9.0`

### Fixed

- Respect DataPortTreeNodes not translated to FunctionBlock connectors

## 0.22.0 - 2024-10-29

### Added

- Add InfluxDB data port

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.18.0`
- `ViciOne.Suite.DataPorts` package, update version to `0.20.0`
- `Core FunctionBlocks` package, update version to `0.26.0`
- `ViciOne.TreeBuilder` package, update version to `0.3.0`

### Removed

- Remove AWS data port
- Remove Azure data port

## 0.21.0 - 2024-10-24

### Changed

- Add default values to metadata options
- `ViciOne.Driver.IoTCore` package, update version to `1.8.3`

## 0.20.0 - 2024-10-23

### Added

- Add AWS data port
- Download Iodd definitions with required parameter `IoddDirectory` and `IoddArchiveName` (optional)
- Control panel for cluster editor

### Changed

- `ViciOne.Ui.ClusterEditor` package, update version to `0.4.0` (`ViciOne.Ui.TreeEditor 0.7.4`)
- Support engine host `0.21.0`
- Support managed engine `0.55.0`
- `Core FunctionBlocks` package, update version to `0.23.0`
- `Suite FunctionBlocks` package, update version to `0.11.0`
- `ViciOne.Driver.IoTCore` package, update version to `1.8.1`
- `ViciOne.Suite.DataPort` package, update version to `0.18.0`

## 0.19.0 - 2024-10-17

### Changed

- Reworked `ClusterDbContext` to store only pointers on active clusters
- `ViciOne.Ui.ClusterEditor` to 0.4.0.1381181 (ViciOne.Ui.TreeEditor 0.7.4)

## 0.18.0 - 2024-10-14

### Added

- Add initial module metadata

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.17.0`
- `Core FunctionBlocks` package, update version to `0.20.0`

### Fixed

- Fix query all dataflow events independent of container depth

## 0.17.0 - 2024-10-01

### Changed

- `ViciOne.Suite.Sdk` packages, update version to `0.16.0` (Merge ClientModules)
- `ViciOne.Ui.ClusterEditor` package, update version to `0.3.0`

## 0.16.1 - 2024-09-24

### Changed

- Improve FunctionBlock-Design loading

## 0.16.0 - 2024-09-17

- Initial Release
