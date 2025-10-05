using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using FinSys.Services;
using Microsoft.AspNetCore.Hosting; // 🔑 REQUIRED: Needed for IWebHostEnvironment

var builder = WebApplication.CreateBuilder(args);

// Define the CORS Policy Name
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// ------------------------------------------------------------------
// ⭐ Add CORS Services (Step 1 of 2)
// ------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
              policy =>
              {
                  
                  // with the exact URL of your React frontend app.
                  policy.WithOrigins("https://rouah.netlify.app")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
              });
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ------------------------------------------------------------------
// 🔑 CRITICAL FIX: Update SupabaseService Registration
// ------------------------------------------------------------------
// The new SupabaseService constructor requires IWebHostEnvironment (IWebHostEnvironment env).
// This explicit registration ensures the DI container retrieves 'env' and passes it.
builder.Services.AddSingleton<SupabaseService>(serviceProvider =>
{
    // Retrieve the necessary services from the service provider
    var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();

    // Create and return the SupabaseService instance
    return new SupabaseService(env);
});

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_12345";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FinSysAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FinSysClient"; // Use audience from config too

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false, // Keep false to match your initial AuthController setup
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        // If ValidateAudience were true, it would be: ValidAudience = jwtAudience
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

        // 🔑 CRITICAL FIX: Tell the validator to look for roles in the standard ClaimTypes.Role
        RoleClaimType = ClaimTypes.Role
    };

    // 🔑 ADDED: Log authentication failures for debugging the 401 error
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"[JWT Error] Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        }
    };
});

// 🔑 ADDED: Add Authorization services (needed for [Authorize])
builder.Services.AddAuthorization();


var app = builder.Build();

// Enable Static Files middleware
// 🔑 REQUIRED: This must be present to serve files from the wwwroot/uploads folder
app.UseStaticFiles();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ------------------------------------------------------------------
// ⭐ Middleware Setup
// ------------------------------------------------------------------
// CORS MUST be configured before UseAuthentication/UseAuthorization
app.UseCors(MyAllowSpecificOrigins);

// Enable authentication & authorization (Order is critical!)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
