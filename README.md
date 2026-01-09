# ViciOne Suite ClusterManagement

ClusterManagement provides an api to control and manage the cluster besides an user interface to build and modify all logic and data connections.

## Manually Publish Module

1. Build `ClusterManagement.Deployment` project
1. Execute `shared/deploy/publish-module.ps1 -module ClusterManagement -srcPath src -outputPath artifacts/publish`
1. Execute `shared/deploy/cleanup-module.ps1 -artifactPath artifacts/publish`

## Module Debugging
For debugging purposes, you need to modify the [launch settings](./src/ClusterManagement.Backend/Properties/launchSettings.json) and set paths to ViciOne.Suite. Now you can use the `ClusterManagement.SuiteDebug` to start the ViciOne.Suite directly out of your IDE.

Afterwards you need to configure ViciOne.Suite to load our ClusterManagement module. Therefore add `path/to/ClusterManagement/src` into `ModuleLoader:ModuleDebugPaths` within the `appsettings.json` of the ViciOne.Suite you configured within `launchSettings.json`.

## Run Tests

To run all tests executed in the pipeline use `dotnet test --filter "Category!=System & Category!=ManualDbTest" -c Debug|Release`. To run the system tests you'll need to manually configure [ClusterSystemTestContext](./tests/ClusterManagement.Tests/Backend/ClusterSystemTestContext.cs) and you'll need a running mqtt broker. Use `dotnet test --filter "Category=System" -c Debug|Release` to run only the system tests.
