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

namespace FinSys.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SupabaseService _supabase;
        private readonly string _jwtSecret;

        public AuthController(SupabaseService supabase, IConfiguration configuration)
        {
            _supabase = supabase;
            _jwtSecret = configuration["Jwt:Key"] ??
                         throw new ArgumentNullException("Jwt:Key is missing in configuration.");
        }

        // ✅ Generate JWT Token
        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim("id", user.Id.ToString()),
                new Claim("name", user.Name ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "user") // ✅ Standard claim
            };

            var token = new JwtSecurityToken(
                issuer: "FinSysAPI",
                audience: "FinSysClient",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ✅ POST: /api/Auth/login
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
                        id = user.Id.ToString(),
                        email = user.Email,
                        name = user.Name,
                        surname = user.Surname,
                        role = user.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Login failed due to a server error.", details = ex.Message });
            }
        }

        // ✅ POST: /api/Auth/register
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
                    return Conflict(new { message = "An account with this email already exists." });

                string? photoUrl = null;
                if (request.Photo != null)
                    photoUrl = await _supabase.SaveFile(request.Photo);

                // ✅ Determine role
                var newUserRole = request.Role?.ToLowerInvariant() ?? "user";

                // Prevent normal signups from creating admin accounts
                if (newUserRole == "admin")
                {
                    var authHeader = Request.Headers["Authorization"].ToString();
                    if (string.IsNullOrEmpty(authHeader) || !User.IsInRole("admin"))
                        return Forbid("Only admins can create admin accounts.");
                }

                var newUser = new User
                {
                    Name = request.Name ?? string.Empty,
                    Surname = request.Surname ?? string.Empty,
                    Dob = request.Dob,
                    Email = normalizedEmail,
                    Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address,
                    Password = request.Password ?? string.Empty,
                    PhotoUrl = photoUrl,
                    Role = newUserRole
                };

                var createdUser = await _supabase.AddUser(newUser);

                return CreatedAtAction(nameof(Login), new { email = createdUser.Email }, new
                {
                    success = true,
                    message = "Registration successful. Please log in.",
                    user = new
                    {
                        email = createdUser.Email,
                        id = createdUser.Id.ToString(),
                        role = createdUser.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Registration failed due to a server error.", details = ex.Message });
            }
        }

        // ✅ Example Protected Endpoint (Role-Based)
        [Authorize(Roles = "admin")]
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _supabase.GetAllUsers();
            return Ok(users);
        }

        [Authorize(Roles = "user,admin,manager")]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue("id");
            var user = await _supabase.GetUserById(userId);
            return Ok(user);
        }
    }

    // ✅ DTOs
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
        public string? Role { get; set; } // ✅ Added Role support
    }
}
