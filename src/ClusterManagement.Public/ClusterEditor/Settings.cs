namespace ClusterManagement.Public.ClusterEditor;

public sealed class Settings
{
    public int GridMode { get; set; } = 1;
    public bool MinimapNodeColoring { get; set; }
    public bool NodeAlignmentBorder { get; set; } = true;
    public bool PanBehavior { get; set; }
    public bool SimplifiedView { get; set; }
    public required string UserId { get; set; }
}
