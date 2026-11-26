using portfolio.Data;
using portfolio.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace portfolio.Controllers;

// Everything in this controller requires the editor login,
// except actions marked [AllowAnonymous] (Index and Error).
[Authorize]
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

    // PUBLIC: viewers can see the project list
    [AllowAnonymous]
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

    // EDITOR ONLY
    public async Task<IActionResult> Create()
    {
        await LoadCourses();
        return View(new Project());
    }

    // EDITOR ONLY
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Project project,
        List<IFormFile>? imageFiles,
        IFormFile? pdfFile)
    {
        imageFiles ??= new List<IFormFile>();
        imageFiles = imageFiles.Where(f => f.Length > 0).ToList();

        var hasPdf = pdfFile != null && pdfFile.Length > 0;

        ValidateProject(project, new List<string>());

        foreach (var file in imageFiles)
        {
            var error = _storage.Validate(file);

            if (error != null)
                ModelState.AddModelError("imageFiles", error);
        }

        if (hasPdf)
        {
            var pdfError = _storage.ValidatePdf(pdfFile!);

            if (pdfError != null)
                ModelState.AddModelError("pdfFile", pdfError);
        }

        if (!ModelState.IsValid)
        {
            await LoadCourses();
            return View(project);
        }

        var uploadedUrls = new List<string>();   // images
        string? uploadedPdf = null;

        try
        {
            foreach (var file in imageFiles)
                uploadedUrls.Add(await _storage.UploadAsync(file));

            if (hasPdf)
            {
                uploadedPdf = await _storage.UploadPdfAsync(pdfFile!);
                project.pdf_url = uploadedPdf;
            }

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

        // The save failed: remove anything already uploaded
        foreach (var url in uploadedUrls)
            await _storage.DeleteByUrlAsync(url);

        if (uploadedPdf != null)
            await _storage.DeleteByUrlAsync(uploadedPdf);

        await LoadCourses();
        return View(project);
    }

    // EDITOR ONLY
    public async Task<IActionResult> Edit(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        await LoadCourses();
        return View(project);
    }

    // EDITOR ONLY
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Project project,
        List<IFormFile>? imageFiles,
        IFormFile? pdfFile)
    {
        imageFiles ??= new List<IFormFile>();
        imageFiles = imageFiles.Where(f => f.Length > 0).ToList();

        var hasPdf = pdfFile != null && pdfFile.Length > 0;

        ValidateProject(project, new List<string>());

        foreach (var file in imageFiles)
        {
            var error = _storage.Validate(file);

            if (error != null)
                ModelState.AddModelError("imageFiles", error);
        }

        if (hasPdf)
        {
            var pdfError = _storage.ValidatePdf(pdfFile!);

            if (pdfError != null)
                ModelState.AddModelError("pdfFile", pdfError);
        }

        if (!ModelState.IsValid)
        {
            await LoadCourses();
            return View(project);
        }

        var existing = await _projects.GetByIdAsync(project.project_id);

        if (existing == null)
            return NotFound();

        string? newPdf = null;

        try
        {
            // Keep the current PDF unless a new one was chosen
            project.pdf_url = existing.pdf_url;

            if (hasPdf)
            {
                newPdf = await _storage.UploadPdfAsync(pdfFile!);
                project.pdf_url = newPdf;
            }

            await _projects.UpdateAsync(project);

            // The old PDF was replaced, so remove its file
            if (newPdf != null && !string.IsNullOrEmpty(existing.pdf_url))
                await _storage.DeleteByUrlAsync(existing.pdf_url);

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

    // EDITOR ONLY
    public async Task<IActionResult> Delete(long id)
    {
        var project = await _projects.GetByIdAsync(id);

        if (project == null)
            return NotFound();

        ViewBag.Error = TempData["Error"];
        return View(project);
    }

    // EDITOR ONLY
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

            if (!string.IsNullOrEmpty(project.pdf_url))
                await _storage.DeleteByUrlAsync(project.pdf_url);

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

    // Validate the project details and image URLs. Add errors to ModelState if invalid.
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

    // PUBLIC: the error page must work for viewers too
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}