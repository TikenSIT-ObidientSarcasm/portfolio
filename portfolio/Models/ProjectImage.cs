namespace portfolio.Models;

public class ProjectImage
{
    public long image_id { get; set; }

    public long project_id { get; set; }

    public string image_url { get; set; } = string.Empty;
}
