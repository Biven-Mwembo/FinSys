using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SupabaseService _supabase;
    // 🔑 FIX 1: Use a clean, non-multi-line, long key from configuration
    private readonly string _jwtSecret;

    // 🔑 FIX 2: Added IConfiguration to read the secret key
    public AuthController(SupabaseService supabase, IConfiguration configuration)
    {
        _supabase = supabase;
        // Read the secret key from the configuration (appsettings.json)
        _jwtSecret = configuration["Jwt:Key"] ??
                     throw new ArgumentNullException("Jwt:Key is missing in configuration.");
    }

    // Generate JWT Token
    private string GenerateJwtToken(User user)
    {
        // Use the key read from configuration
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim("id", user.Id.ToString()),
            new Claim("name", user.Name ?? ""),
            
            // 🔑 FIX 3: Use the standard ClaimTypes.Role for authorization to work
            new Claim(ClaimTypes.Role, user.Role)
            
            // Optional: If you need a custom 'role' claim for older/non-standard clients, 
            // you can include both, but ClaimTypes.Role is standard for C# authorization.
            // new Claim("role", user.Role) 
        };

        var token = new JwtSecurityToken(
            // Use config values instead of hardcoding for consistency
            issuer: "FinSysAPI",
            audience: "FinSysClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // POST: /api/Auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _supabase.SimpleLoginAsync(normalizedEmail, request.Password);

            if (user == null)
                return Unauthorized(new { message = "Invalid email or password" });

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                success = true,
                token,
                user = new
                {
                    email = user.Email,
                    id = user.Id.ToString(),
                    name = user.Name,
                    surname = user.Surname,
                    role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Login failed due to a server error", details = ex.Message });
        }
    }

    // ... (Register method remains unchanged) ...
    // Note: The rest of the AuthController (Register) is unchanged, but included below for completeness.

    // POST: /api/Auth/register (Sign Up)
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] UserSignUpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _supabase.GetUserByEmail(normalizedEmail);
            if (existingUser != null)
                return Conflict(new { message = "Registration failed. An account with this email already exists." });

            string? photoUrl = null;
            if (request.Photo != null)
                photoUrl = await _supabase.SaveFile(request.Photo);

            var newUser = new User
            {
                Name = request.Name ?? string.Empty,
                Surname = request.Surname ?? string.Empty,
                Dob = request.Dob,
                Email = normalizedEmail,
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address,
                Password = request.Password ?? string.Empty,
                PhotoUrl = photoUrl,
                Role = "user"
            };

            var createdUser = await _supabase.AddUser(newUser);

            return CreatedAtAction(nameof(Login), new { email = createdUser.Email }, new
            {
                success = true,
                message = "Registration successful. Please log in.",
                user = new { email = createdUser.Email, id = createdUser.Id.ToString() }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Registration failed due to a server error.", details = ex.Message });
        }
    }
}
// DTOs for AuthController (No changes needed here)
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserSignUpRequest
{
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public DateTime Dob { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Address { get; set; }
    public IFormFile? Photo { get; set; }
    public string Password { get; set; } = string.Empty;
}