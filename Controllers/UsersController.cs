using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;

namespace FinSys.Controllers
{
    [Authorize] // all endpoints protected unless explicitly marked [AllowAnonymous]
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

        // ✅ Admin-only: Get all users
        [HttpGet("all")]
        [Authorize(Roles = "admin")] // role name must match exactly how you issued it in JWT
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var users = await _supabase.GetUsers(); // or GetAllUsers() if you have that
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch users", details = ex.Message });
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
