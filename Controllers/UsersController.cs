using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;  // Added for GetUserIdFromToken

namespace FinSys.Controllers
{
    [Authorize] // All endpoints protected unless explicitly marked [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly SupabaseService _supabase;
        private readonly IWebHostEnvironment _env;

        public UsersController(SupabaseService supabase, IWebHostEnvironment env)
        {
            _supabase = supabase;
            _env = env;
        }

        // Helper to get the user ID from the JWT (for consistency, though not used in admin operations here)
        private string GetUserIdFromToken()
        {
            var userIdString = User.FindFirstValue("id") ?? throw new UnauthorizedAccessException("User ID claim not found.");
            return userIdString;
        }

        // ✅ Admin-only: Get all users
        [HttpGet("all")]
        [Authorize(Roles = "admin")] // Role name must match exactly how you issued it in JWT
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var users = await _supabase.GetUsers(); // Or GetAllUsers() if you have that
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch users", details = ex.Message });
            }
        }

        // ✅ Admin-only: Update a user (including role)
        [HttpPatch("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] User user)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var updated = await _supabase.UpdateUser(id, user);
                if (!updated)
                {
                    return NotFound(new { Message = $"User with ID '{id}' not found." });
                }
                return NoContent(); // 204 No Content on success
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Update failed.", Details = ex.Message });
            }
        }

        // ✅ Admin-only: Delete a user
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var deleted = await _supabase.DeleteUser(id);
                if (!deleted)
                {
                    return NotFound(new { Message = $"User with ID {id} not found." });
                }
                return NoContent(); // 204 No Content on success
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to delete user.", Details = ex.Message });
            }
        }

        // ✅ Public signup endpoint
        [HttpPost]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Post([FromForm] UserSignUpRequest request)
        {
            var user = new User
            {
                Name = request.Name,
                Surname = request.Surname,
                Dob = request.Dob,
                Email = request.Email,
                Address = request.Address,
                Password = request.Password,
                Role = "user"
            };

            try
            {
                var createdUser = await _supabase.AddUser(user);
                return CreatedAtAction(nameof(GetAll), new { id = createdUser.Id }, createdUser);
            }
            catch (HttpRequestException ex)
            {
                return BadRequest(new { Message = "User creation failed at Supabase.", Details = ex.Message });
            }
        }
    }
}
