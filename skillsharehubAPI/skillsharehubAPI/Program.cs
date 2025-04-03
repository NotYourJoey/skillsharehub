using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using skillsharehubAPI.Data;
using skillsharehubAPI.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Get token from configuration with null check
var tokenValue = builder.Configuration.GetSection("AppSettings:Token").Value;

// Ensure token is not null
if (string.IsNullOrEmpty(tokenValue))
{
    throw new InvalidOperationException("JWT Token is not configured. Please add a valid token to AppSettings:Token in appsettings.json");
}

// Add authentication with non-null token
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenValue)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Register helpers
builder.Services.AddScoped<AuthHelper>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create upload directories
string webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRootPath))
{
    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    Directory.CreateDirectory(webRootPath);
}

string uploadsPath = Path.Combine(webRootPath, "uploads");
string profilesPath = Path.Combine(uploadsPath, "profiles");
string postsPath = Path.Combine(uploadsPath, "posts");

Directory.CreateDirectory(uploadsPath);
Directory.CreateDirectory(profilesPath);
Directory.CreateDirectory(postsPath);

app.Run();
