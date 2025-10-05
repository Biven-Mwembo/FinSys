// FinSys/Controllers/TransactionsController.cs

using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations; // Not strictly needed here, but left for context
using System.Security.Claims;
using System.Threading.Tasks;

namespace FinSys.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")] // Base route: /api/transactions
    public class TransactionsController : ControllerBase
    {
        private readonly SupabaseService _supabase;

        public TransactionsController(SupabaseService supabase)
        {
            _supabase = supabase;
        }

        // Helper to get the user ID from the JWT and convert it to Guid.
        private Guid GetUserIdFromToken()
        {
            var userIdString = User.FindFirstValue("id") ?? throw new UnauthorizedAccessException("User ID claim not found.");

            if (Guid.TryParse(userIdString, out var userIdGuid))
            {
                return userIdGuid;
            }

            throw new InvalidOperationException("User ID claim is not a valid GUID.");
        }

        // GET: /api/transactions/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetTransactionsByUser(string userId)
        {
            if (!Guid.TryParse(userId, out Guid userIdGuid))
            {
                return BadRequest(new { Message = "Invalid user ID format." });
            }

            var tokenUserId = GetUserIdFromToken();

            // 🔑 PROTECTION: Ensure the requested userId matches the ID in the token
            if (tokenUserId != userIdGuid && !User.IsInRole("Admin"))
            {
                return Forbid("Access to other users' transactions is forbidden.");
            }

            try
            {
                var transactions = await _supabase.GetTransactionsByUser(userId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to fetch user transactions.", Details = ex.Message });
            }
        }

        // ------------------------------------------------------------------
        // POST /api/transactions
        // ------------------------------------------------------------------
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Post([FromForm] TransactionFormRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var secureUserId = GetUserIdFromToken(); // Returns Guid

            try
            {
                string? fileUrl = null;
                if (request.File != null)
                {
                    fileUrl = await _supabase.SaveFile(request.File);
                }

                // 🔑 MODEL FIX: Using FileUrl and UserId (Guid) as defined in the updated Transaction model
                var transaction = new Transaction
                {
                    Date = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    Channel = request.Channel,
                    Motif = request.Motif,
                    FileUrl = fileUrl, // ✅ NOW USES FileUrl
                    UserId = secureUserId // ✅ NOW USES UserId (Guid)
                };

                var createdTransaction = await _supabase.AddTransaction(transaction);

                return CreatedAtAction(
                    nameof(GetTransactionsByUser),
                    new { userId = createdTransaction.UserId }, // UserId is Guid, correctly used here
                    createdTransaction
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Transaction creation failed due to a server error.", Details = ex.Message });
            }
        }

        // ------------------------------------------------------------------
        // ADMIN METHODS
        // ------------------------------------------------------------------

        // 🔑 ADMIN READ: GET: /api/transactions/all
        [HttpGet("all")]
        [Authorize(Roles = "Admin")] // 🛡️ ONLY ADMINS CAN ACCESS THIS
        public async Task<IActionResult> GetAllTransactionsForAdmin()
        {
            try
            {
                var transactions = await _supabase.GetAllTransactionsWithUsers();

                if (transactions == null)
                {
                    return NotFound(new { Message = "No transactions found." });
                }

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to fetch all transactions (Admin access).", Details = ex.Message });
            }
        }


        // 🔑 ADMIN UPDATE: PUT: /api/transactions/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // 🛡️ ONLY ADMINS CAN ACCESS THIS
        // 🔑 DTO FIX: Using TransactionUpdateRequest from FinSys.Models
        public async Task<IActionResult> UpdateTransaction(string id, [FromBody] TransactionUpdateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var updated = await _supabase.UpdateTransaction(id, request);

                if (!updated)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found." });
                }

                return NoContent(); // 204 success, no content to return
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to update transaction (Admin access).", Details = ex.Message });
            }
        }

        // 🔑 ADMIN DELETE: DELETE: /api/transactions/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // 🛡️ ONLY ADMINS CAN ACCESS THIS
        public async Task<IActionResult> DeleteTransaction(string id)
        {
            try
            {
                var deleted = await _supabase.DeleteTransaction(id);

                if (!deleted)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found." });
                }

                return NoContent(); // 204 success, no content to return
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to delete transaction (Admin access).", Details = ex.Message });
            }
        }

        // Deprecated GET: /api/transactions
        [HttpGet]
        [AllowAnonymous] // Allow anyone to see this message
        public IActionResult GetTransactions()
        {
            return BadRequest(new { Message = "Please use /api/transactions/user/{userId} or /api/transactions/all." });
        }
    }
    // DTOs moved to FinSys.Models
}