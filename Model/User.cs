// FinSys/Models/Transaction.cs

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Security.Claims;

namespace FinSys.Models
{
    // ----------------------------------------------------
    // User Model
    // ----------------------------------------------------
    public class User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("surname")]
        public string Surname { get; set; } = string.Empty;

        [JsonPropertyName("dob")]
        public DateTime? Dob { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("photo")]
        public string? PhotoUrl { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";
    }

    public class Transaction
    {
        // FIX: Changed default to null and used WhenWritingNull.
        // This ensures the ID is not sent on POST, forcing the database to use its DEFAULT value.
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; } = null;

        // DB Column: date
        [Required]
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        // DB Column: amount
        [Required]
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        // DB Column: currency
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        // DB Column: channel
        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        // DB Column: motif
        [JsonPropertyName("motif")]
        public string Motif { get; set; } = string.Empty;

        // DB Column: file_url
        [JsonPropertyName("file_url")]
        public string? FileUrl { get; set; }

        // DB Column: status
        [JsonPropertyName("status")]
        public string Status { get; set; } = "Pending";

        // Foreign Key ID
        [Required]
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("UserDetails")]
        public JoinedUser? UserDetails { get; set; }
    }

    public class JoinedUser
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("surname")]
        public string Surname { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    // ----------------------------------------------------
    // TransactionFormRequest DTO (for ASP.NET Core Form Binding)
    // ----------------------------------------------------
    public class TransactionFormRequest
    {
        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
        public decimal Amount { get; set; }

        [Required]
        public string Currency { get; set; } = string.Empty;

        [Required]
        public string Channel { get; set; } = string.Empty;

        public string Motif { get; set; } = string.Empty;

        public IFormFile? File { get; set; }
    }

    // DTO for Admin Update Request
  // DTO for Admin Update Request
public class TransactionUpdateRequest
{
    [JsonPropertyName("date")]  // Forces "date" in JSON (matches DB column)
    public DateTime Date { get; set; }

    [JsonPropertyName("amount")]  // Forces "amount" in JSON (matches DB column)
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]  // Forces "currency" in JSON (matches DB column)
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("channel")]  // Forces "channel" in JSON (matches DB column)
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("motif")]  // Forces "motif" in JSON (matches DB column)
    public string Motif { get; set; } = string.Empty;

    [JsonPropertyName("file_url")]  // Forces "file_url" in JSON (matches DB column; note the underscore)
    public string? FileUrl { get; set; }  // Keep the property name as-is for C# consistency
}

}
