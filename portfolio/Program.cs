using Npgsql;
using portfolio.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Account authentication (cookie login for the editor)
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
    });

// PostgreSQL connection (used by the repositories)
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

var supabase = new Supabase.Client(
    supabaseUrl,
    supabaseKey,
    new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = false
    });

await supabase.InitializeAsync();

builder.Services.AddSingleton(supabase);

// Repositories
builder.Services.AddHttpClient<ImageStorageService>();
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

app.UseAuthentication();   // reads the login cookie (must come first)
app.UseAuthorization();    // enforces [Authorize]

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Project}/{action=Index}/{id?}");

app.Run();