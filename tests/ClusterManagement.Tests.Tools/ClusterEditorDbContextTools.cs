using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Tests.Tools;

public static class ClusterEditorDbContextTools
{
    public static EditorSettings SeedSettings(this IClusterEditorDbContext context)
    {
        var settings = new EditorSettings
        {
            GridMode = 1,
            NodeAlignmentBorder = true,
            UserId = "TestUser1",
        };

        context.Settings.Add(settings);
        context.SaveChanges();
        return settings;
    }
}
