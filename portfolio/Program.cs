
using Npgsql;
using portfolio.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Existing PostgreSQL connection
var connectionString =
    builder.Configuration.GetConnectionString("portfolio")
    ?? throw new InvalidOperationException(
        "Database connection string was not found.");

builder.Services.AddSingleton(
    NpgsqlDataSource.Create(connectionString));

// Supabase Storage connection
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException(
        "Supabase URL was not found.");

var supabaseKey = builder.Configuration["Supabase:ServiceRoleKey"]
    ?? throw new InvalidOperationException(
        "Supabase service key was not found.");

var supabase = new Supabase.Client(supabaseUrl, supabaseKey);

await supabase.InitializeAsync();

builder.Services.AddSingleton(supabase);

// Existing repositories
builder.Services.AddScoped<CourseRepository>();
builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<ProjectImageRepository>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Project/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Project}/{action=Index}/{id?}");

app.Run();
