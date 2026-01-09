using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster;

[ApiController]
[Route("api/[controller]")]
public sealed class ClusterController(IClusterSagaDbContext sagaDbContext, ILogger<ClusterController> logger) : ControllerBase
{
    // GET api/<controller>/saga/{command}
    [HttpGet("saga/{command}")]
    public async Task<IActionResult> Saga(string command)
    {
        try
        {
            var deployState = await sagaDbContext.DeployClusterState
                .FirstOrDefaultAsync(ControllerContext.HttpContext.RequestAborted);

            if (deployState is null)
                return Content("No cluster saga state found");

            switch (command.ToUpperInvariant())
            {
                case "RESET":
                    TryLogClusterState(deployState);
                    sagaDbContext.DeployClusterState.Remove(deployState);
                    await sagaDbContext.Instance.SaveChangesAsync(ControllerContext.HttpContext.RequestAborted);
                    return Content("Cluster saga state removed!");

                case "STATUS":
                    return new JsonResult(deployState, GetSerializerOptions());

                default:
                    return Content("Invalid saga command!");
            }
        }
        catch (ArgumentException aex)
        {
            // On edge-s after excessive testing a saga state was stored to database where CommitVersion is empty
            // and therefore this exception is raised on Version.Parse that's called internally by EF. I don't know
            // how this can happen yet...set the default version "0.0.0" to the migration.
            logger.LogError(aex, "Invalid deploy database state detected");
            return Content($"Invalid database deploy state detected");
        }
        catch (Exception ex)
        {
            return Content($"Failed: {ex.Message}");
        }
    }

    private void TryLogClusterState(DeployClusterState deployState)
    {
        try
        {
            logger.LogWarning("Reset current deployment state: {State}", JsonSerializer.Serialize(deployState, GetSerializerOptions()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log serialized deployment state");
        }
    }

    private static JsonSerializerOptions GetSerializerOptions()
        => new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { DefaultJsonSerializerSettings.CreateDateTimeConverter() },
            WriteIndented = true
        };
}
