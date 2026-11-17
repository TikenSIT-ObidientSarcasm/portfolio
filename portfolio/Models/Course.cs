namespace portfolio.Models;

public class Course
{
    public long course_id { get; set; }

    public string course_code { get; set; } = string.Empty;

    public string course_name { get; set; } = string.Empty;

    public string? description { get; set; }
}