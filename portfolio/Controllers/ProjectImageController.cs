
using portfolio.Data;
using portfolio.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Supabase.Storage;
using System.Text.RegularExpressions;

namespace portfolio.Controllers;

public class ProjectImageController : Controller
{
    private const string BucketName = "project-image";
    private const long MaxFileSize = 5 * 1024 * 1024;

    private readonly ProjectImageRepository _projectImages;
    private readonly ProjectRepository _projects;
    private readonly Supabase.Client _supabase;
    private readonly IWebHostEnvironment _environment;

    public ProjectImageController(
        ProjectImageRepository projectImages,
        ProjectRepository projects,
        Supabase.Client supabase,
        IWebHostEnvironment environment)
    {
        _projectImages = projectImages;
        _projects = projects;
        _supabase = supabase;
        _environment = environment;
    }

    // CREATE GET
    public async Task<IActionResult> Create(long projectId)
    {
        var project = await _projects.GetByIdAsync(projectId);

        if (project == null)
            return NotFound();

        ViewBag.ProjectTitle = project.project_title;

        return View(new ProjectImage
        {
            project_id = projectId
        });
    }

    // CREATE POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectImage projectImage)
    {
        // Do not expose an unrestricted service-role upload endpoint
        // in a deployed application without adding authentication.
        if (!_environment.IsDevelopment())
            return NotFound();

        var project = await _projects.GetByIdAsync(
            projectImage.project_id);

        if (project == null)
        {
            ModelState.AddModelError(
                "project_id", "Project does not exist.");
        }

        var file = projectImage.ImageFile;

        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(
                "ImageFile", "Please select an image.");
        }
        else
        {
            if (file.Length > MaxFileSize)
            {
                ModelState.AddModelError(
                    "ImageFile", "Image must not exceed 5 MB.");
            }

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "ImageFile",
                    "Only JPG, JPEG, PNG, and WEBP images are allowed.");
            }

            var expectedContentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => ""
            };

            if (file.ContentType != expectedContentType)
            {
                ModelState.AddModelError(
                    "ImageFile", "The selected file type is invalid.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProjectTitle = project?.project_title;
            return View(projectImage);
        }

        string? storagePath = null;

        try
        {
            var extension = Path.GetExtension(file!.FileName)
                .ToLowerInvariant();

            // Unique filename prevents accidental overwrites.
            storagePath =
                $"{projectImage.project_id}/{Guid.NewGuid():N}{extension}";

            using var memoryStream = new MemoryStream();

            await file.CopyToAsync(memoryStream);

            var bytes = memoryStream.ToArray();

            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => throw new InvalidOperationException(
                    "Unsupported image type.")
            };

            await _supabase.Storage
                .From(BucketName)
                .Upload(
                    bytes,
                    storagePath,
                    new FileOptions
                    {
                        CacheControl = "3600",
                        ContentType = contentType,
                        Upsert = false
                    });

            // Get the public URL from the public bucket.
            projectImage.image_url = _supabase.Storage
                .From(BucketName)
                .GetPublicUrl(storagePath);

            // Save the URL in your existing PostgreSQL table.
            await _projectImages.AddAsync(projectImage);

            return RedirectToAction(
                "Details",
                "Project",
                new { id = projectImage.project_id });
        }
        catch (PostgresException)
        {
            await RemoveUploadedFile(storagePath);

            ModelState.AddModelError(
                "",
                "The image was uploaded, but its database record could not be saved.");
        }
        catch (Exception)
        {
            await RemoveUploadedFile(storagePath);

            ModelState.AddModelError(
                "",
                "Something went wrong while uploading the image. Check your Supabase Storage configuration.");
        }

        ViewBag.ProjectTitle = project?.project_title;
        return View(projectImage);
    }

    // EDIT GET
    public async Task<IActionResult> Edit(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        return View(image);
    }

    // EDIT POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProjectImage projectImage)
    {
        ValidateImage(projectImage);

        var existing = await _projectImages.GetByIdAsync(
            projectImage.image_id);

        if (existing == null)
            return NotFound();

        projectImage.project_id = existing.project_id;

        if (!ModelState.IsValid)
            return View(projectImage);

        try
        {
            await _projectImages.UpdateAsync(projectImage);

            return RedirectToAction(
                "Details",
                "Project",
                new { id = existing.project_id });
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "image_url", "This image URL already exists.");
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while updating the image.");
        }

        return View(projectImage);
    }

    // DELETE GET
    public async Task<IActionResult> Delete(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        ViewBag.Project = await _projects.GetByIdAsync(
            image.project_id);

        return View(image);
    }

    // DELETE POST
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        try
        {
            await _projectImages.DeleteAsync(id);

            return RedirectToAction(
                "Details",
                "Project",
                new { id = image.project_id });
        }
        catch (Exception)
        {
            TempData["Error"] =
                "Something went wrong while deleting the image.";

            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task RemoveUploadedFile(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return;

        try
        {
            await _supabase.Storage
                .From(BucketName)
                .Remove(new List<string> { storagePath });
        }
        catch
        {
            // Do not hide the original upload/database error.
        }
    }

    private void ValidateImage(ProjectImage projectImage)
    {
        if (string.IsNullOrWhiteSpace(projectImage.image_url) ||
            !Regex.IsMatch(
                projectImage.image_url.Trim(),
                @"^https?://[^\s]+$",
                RegexOptions.IgnoreCase))
        {
            ModelState.AddModelError(
                "image_url",
                "Enter a valid image URL beginning with http:// or https://.");
        }
    }
}
