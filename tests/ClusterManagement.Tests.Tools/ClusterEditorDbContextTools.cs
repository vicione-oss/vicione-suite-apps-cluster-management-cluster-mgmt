using ClusterManagement.Backend.DbContext;
using ClusterManagement.Public.ClusterEditor;

namespace ClusterManagement.Tests.Tools;

public static class ClusterEditorDbContextTools
{
    public static Settings SeedSettings(this IClusterEditorDbContext context)
    {
        var settings = new Settings
        {
            GridMode = 1,
            NodeAlignmentBorder = true,
            UserId = "TestUser1",
        };

        context.Settings.Add(settings);
        context.Instance.SaveChanges();
        return settings;
    }
}
