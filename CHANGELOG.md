# Changelog

## 3.0.0 - Unreleased

### Added

- `Public`
  - `ClusterSerializer` class, added asynchronous compress methods
  - `ClusterError.PublishFailedClusterIdMismatch`, a commit was rejected because the cluster has another id than the committed cluster
  - `ClusterInfo.Deployed`, when the active version was deployed
  - `OpcUaClientConnection` contract and `ConnectionType.OpcUaClient` / `Connection.SetOpcUaClientConnection` / `Connection.GetOpcUaClientConnection` extension members for configuring a client-side connection to an OPC UA server
  - `OpcUaServerConnection` contract and `ConnectionType.OpcUaServer` / `Connection.SetOpcUaServerConnection` / `Connection.GetOpcUaServerConnection` extension members for configuring a server-side OPC UA connection, bound to a single network interface by name
  - `ClusterNodeDeploymentStepChanged` event, published by every instance when a step of its deployment starts, succeeds, fails or is refused by the engine host, with the engine host or engine it works on
  - `DeploymentState.CorrelationId`, the correlation id of the running deployment's events
  - `ClusterUpdateFailed.WasReset`, set when a deployment did not fail on its own but its state was reset
  - `ClusterUpdateFailed.FailedVersionKept`, set when the rollback could not delete the failed version
  - `Constants.EngineControlFeature`, the module feature a role needs to start, stop and tear down engines
  - OPC UA client and server connections can be created and edited in the suite's connection settings dialog: the backend registers both types with `IConnectionTypeRegistry`, and the client contributes localized settings components and validators via `IConnectionTypeUiRegistry`
  - `OpcUaClientConnection.GetCertificateStoreProperties` / `OpcUaServerConnection.GetCertificateStoreProperties` extension members, the store type and store path properties of the OPC UA DataPorts keyed by property id, for generating a DataPort from a connection
- OPC UA connection settings list, upload and delete the certificates of their certificate stores
- OPC UA certificate stores stay valid when a backup is restored on an instance with another home directory
- Deleting an OPC UA connection deletes its certificates
- Cluster Manager collects the cycle reports of every engine host in the running version from module start, over one MQTT connection per broker, so its cycle time charts are filled when the page opens
- Cluster Manager runtime panel: engine state per engine host, live cycle time chart per engine and per engine host chain read from the broker its instance deployed it with, and a journal link per engine host on the same instance for users allowed to view the log; the engine states stay current without a refresh button, also when an engine host stops or comes back
- Cluster Manager marks engine hosts whose chain takes longer than its cycle time
- Cluster Manager warns when a deployment is rejected because another one is still running
- Cluster Manager names the step and the engine host or engine a failed instance stopped at
- Cluster Manager lists the steps of each instance of a deployment with their durations, and the step a working instance is on and for how long
- Cluster Manager shows the steps of a standalone instance only once its update has ended
- Cluster Manager deploys the default cluster from a quick action in the version list, after a confirmation
- Cluster Manager Expert view, switched on in the page bar for the session: adds the topology counts, the cycle times and journal links of the engine hosts, the steps of each instance, the deployment state, the packages, downloading and deleting versions and the Engines page; the Standard view keeps every state and error
- Cluster Manager runtime panel and Engines page show the description of an engine host or engine when its name is hovered
- IODD list shows the description of a device variant when its name is hovered
- Event Viewer marks values the engine sent as invalid; its Expert view adds the engine cycle that sent each value and the milliseconds of its time
- Deploy DataPort trees with envelope children (datapoints under a datapoint): the parent keeps its own connector and channel, and every child gets a connector that the parent transfers with the parent's transfer mode; serving them needs a DataPort provider built for envelope children (`dp-mqtt` from `2.0.0`)

### Removed

- The cluster import wizard and the cluster export in the cluster editor, both removed from `ViciOne.Ui.ClusterEditor`; the JSON download on the management page remains
- The DevExpress dependency, which came in through `ViciOne.Ui.ClusterEditor` and `ViciOne.Ui.Shared.Dx`

