using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
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

        // Helper to get the user ID from the JWT
        private string GetUserIdStringFromToken() =>
            User.FindFirstValue("id") ?? throw new UnauthorizedAccessException("User ID claim not found.");

        // Helper to get the user ID (as Guid) from the JWT
        private Guid GetUserIdFromToken()
        {
            var userIdString = GetUserIdStringFromToken();

            if (Guid.TryParse(userIdString, out var userIdGuid))
            {
                return userIdGuid;
            }

            throw new InvalidOperationException("User ID claim is not a valid GUID.");
        }

        // 🔑 NEW HELPER: Get the raw JWT from the request Authorization header.
        private string GetUserJwtFromToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                // This shouldn't happen under [Authorize], but it's a good fail-safe.
                throw new UnauthorizedAccessException("Authorization token is missing or malformed.");
            }
            // Strip "Bearer " prefix
            return authHeader.Substring("Bearer ".Length).Trim();
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

            // 🐛 FIX 1: Retrieve the JWT and pass it to the service method
            string userJwt = GetUserJwtFromToken();

            try
            {
                var transactions = await _supabase.GetTransactionsByUser(userId, userJwt);
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
            // You should also get the JWT here to pass to AddTransaction if RLS is enforced on insert
            // string userJwt = GetUserJwtFromToken(); 

            try
            {
                string? fileUrl = null;
                if (request.File != null)
                {
                    fileUrl = await _supabase.SaveFile(request.File);
                }

                var transaction = new Transaction
                {
                    Date = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    Channel = request.Channel,
                    Motif = request.Motif,
                    FileUrl = fileUrl,
                    UserId = secureUserId
                };

                // NOTE: AddTransaction needs a JWT argument if you update the SupabaseService method signature
                var createdTransaction = await _supabase.AddTransaction(transaction);

                return CreatedAtAction(
                    nameof(GetTransactionsByUser),
                    new { userId = createdTransaction.UserId },
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllTransactionsForAdmin()
        {
            try
            {
                // This call uses the internal service role key and doesn't need a JWT
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
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTransaction(Guid id, [FromBody] TransactionUpdateRequest request)
        {
            if (request == null)
                return BadRequest("Invalid transaction data.");

            try
            {
                // UpdateTransaction needs a JWT argument if you update the SupabaseService method signature
                var updated = await _supabase.UpdateTransaction(id.ToString(), request);

                if (!updated)
                    return NotFound($"Transaction with ID {id} not found.");

                return Ok(new { Message = $"Transaction {id} updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error updating transaction.", Details = ex.Message });
            }
        }

        // 🔑 ADMIN DELETE: DELETE: /api/transactions/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTransaction(string id)
        {
            try
            {
                // DeleteTransaction needs a JWT argument if you update the SupabaseService method signature
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

        // ------------------------------------------------------------------
        // SINGLE ITEM GET
        // ------------------------------------------------------------------
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransactionById(string id)
        {
            if (!Guid.TryParse(id, out Guid transactionIdGuid))
            {
                return BadRequest(new { Message = "Invalid transaction ID format." });
            }

            // You should get the JWT here to pass to GetTransactionById
            // If the SupabaseService method requires it.
            // string userJwt = GetUserJwtFromToken();

            try
            {
                // 🛑 NOTE: The current SupabaseService.GetTransactionById uses the Admin key,
                // but for a secure app, it should use the user's JWT.
                var transaction = await _supabase.GetTransactionById(id);

                if (transaction == null)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found." });
                }

                // 🔑 PROTECTION: Ensure the requested transaction belongs to the user, or the user is Admin
                var tokenUserId = GetUserIdFromToken();
                if (tokenUserId != transaction.UserId && !User.IsInRole("Admin"))
                {
                    return Forbid("Access to this transaction is forbidden.");
                }

                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to fetch transaction.", Details = ex.Message });
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
