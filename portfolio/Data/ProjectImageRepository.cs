using Npgsql;
using portfolio.Models;

namespace portfolio.Data;

public class ProjectImageRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ProjectImageRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Converts a database row into a ProjectImage object.
    private static ProjectImage ReadProjectImage(NpgsqlDataReader reader)
    {
        return new ProjectImage
        {
            image_id = reader.GetInt64(0),
            project_id = reader.GetInt64(1),
            image_url = reader.GetString(2)
        };
    }

    // READ: Get all project images.
    public async Task<List<ProjectImage>> GetAllAsync()
    {
        const string sql = @"
            SELECT image_id, project_id, image_url
            FROM public.project_image
            ORDER BY image_id;
        ";

        var images = new List<ProjectImage>();

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            images.Add(ReadProjectImage(reader));
        }

        return images;
    }

    // READ: Get all images belonging to one project.
    public async Task<List<ProjectImage>> GetByProjectIdAsync(long projectId)
    {
        const string sql = @"
            SELECT image_id, project_id, image_url
            FROM public.project_image
            WHERE project_id = @projectId
            ORDER BY image_id;
        ";

        var images = new List<ProjectImage>();

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("projectId", projectId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            images.Add(ReadProjectImage(reader));
        }

        return images;
    }

    // READ: Get one project image by ID.
    public async Task<ProjectImage?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT image_id, project_id, image_url
            FROM public.project_image
            WHERE image_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return ReadProjectImage(reader);
        }

        return null;
    }

    // CREATE: Add a project image.
    public async Task AddAsync(ProjectImage projectImage)
    {
        const string sql = @"
            INSERT INTO public.project_image
                (project_id, image_url)
            VALUES
                (@project_id, @image_url);
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectImage.project_id);
        command.Parameters.AddWithValue("image_url", projectImage.image_url);

        await command.ExecuteNonQueryAsync();
    }

    // UPDATE: Edit an existing project image.
    public async Task UpdateAsync(ProjectImage projectImage)
    {
        const string sql = @"
            UPDATE public.project_image
            SET
                project_id = @project_id,
                image_url = @image_url
            WHERE image_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectImage.project_id);
        command.Parameters.AddWithValue("image_url", projectImage.image_url);
        command.Parameters.AddWithValue("id", projectImage.image_id);

        await command.ExecuteNonQueryAsync();
    }

    // DELETE: Remove a project image.
    public async Task DeleteAsync(long id)
    {
        const string sql = @"
            DELETE FROM public.project_image
            WHERE image_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync();
    }
}