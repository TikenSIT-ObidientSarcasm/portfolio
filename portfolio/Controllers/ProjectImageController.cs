using Microsoft.AspNetCore.Mvc;
using portfolio.Data;

namespace portfolio.Controllers;

public class ProjectImageController : Controller
{
    private readonly ProjectImageRepository _projectImages;
    private readonly ProjectRepository _projects;
    private readonly ImageStorageService _storage;

    public ProjectImageController(
        ProjectImageRepository projectImages,
        ProjectRepository projects,
        ImageStorageService storage)
    {
        _projectImages = projectImages;
        _projects = projects;
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        var images = await _projectImages.GetAllAsync();
        return View(images);
    }

    public async Task<IActionResult> Delete(long id)
    {
        var image = await _projectImages.GetByIdAsync(id);

        if (image == null)
            return NotFound();

        ViewBag.Project = await _projects.GetByIdAsync(image.project_id);
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
            await _storage.DeleteByUrlAsync(image.image_url);

            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "Something went wrong while deleting the image.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }
}