### Changed

- New OPC UA server connections default to the security policy Basic256Sha256 - Sign & Encrypt
- Cluster Manager shows the running cluster with its deployment time, topology counts and packages next to a version timeline
- Cluster Manager opens the Event Viewer from a card on the page instead of a toolbar button, and its deployment state tools (Get Saga, Reset Saga) sit in the header of the deployment panel
- Cluster Manager pages share a page bar with a trail from the cluster to its pages, what the running cluster is doing and the page's actions; the top bar names the Cluster Manager on every page
- Cluster Manager tile describes the module as "Deploy & monitor versions"
- Dataflow events page matches the Cluster Manager, opens with a tree of dataflows and their containers that expands one level at a time, opens or closes a group with a click anywhere on its row, groups events by dataflow, function block, input or output, or value type, subscribes the outputs in view instead of offering subscribe buttons, writes Boolean inputs with a checkbox and other inputs with Enter or the send button, and copies a value with a click
- Dataflow events page reloads its events after a deployment, so its refresh button is gone
- Event Viewer searches, groups, filters and opens or closes all groups from one toolbar at the head of its table, with the expand and collapse icons of the cluster editor
- Cluster Manager deployments show their phases, the error of each instance and whether the rollback succeeded, instead of a status and duration column
- Deleting a cluster version asks for confirmation
- Engines page shows its actions only in the Expert view
- **Breaking:** Starting, stopping and tearing down engines and resetting the deployment state need the new `Engine control` feature, which the Administrator role gets at the next start; grant it to other roles that need it, and their users log in again. The feature only hides these actions in the pages; the backend does not check it
- Engines page lists the engines under their engine host in the order they run, with their state and latest cycle and the host's cycle against its cycle time; the Expert view adds their order, ids, the fastest and slowest cycle since the Cluster Manager receives the cycles of the engine host, function blocks and log level, and tearing down an engine asks first
- Resetting the deployment state asks for confirmation, ends every deployment of the cluster as failed so nothing waits for it, logs who reset it and says what it reset; it also works while a hung deployment blocks other commands
- Package uploads in the cluster editor now stream to disk instead of loading the whole archive into memory, increasing the maximum package upload size from 2 MB to 10 MB
- IODD downloads are grouped by vendor, one archive per vendor instead of one per selected device variant
- Failed IODD uploads show an error banner, and the IODD action buttons match the package panel
- Uploading an IODD that already exists names the device instead of showing "Upload failed"
- IODD upload times are shown as short date and 24-hour time in the user's time zone
- The IODD tree selects the repository root by default, so all devices are listed
- Package updates only suggest a prerelease, such as `2.0.0-beta.1` or a `-ci` build, when the installed version is a prerelease itself; prereleases can still be installed from the version list
- Engine hosts write log event properties such as `SOURCE_CONTEXT`, `ENGINE_ID` and `EVENT_ID` as separate journal fields, not only inside `SYSLOG_STRUCTURED_DATA`
- Clusters are read with a cache of resolved types instead of resolving every typed value against all package assemblies, which shortens deployments and the Cluster Manager's CPU load while engines run
- `Public`
  - `IoddChanged`, added the error codes `IoddFileNotFound`, `IoddFileOutsideUploadDirectory` and `IoddAlreadyExists`; for `IoddAlreadyExists` the event carries the id and `DeviceInfo` of the existing device
  - **Breaking:** `IIoddStore`, replaced `TryAddDevice(ZipArchive, out IoddId?)` with `AddDevice(ZipArchive, out IoddId?)`, which returns an `AddDeviceResult` telling whether the device was added, the archive has no IODD file, the device already exists or extracting it failed
  - `ClusterErrors`, added the error codes `EngineNotDeployed` and `EngineHostDidNotAnswer`
  - `IIoddStore`, replaced `FillDeviceArchive` with `FillVendorArchive` which archives several devices of a vendor as `<vendor-id>/<device-id>/<IODD files>`
  - `DownloadIoddRequest`, replaced `Id` with `VendorId` and `DeviceIds`
  - `DownloadIoddResponse`, `Filename` now contains the vendor archive name `iodds-<vendor name>.zip`
  - **Breaking:** `IDataflowEvent`, added `FunctionBlockId`, `FunctionBlockName`, `ConnectorName`, `DataflowName` and `ContainerPath`
