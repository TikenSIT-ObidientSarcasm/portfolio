using portfolio.Data;
using portfolio.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System.Text.RegularExpressions;

namespace portfolio.Controllers;

// Everything here requires the editor login,
// except actions marked [AllowAnonymous] (Index and Details).
[Authorize]
public class CourseController : Controller
{
    private readonly CourseRepository _courses;
    private readonly ProjectRepository _projects;

    public CourseController(
        CourseRepository courses,
        ProjectRepository projects)
    {
        _courses = courses;
        _projects = projects;
    }

    // PUBLIC: viewers can see the course list
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var courses = await _courses.GetAllAsync();

        var projectCounts = new Dictionary<long, int>();

        foreach (var course in courses)
        {
            var projects = await _projects.GetByCourseIdAsync(course.course_id);
            projectCounts[course.course_id] = projects.Count;
        }

        ViewBag.ProjectCounts = projectCounts;

        return View(courses);
    }

    // PUBLIC: read-only course details
    [AllowAnonymous]
    public async Task<IActionResult> Details(long id)
    {
        var course = await _courses.GetByIdAsync(id);

        if (course == null)
            return NotFound();

        ViewBag.Projects = await _projects.GetByCourseIdAsync(id);

        return View(course);
    }

    // EDITOR ONLY
    public IActionResult Create()
    {
        return View(new Course());
    }

    // EDITOR ONLY
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Course course)
    {
        ValidateCourse(course);

        if (!ModelState.IsValid)
            return View(course);

        try
        {
            await _courses.AddAsync(course);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "course_code",
                "This course code already exists.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "",
                "The course details do not satisfy the database rules.");
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while saving the course.");
        }

        return View(course);
    }

    // EDITOR ONLY
    public async Task<IActionResult> Edit(long id)
    {
        var course = await _courses.GetByIdAsync(id);

        if (course == null)
            return NotFound();

        return View(course);
    }

    // EDITOR ONLY
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Course course)
    {
        ValidateCourse(course);

        if (!ModelState.IsValid)
            return View(course);

        var existing = await _courses.GetByIdAsync(course.course_id);

        if (existing == null)
            return NotFound();

        try
        {
            await _courses.UpdateAsync(course);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "course_code",
                "This course code is already being used.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            ModelState.AddModelError(
                "",
                "The course details do not satisfy the database rules.");
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                "",
                "Something went wrong while updating the course.");
        }

        return View(course);
    }

    // EDITOR ONLY
    public async Task<IActionResult> Delete(long id)
    {
        var course = await _courses.GetByIdAsync(id);

        if (course == null)
            return NotFound();

        return View(course);
    }

    // EDITOR ONLY
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var course = await _courses.GetByIdAsync(id);

        if (course == null)
            return NotFound();

        try
        {
            await _courses.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            ViewBag.Error =
                "This course cannot be deleted because it still has projects.";

            return View("Delete", course);
        }
        catch (Exception)
        {
            ViewBag.Error =
                "Something went wrong while deleting the course.";

            return View("Delete", course);
        }
    }

    private void ValidateCourse(Course course)
    {
        if (string.IsNullOrWhiteSpace(course.course_code) ||
            !Regex.IsMatch(
                course.course_code.Trim(),
                @"^[A-Za-z0-9][A-Za-z0-9_-]{1,29}$"))
        {
            ModelState.AddModelError(
                "course_code",
                "Use 2–30 letters, numbers, hyphens, or underscores.");
        }

        if (string.IsNullOrWhiteSpace(course.course_name) ||
            course.course_name.Trim().Length < 2)
        {
            ModelState.AddModelError(
                "course_name",
                "Course name must contain at least 2 characters.");
        }
    }
}