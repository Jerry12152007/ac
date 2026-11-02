namespace ACLocalTrafficStudio;

public sealed class CarEntry
{
    public string Folder { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
}

public sealed class TrackEntry
{
    public string Folder { get; set; } = "";
    public string Layout { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string AiStatus { get; set; } = "";
}