- **Breaking:** Fail a deployment with a message naming the node and the declaring node type when a tree node is linked against the transfer direction its node type declares or inherits from an ancestor or the DataPort; directions a node type declares are narrowed to those its ancestors transfer
- **Breaking:** Fail a deployment when an envelope child is linked in a direction the ruleset's `LinkDirections` does not allow; a predefined child such as a validity derived from the parent value is sent with its parent's message but must never be linked
- **Breaking:** `DeployCluster` is rejected with `ClusterUpdateRejected` while another deployment of the same cluster is running

- Updated .NET to version `10.0.12`
- `AspNetCore.SassCompiler` package, update version to `1.105.1`
- `MQTTnet` package, update version to `5.2.0.1603`
- `System.IO.Abstractions`, updated to version `22.3.0`
- `System.IO.Abstractions` package, added to `ClusterManagement.Internal`
- `ViciOne.Suite.Sdk` packages, update to version `3.2.0`
- `ViciOne.Ui.ClusterEditor` package, update to `2.1.0`
- `ViciOne.Ui.Design` package, update version to `2.5.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `4.18.0`
- `ViciOne.Ui.Localization` package, update version to `3.6.0`
- `ViciOne.Tree.Builder` package, update version to `3.1.0`
- Rename the company to `ViciOne open automation gmbh` in the license and the package metadata
- Cluster Manager and Event Viewer call FunctionBlocks by their name, in English and German
- Cluster Manager version list, Engines page and Event Viewer show the actions of a row only while the pointer or the keyboard focus is on it, unless the screen has no pointer that hovers
- A deployment that gets no answer from an instance, e.g. an offline node, fails after a timeout and rolls back instead of waiting forever

### Fixed

- Startup no longer adds a second Default Cluster when the snapshot of the active version is missing
- Unsubscribing an event topic the broker held no subscription for no longer logs a warning
- Parallel deployments of one cluster no longer leave a deployment in `CleaningCluster` or fail the deployment state request
- Two deployments completing close together no longer break the reconnect of the dataflow event connection
- Asking for the state of an engine answers with an error when it fails unexpectedly, instead of failing the request
- Cluster Manager shows an engine its host could not start as "Start failed" instead of stopped
- Engines page lists every engine once and shows each state as it arrives, instead of after asking every engine one after the other
- Engines page and Runtime panel show the engines as deploying while a deployment runs and ask them once it ends, instead of waiting for engine hosts the deployment restarts and warning that they did not answer
- Cluster Manager buttons light up on hover and keep no accent border after a click
- Event Viewer keeps the room of its connector column on screens narrower than 1000px instead of an empty column beside the others
- Cluster Manager shows the hidden versions when the version list is expanded, instead of a scroll area as high as the collapsed list
- Cluster Manager names a count of one in the singular, such as "1 Node" and "1 more version"
- Event Viewer shows and copies characters such as °, ä or µ in values as themselves instead of escaped like `\u00B0`
- `ClusterSerializer` reads several clusters at the same time; concurrent reads corrupted its type cache and failed the load
- Downloading IODDs no longer produces redundant archives with identical content for devices with multiple product variants
- Load function blocks dialog, checking a function block no longer affects an equally named one in another namespace (e.g. `ACX.ViciOne.FunctionBlocks.Test` and `ACX.ViciOne.StandardFunction.Test`)
- Write `packages.json`, `packages-ops.json` and cluster snapshots atomically so a crash or power loss during a write cannot leave corrupt files behind
- Recover from corrupted or partially written package files on startup instead of failing module initialization
- Replacing a cluster snapshot no longer leaves trailing data of the previous version in the file
- Deploying a cluster no longer fails with `Value cannot be null. (Parameter 'link.DestinationConnector')` when a FunctionBlock output is linked to a DataPort datapoint whose transfer mode is None
- Recognize cluster packages with a SemVer prerelease version such as `2.0.0-beta.1`, which were missing from the package list and failed to download or upload
- Reject package uploads whose version is not valid SemVer, such as `2.0.0-01`, which kept all packages from loading on the next start
- Sort a package's version list by version instead of alphabetically when it contains uploaded versions, e.g. `1.10.0` above `1.9.0`
- `IEventBroker.Subscribe` no longer keeps the handler when it fails, and no longer fails when a handler throws on the last value
- A deployment whose rollback cannot remove the committed version is reported as failed instead of staying in progress
- A deployment that fails while committing the target version shows the commit error
- A deployment whose target or active cluster cannot be loaded is reported as failed instead of staying in progress
- A master restarted during a deployment deploys its running version again instead of rejecting every further deployment
- A deployment that fails right after committing its version no longer leaves that version in the version list
- Cluster Manager shows the version being deployed instead of `0.0.0` while a new version is committed and when a deployment fails before it starts
- Saving a cluster while another save or a deployment of it is committing no longer deletes or takes the other's version, and toggling engines or changing function block settings at the same time no longer loses one of the changes or the active snapshot
- A failed store no longer deletes an existing cluster version
- Committing a cluster whose id differs from the committed cluster fails instead of storing it as another cluster
- Committing a cluster that cannot be read fails at once instead of after several retries
- Cluster Manager groups the versions by cluster when several are stored, and shows draft and default only for the running cluster
- Dataflow events page no longer stops receiving values when a write or subscription reconnects to the broker after the connection dropped
- The dataflow events page and the debug engines page require full access to Cluster Management like its other pages; any signed-in user could open them by URL, write dataflow inputs and start, stop or tear down engines

## 2.3.0 - 2026-07-13

### Changed

- `ViciOne.Ui.ClusterEditor` package, update to `1.2.0`

## 2.2.0 - 2026-06-12

### Changed

- Added authorization requirement to have full access to control panels
- Throttle parallel package artifact downloads to avoid client pool exhaustion
- Updated .NET to version `10.0.8`
- `AspNetCore.SassCompiler` package, update version to `1.100.0`
- `MassTransit.EntityFrameworkCore` package, update version to `8.5.10`
- `ViciOne.Suite.Sdk` packages, update to version `2.1.0`
- `ViciOne.Suite.DataPort` package, update to version `0.32.0`
- `ViciOne.Ui.Design` package, update to `2.1.0`
- `ViciOne.Ui.ClusterEditor` package, update to `1.1.0`
- `ViciOne.Ui.Localization` package, update version to `3.4.0`
- `ViciOne.Ui.MonochromeIcons.Assets` package, update version to `4.11.0`

- Update `ViciOne.Suite.DataPort.Http.Client` to `0.32.0`
- Update `ViciOne.Suite.DataPort.InfluxDb` to `0.32.0`
- Update `ViciOne.Suite.DataPort.MySql` to `0.32.0`
- Update `ViciOne.Suite.DataPort.Mqtt` to `1.0.0`
- Update `ViciOne.Suite.DataPort.OpcUa.Client` to `0.32.0`
- Update `ViciOne.Suite.DataPort.OpcUa.Server` to `0.32.0`
- Update `ViciOne.Suite.DataPort.PostgreSql` to `0.32.0`
- Update `ViciOne.Suite.DataPort.Sqlite` to `0.32.0`
- Update `ViciOne.Suite.DataPort.SqlServer` to `0.32.0`

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
