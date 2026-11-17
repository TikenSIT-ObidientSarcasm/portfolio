
using portfolio.Models;
using Npgsql;

namespace portfolio.Data;

public class CourseRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public CourseRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Converts a database row into a Course object.
    private static Course ReadCourse(NpgsqlDataReader reader)
    {
        return new Course
        {
            course_id = reader.GetInt64(0),
            course_code = reader.GetString(1),
            course_name = reader.GetString(2),
            description = reader.IsDBNull(3)
                ? null
                : reader.GetString(3)
        };
    }

    // READ: Get all courses.
    public async Task<List<Course>> GetAllAsync()
    {
        const string sql = @"
            SELECT course_id, course_code, course_name, description
            FROM public.course
            ORDER BY course_code;
        ";

        var courses = new List<Course>();

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            courses.Add(ReadCourse(reader));
        }

        return courses;
    }

    // READ: Get one course using its ID.
    public async Task<Course?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT course_id, course_code, course_name, description
            FROM public.course
            WHERE course_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return ReadCourse(reader);
        }

        return null;
    }

    // CREATE: Add a new course.
    public async Task AddAsync(Course course)
    {
        const string sql = @"
            INSERT INTO public.course
                (course_code, course_name, description)
            VALUES
                (@course_code, @course_name, @description);
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(
            "course_code",
            course.course_code
        );

        command.Parameters.AddWithValue(
            "course_name",
            course.course_name
        );

        command.Parameters.AddWithValue(
            "description",
            (object?)course.description ?? DBNull.Value
        );

        await command.ExecuteNonQueryAsync();
    }

    // UPDATE: Edit an existing course.
    public async Task UpdateAsync(Course course)
    {
        const string sql = @"
            UPDATE public.course
            SET
                course_code = @course_code,
                course_name = @course_name,
                description = @description
            WHERE course_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(
            "course_code",
            course.course_code
        );

        command.Parameters.AddWithValue(
            "course_name",
            course.course_name
        );

        command.Parameters.AddWithValue(
            "description",
            (object?)course.description ?? DBNull.Value
        );

        command.Parameters.AddWithValue(
            "id",
            course.course_id
        );

        await command.ExecuteNonQueryAsync();
    }

    // DELETE: Remove a course using its ID.
    public async Task DeleteAsync(long id)
    {
        const string sql = @"
            DELETE FROM public.course
            WHERE course_id = @id;
        ";

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync();
    }
}
