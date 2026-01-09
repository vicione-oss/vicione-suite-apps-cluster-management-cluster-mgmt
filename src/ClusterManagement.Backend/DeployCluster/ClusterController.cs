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
        var deployState = await sagaDbContext.DeployClusterState
            .FirstOrDefaultAsync(ControllerContext.HttpContext.RequestAborted);

        if (deployState is null)
            return Content("No cluster saga state found");

        switch (command.ToUpperInvariant())
        {
            case "RESET":
                TryLogClusterState(deployState);
                sagaDbContext.DeployClusterState.Remove(deployState);
                await sagaDbContext.Instance.SaveChangesAsync();
                return Content("Cluster saga state removed!");

            case "STATUS":
                return new JsonResult(deployState, GetSerializerOptions());

            default:
                return Content("Invalid saga command!");
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
