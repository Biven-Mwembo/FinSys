using Microsoft.AspNetCore.Mvc;

using System;

using System.IO;

using System.Threading.Tasks;

using FinSys.Models;

using FinSys.Services;

using Microsoft.AspNetCore.Hosting;

using System.Net.Http;

using Microsoft.AspNetCore.Authorization; // 🔑 Add this using directive



namespace FinSys.Controllers

{

    // 🔑 Add the Authorize attribute here to protect the controller

    [Authorize]

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



        // 🔑 ADMIN READ: Only allows users with the "Admin" role to call this

        [HttpGet]

        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Get() => Ok(await _supabase.GetUsers());



        [HttpPost]

        // 🔑 Allow public access since this is the Sign Up endpoint

        [AllowAnonymous]

        [Consumes("multipart/form-data")]

        public async Task<IActionResult> Post([FromForm] UserSignUpRequest request)

        {

            // ... (The rest of your existing Post logic remains here) ...



            // 1. Map DTO fields to the database model

            var user = new User

            {

                Name = request.Name,

                Surname = request.Surname,

                Dob = request.Dob,

                Email = request.Email,

                Address = request.Address,

                Password = request.Password

                // NOTE: Role is defaulted to "user" in the User model or AuthController logic

            };



            // ... (Photo upload logic) ...



            try

            {

                var createdUser = await _supabase.AddUser(user);

                // 🔑 NOTE: The frontend SignUp component is expecting a redirect to Login or success message, 

                // so you might want to return an Ok or Created response without full user data.

                return CreatedAtAction(nameof(Get), new { id = createdUser.Id }, createdUser);

            }

            catch (HttpRequestException ex)

            {

                return BadRequest(new { Message = "User creation failed at Supabase.", Details = ex.Message });

            }

        }

    }

    // ... (Your TransactionRequest DTOs are usually in another file, but are omitted here for brevity)

}
