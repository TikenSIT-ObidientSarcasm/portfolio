namespace portfolio.Models;

public class Project
{
    public long project_id { get; set; }

    public long course_id { get; set; }

    public string project_title { get; set; } = string.Empty;

    public string? description { get; set; }

    public string? video_url { get; set; }

    public DateTime? created_at { get; set; }
}
