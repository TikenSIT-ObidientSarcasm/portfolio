
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
    private readonly ImageStorageService _storage;

    public ProjectController(
        ProjectRepository projects,
        CourseRepository courses,
        ProjectImageRepository projectImages,
        ImageStorageService storage)
    {
        _projects = projects;
        _courses = courses;
        _projectImages = projectImages;
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _projects.GetAllAsync();
        var courses = await _courses.GetAllAsync();
        var images = await _projectImages.GetAllAsync();

        ViewBag.CourseLookup = courses.ToDictionary(
            c => c.course_id, c => c);

        ViewBag.ProjectImages = images
            .GroupBy(i => i.project_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(projects);
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
        List<IFormFile>? imageFiles)
    {
        imageFiles ??= new List<IFormFile>();
        imageFiles = imageFiles.Where(f => f.Length > 0).ToList();

        ValidateProject(project, new List<string>());

        foreach (var file in imageFiles)
        {
            var error = _storage.Validate(file);

            if (error != null)
                ModelState.AddModelError("imageFiles", error);
        }

        if (!ModelState.IsValid)
        {
            await LoadCourses();
            return View(project);
        }

        var uploadedUrls = new List<string>();

        try
        {
            foreach (var file in imageFiles)
                uploadedUrls.Add(await _storage.UploadAsync(file));

            await _projects.AddProjectWithImagesAsync(
                project, uploadedUrls);

            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "course_id",
                "The selected course does not exist.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(
                "", $"Save failed: {ex.Message}");
        }

        foreach (var url in uploadedUrls)
            await _storage.DeleteByUrlAsync(url);

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
    public async Task<IActionResult> Edit(
        Project project,
        List<IFormFile>? imageFiles)
    {
        imageFiles ??= new List<IFormFile>();
        imageFiles = imageFiles.Where(f => f.Length > 0).ToList();

        ValidateProject(project, new List<string>());

        foreach (var file in imageFiles)
        {
            var error = _storage.Validate(file);

            if (error != null)
                ModelState.AddModelError("imageFiles", error);
        }

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

            foreach (var file in imageFiles)
            {
                var url = await _storage.UploadAsync(file);

                await _projectImages.AddAsync(new ProjectImage
                {
                    project_id = project.project_id,
                    image_url = url
                });
            }

            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "course_id",
                "The selected course does not exist.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(
                "", $"Update failed: {ex.Message}");
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
            // Get the image URLs BEFORE deleting, because the cascade
            // removes the image rows along with the project.
            var images = await _projectImages.GetByProjectIdAsync(id);

            await _projects.DeleteAsync(id);

            // Now remove the actual files from Supabase Storage.
            foreach (var image in images)
                await _storage.DeleteByUrlAsync(image.image_url);

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
            courses, "course_id", "course_name");
    }

    private void ValidateProject(
        Project project, List<string> imageUrls)
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
                "video_url", "Enter a valid video URL.");
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

    public IActionResult Error()
    {
        return View();
    }
}
