// FinSys/Controllers/TransactionsController.cs

using FinSys.Models;
using FinSys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations; 
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic; 

namespace FinSys.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly SupabaseService _supabase;

        public TransactionsController(SupabaseService supabase)
        {
            _supabase = supabase;
        }

        // Helper to get the user ID from the JWT. Returns string (e.g., "USER001").
        private string GetUserIdFromToken()
        {
            var userIdString = User.FindFirstValue("id") ?? throw new UnauthorizedAccessException("User ID claim not found.");
            return userIdString;
        }

        // GET: /api/transactions/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetTransactionsByUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "Invalid user ID format." });
            }

            var tokenUserId = GetUserIdFromToken();

            // PROTECTION: Ensure the requested userId matches the ID in the token
            if (tokenUserId != userId && !User.IsInRole("admin"))
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

            var secureUserId = GetUserIdFromToken(); 

            try
            {
                // 1. Handle File Upload
                string? fileUrl = null;
                if (request.File != null)
                {
                    fileUrl = await _supabase.SaveFile(request.File);
                }

                // 2. Determine Transaction Status based on Channel
                var transactionStatus = "Approved"; 
                var responseMessage = "Transaction added successfully.";
                
                if (request.Channel.Equals("Sorties", StringComparison.OrdinalIgnoreCase))
                {
                    transactionStatus = "Pending";
                    responseMessage = "Sortie request sent successfully and is pending Admin approval.";
                }

                // 3. Create Transaction Object with Status
                var transaction = new Transaction
                {
                    Date = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    Channel = request.Channel,
                    Motif = request.Motif,
                    FileUrl = fileUrl,
                    UserId = secureUserId, 
                    Status = transactionStatus
                };

                // 4. Add Transaction (will be Pending or Approved)
                var createdTransaction = await _supabase.AddTransaction(transaction);

                // 5. Return appropriate status/message
                if (transactionStatus == "Pending")
                {
                    // Return 202 Accepted for a request that's pending action
                    return Accepted(new { Message = responseMessage, Transaction = createdTransaction }); 
                }

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
// ADMIN: View all Pending Requests (Sorties awaiting approval)
// ------------------------------------------------------------------
[HttpGet("pending")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> GetPendingTransactions()
{
    try
    {
        var pendingTransactions = await _supabase.GetPendingTransactions();

        if (pendingTransactions == null || pendingTransactions.Count == 0)
        {
            return Ok(new List<object>()); 
        }

        return Ok(pendingTransactions);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { Message = "Failed to fetch pending transactions.", Details = ex.Message });
    }
}

// ------------------------------------------------------------------
// ADMIN: Get ALL user transactions (pending, approved, declined)
// ------------------------------------------------------------------

[HttpGet("all-admin")] 
[Authorize(Roles = "admin")]
public async Task<IActionResult> GetAllTransactions()
{
    try
    {
        var allTransactions = await _supabase.GetAllTransactionsWithUsers();

        if (allTransactions == null || allTransactions.Count == 0)
        {
            return Ok(new List<object>()); 
        }

        return Ok(allTransactions);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { Message = "Failed to fetch all transactions.", Details = ex.Message });
    }
}


        // ------------------------------------------------------------------
        // PRIVILEGED ROLES METHODS (ADMIN/FINANCIER/PASTEUR/VP)
        // ------------------------------------------------------------------

        // PRIVILEGED READ: GET: /api/transactions/all
        [HttpGet("all")]
       [Authorize(Roles = "admin,financier,vice-president,pasteur")] 
        public async Task<IActionResult> GetAllTransactionsForPrivilegedRoles()
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
                return StatusCode(500, new { Message = "Failed to fetch all transactions (Privileged access).", Details = ex.Message });
            }
        }


        // ADMIN UPDATE: PUT: /api/transactions/{id}
        [HttpPatch("item/{id}")]// ⭐ CHANGED FROM [HttpPut] TO [HttpPatch] ⭐
[Authorize(Roles = "admin")] 
public async Task<IActionResult> UpdateTransaction(string id, [FromBody] TransactionUpdateRequest request)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);

    try
    {
        var updated = await _supabase.UpdateTransaction(id, request);

        if (!updated)
        {
            return NotFound(new { Message = $"Transaction with ID '{id}' not found. Verify it exists in the database." });
        }

        return NoContent(); 
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return NotFound(new { Message = $"Transaction ID '{id}' does not exist in Supabase." });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { Message = "Update failed.", Details = ex.Message });
    }
}

        // ADMIN: Approve or Reject Pending Transactions
        [HttpPut("item/{id}/approved")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ApproveTransaction(string id)
        {
            try
            {
                var transaction = await _supabase.GetTransactionById(id);
                if (transaction == null)
                    return NotFound(new { Message = "Transaction not found." });

                if (transaction.Status == "Approved")
                    return BadRequest(new { Message = "Transaction is already approved." });

                transaction.Status = "Approved";
                await _supabase.UpdateTransactionStatus(id, "Approved"); 

                return Ok(new { Message = "Transaction approved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to approve transaction.", Details = ex.Message });
            }
        }
        [HttpPut("item/{id}/declined")]
        [Authorize(Roles = "admin")]
      
public async Task<IActionResult> ApproveTransaction(string id)
{
    try
    {
        var transaction = await _supabase.GetTransactionById(id);
        if (transaction == null)
            return NotFound(new { Message = "Transaction not found." });

        if (transaction.Status == "Approved")
            return BadRequest(new { Message = "Transaction is already approved." });

        transaction.Status = "Approved";
        await _supabase.UpdateTransactionStatus(id, "Approved");

        return Ok(new { Message = "Transaction approved successfully." });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            Message = "Failed to approve transaction.",
            Details = ex.Message
        });
    }
}



        // ADMIN DELETE: DELETE: /api/transactions/{id}
        [HttpDelete("item/{id}")]
        [Authorize(Roles = "admin")] 
        public async Task<IActionResult> DeleteTransaction(string id)
        {
            try
            {
                var deleted = await _supabase.DeleteTransaction(id);

                if (!deleted)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found." });
                }

                return NoContent(); 
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to delete transaction (Admin access).", Details = ex.Message });
            }
        }

        // SINGLE ITEM GET
       [HttpGet("item/{id}")]
        public async Task<IActionResult> GetTransactionById(string id)
        {
            // Remove Guid check
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { Message = "Invalid transaction ID format." });
            }

            try
            {
                var transaction = await _supabase.GetTransactionById(id);

                if (transaction == null)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found." });
                }

                // PROTECTION: Ensure the requested transaction belongs to the user, or the user is Admin
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
        [AllowAnonymous] 
        public IActionResult GetTransactions()
        {
            return BadRequest(new { Message = "Please use /api/transactions/user/{userId} or /api/transactions/all." });
        }
    }
}
