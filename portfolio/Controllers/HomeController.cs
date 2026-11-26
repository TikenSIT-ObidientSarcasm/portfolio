using Microsoft.AspNetCore.Mvc;
using portfolio.Data;

namespace portfolio.Controllers;

// Public landing page: anyone can see it, no login needed.
public class HomeController : Controller
{
    private readonly ProjectRepository _projects;
    private readonly CourseRepository _courses;
    private readonly ProjectImageRepository _projectImages;

    public HomeController(
        ProjectRepository projects,
        CourseRepository courses,
        ProjectImageRepository projectImages)
    {
        _projects = projects;
        _courses = courses;
        _projectImages = projectImages;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _projects.GetAllAsync();
        var courses = await _courses.GetAllAsync();
        var images = await _projectImages.GetAllAsync();

        // Numbers for the stat panels
        ViewBag.ProjectCount = projects.Count();
        ViewBag.CourseCount = courses.Count();
        ViewBag.ImageCount = images.Count();

        // Lookups so the cards can show a course code and a cover image
        ViewBag.CourseLookup = courses.ToDictionary(
            c => c.course_id, c => c);

        ViewBag.ProjectImages = images
            .GroupBy(i => i.project_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        // The three newest projects
        var latest = projects
            .OrderByDescending(p => p.project_id)
            .Take(3)
            .ToList();

        return View(latest);
    }
}