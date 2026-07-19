using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using InvoicePK.Data;
using InvoicePK.Helpers;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────
// Uses PostgreSQL in production (Railway), SQL Server locally
var connectionString = builder.Configuration.GetConnectionString("Default")!;
var isProduction = builder.Environment.IsProduction();

if (isProduction)
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
else
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// ── JWT Auth ──────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<InvoicePK.Services.InvoiceNumberService>();
builder.Services.AddScoped<InvoicePK.Services.PdfService>();
builder.Services.AddScoped<InvoicePK.Services.EmailService>();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// ── CORS (for React frontend) ─────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            "http://localhost:5173",
            "https://invoicepk-frontend.vercel.app"  // update with your actual Vercel URL
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ── Swagger ───────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "InvoicePK API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter: Bearer {token}",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" }},
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Auto-create tables on startup ─────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Middleware Pipeline ───────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
