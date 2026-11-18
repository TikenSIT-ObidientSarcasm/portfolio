
using portfolio.Data;
using portfolio.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using System.Text.RegularExpressions;

namespace portfolio.Controllers;

public class ProjectController : Controller
{
    private readonly ProjectRepository _projects;
    private readonly CourseRepository _courses;
    private readonly ProjectImageRepository _projectImages;

    public ProjectController(
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

        ViewBag.CourseLookup = courses.ToDictionary(
            c => c.course_id,
            c => c);

        ViewBag.ProjectImages = images
            .GroupBy(i => i.project_id)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        return View(projects);
    }

    public async Task<IActionResult> Details(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        ViewBag.Course = await _courses.GetByIdAsync(project.course_id);

        var images = await _projectImages.GetAllAsync();

        ViewBag.Images = images
            .Where(i => i.project_id == id)
            .ToList();

        return View(project);
    }

    public async Task<IActionResult> Create()
    {
        await LoadCourses();
        return View(new Project());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Project project,
        string? ImageUrls)
    {
        var imageUrls = (ImageUrls ?? "")
            .Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(url => url.Trim())
            .Where(url => url.Length > 0)
            .ToList();

        ValidateProject(project, imageUrls);

        if (!ModelState.IsValid)
        {
            ViewData["ImageUrls"] = ImageUrls;
            await LoadCourses();
            return View(project);
        }

        try
        {
            await _projects.AddProjectWithImagesAsync(project, imageUrls);

            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "course_id",
                "The selected course does not exist.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "ImageUrls",
                "A duplicate image URL was found for this project.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "",
                "One or more project details do not satisfy the database rules.");
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while saving the project.");
        }

        ViewData["ImageUrls"] = ImageUrls;
        await LoadCourses();

        return View(project);
    }

    public async Task<IActionResult> Edit(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        await LoadCourses();
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Project project)
    {
        ValidateProject(project, new List<string>());

        if (!ModelState.IsValid)
        {
            await LoadCourses();
            return View(project);
        }

        var existing = await _projects.GetByIdAsync(project.project_id);

        if (existing == null)
            return NotFound();

        try
        {
            await _projects.UpdateAsync(project);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "course_id",
                "The selected course does not exist.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "",
                "The project details do not satisfy the database rules.");
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while updating the project.");
        }

        await LoadCourses();
        return View(project);
    }

    public async Task<IActionResult> Delete(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        ViewBag.Error = TempData["Error"];
        return View(project);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        try
        {
            await _projects.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] =
                "Something went wrong while deleting the project.";

            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task LoadCourses()
    {
        var courses = await _courses.GetAllAsync();

        ViewBag.Courses = new SelectList(
            courses,
            "course_id",
            "course_name");
    }

    private void ValidateProject(
        Project project,
        List<string> imageUrls)
    {
        if (string.IsNullOrWhiteSpace(project.project_title) ||
            project.project_title.Trim().Length < 2)
        {
            ModelState.AddModelError(
                "project_title",
                "Project title must contain at least 2 characters.");
        }

        if (!string.IsNullOrWhiteSpace(project.video_url) &&
            !Regex.IsMatch(
                project.video_url.Trim(),
                @"^https?://[^\s]+$",
                RegexOptions.IgnoreCase))
        {
            ModelState.AddModelError(
                "video_url",
                "Enter a valid URL beginning with http:// or https://.");
        }

        for (int i = 0; i < imageUrls.Count; i++)
        {
            if (!Regex.IsMatch(
                imageUrls[i],
                @"^https?://[^\s]+$",
                RegexOptions.IgnoreCase))
            {
                ModelState.AddModelError(
                    "ImageUrls",
                    $"Image URL on line {i + 1} is invalid.");
            }
        }
    }
}
