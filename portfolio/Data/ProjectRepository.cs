using portfolio.Models;
using Npgsql;

namespace portfolio.Data;

public class ProjectRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ProjectRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Converts a database row into a Project object.
    private static Project ReadProject(NpgsqlDataReader reader)
    {
        return new Project
        {
            project_id = reader.GetInt64(0),
            course_id = reader.GetInt64(1),
            project_title = reader.GetString(2),
            description = reader.IsDBNull(3) ? null : reader.GetString(3),
            video_url = reader.IsDBNull(4) ? null : reader.GetString(4),
            created_at = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            pdf_url = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    // READ: Get all projects.
    public async Task<List<Project>> GetAllAsync()
    {
        const string sql = @"
            SELECT project_id, course_id, project_title,
                   description, video_url, created_at, pdf_url
            FROM public.project
            ORDER BY created_at DESC;
        ";

        var projects = new List<Project>();

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    // READ: Get one project by ID.
    public async Task<Project?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT project_id, course_id, project_title,
                   description, video_url, created_at, pdf_url
            FROM public.project
            WHERE project_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return ReadProject(reader);
        }

        return null;
    }

    // READ: Get projects belonging to a course.
    public async Task<List<Project>> GetByCourseIdAsync(long courseId)
    {
        const string sql = @"
            SELECT project_id, course_id, project_title,
                   description, video_url, created_at, pdf_url
            FROM public.project
            WHERE course_id = @courseId
            ORDER BY created_at DESC;
        ";

        var projects = new List<Project>();

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("courseId", courseId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    // CREATE: Add a project.
    public async Task<long> AddAsync(Project project)
    {
        const string sql = @"
            INSERT INTO public.project
                (course_id, project_title, description, video_url, pdf_url)
            VALUES
                (@course_id, @project_title, @description, @video_url, @pdf_url)
            RETURNING project_id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("course_id", project.course_id);
        command.Parameters.AddWithValue("project_title", project.project_title);
        command.Parameters.AddWithValue(
            "description",
            (object?)project.description ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "video_url",
            (object?)project.video_url ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "pdf_url",
            (object?)project.pdf_url ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();

        return (long)result!;
    }

    // UPDATE: Edit an existing project.
    public async Task UpdateAsync(Project project)
    {
        const string sql = @"
            UPDATE public.project
            SET
                course_id = @course_id,
                project_title = @project_title,
                description = @description,
                video_url = @video_url,
                pdf_url = @pdf_url
            WHERE project_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("course_id", project.course_id);
        command.Parameters.AddWithValue("project_title", project.project_title);
        command.Parameters.AddWithValue(
            "description",
            (object?)project.description ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "video_url",
            (object?)project.video_url ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "pdf_url",
            (object?)project.pdf_url ?? DBNull.Value);
        command.Parameters.AddWithValue("id", project.project_id);

        await command.ExecuteNonQueryAsync();
    }

    // DELETE: Remove a project.
    // Its images are automatically deleted through ON DELETE CASCADE.
    public async Task DeleteAsync(long id)
    {
        const string sql = @"
            DELETE FROM public.project
            WHERE project_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync();
    }

    // CREATE: Add a project and its images in ONE transaction.
    public async Task<long> AddProjectWithImagesAsync(
        Project project,
        List<string> imageUrls)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            const string projectSql = @"
                INSERT INTO public.project
                    (course_id, project_title, description, video_url, pdf_url)
                VALUES
                    (@course_id, @project_title, @description, @video_url, @pdf_url)
                RETURNING project_id;
            ";

            long projectId;

            await using (var command =
                new NpgsqlCommand(projectSql, connection, transaction))
            {
                command.Parameters.AddWithValue("course_id", project.course_id);
                command.Parameters.AddWithValue(
                    "project_title", project.project_title);
                command.Parameters.AddWithValue(
                    "description",
                    (object?)project.description ?? DBNull.Value);
                command.Parameters.AddWithValue(
                    "video_url",
                    (object?)project.video_url ?? DBNull.Value);
                command.Parameters.AddWithValue(
                    "pdf_url",
                    (object?)project.pdf_url ?? DBNull.Value);

                projectId = (long)(await command.ExecuteScalarAsync())!;
            }

            const string imageSql = @"
                INSERT INTO public.project_image
                    (project_id, image_url)
                VALUES
                    (@project_id, @image_url);
            ";

            foreach (string imageUrl in imageUrls)
            {
                await using var imageCommand =
                    new NpgsqlCommand(imageSql, connection, transaction);

                imageCommand.Parameters.AddWithValue("project_id", projectId);
                imageCommand.Parameters.AddWithValue("image_url", imageUrl);

                await imageCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            return projectId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}