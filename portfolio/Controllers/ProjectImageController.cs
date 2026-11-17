
using portfolio.Data;
using portfolio.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using System.Text.RegularExpressions;

namespace portfolio.Controllers;

public class ProjectImageController : Controller
{
    private readonly ProjectImageRepository _projectImages;
    private readonly ProjectRepository _projects;

    public ProjectImageController(
        ProjectImageRepository projectImages,
        ProjectRepository projects)
    {
        _projectImages = projectImages;
        _projects = projects;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _projectImages.GetAllAsync());
    }

    public async Task<IActionResult> Create()
    {
        await LoadProjects();
        return View(new ProjectImage());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectImage projectImage)
    {
        ValidateImage(projectImage);

        if (!ModelState.IsValid)
        {
            await LoadProjects();
            return View(projectImage);
        }

        try
        {
            await _projectImages.AddAsync(projectImage);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "project_id",
                "The selected project does not exist."
            );
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "image_url",
                "This image URL already exists for this project."
            );
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "image_url",
                "The image URL does not satisfy the database rules."
            );
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while saving the image."
            );
        }

        await LoadProjects();
        return View(projectImage);
    }

    public async Task<IActionResult> Edit(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        await LoadProjects();
        return View(image);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProjectImage projectImage)
    {
        ValidateImage(projectImage);

        if (!ModelState.IsValid)
        {
            await LoadProjects();
            return View(projectImage);
        }

        var existing = await _projectImages.GetByIdAsync(
            projectImage.image_id
        );

        if (existing == null)
            return NotFound();

        try
        {
            await _projectImages.UpdateAsync(projectImage);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ModelState.AddModelError(
                "project_id",
                "The selected project does not exist."
            );
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "image_url",
                "This image URL already exists for this project."
            );
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "image_url",
                "The image URL does not satisfy the database rules."
            );
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while updating the image."
            );
        }

        await LoadProjects();
        return View(projectImage);
    }

    public async Task<IActionResult> Delete(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        return View(image);
    }

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
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] =
                "Something went wrong while deleting the image.";

            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task LoadProjects()
    {
        var projects = await _projects.GetAllAsync();

        ViewBag.Projects = new SelectList(
            projects,
            "project_id",
            "project_title"
        );
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
                "Enter a valid image URL beginning with http:// or https://."
            );
        }
    }
}
