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
        public Guid Id { get; set; }

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
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid Id { get; set; }

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

        // ----------------------------------------------------------------------
        // Foreign Key ID
        // This maps to the 'user_id' column in the database and is used when POSTing/PATCHing.
        // On GET, Supabase returns the raw 'user_id' key, which is handled here.
        [Required]
        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }

        [JsonIgnore] // 💥 THIS IS THE FIX 💥
        [JsonPropertyName("UserDetails")]
        public JoinedUser? UserDetails { get; set; }

        // ----------------------------------------------------------------------
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
    public class TransactionUpdateRequest
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Motif { get; set; } = string.Empty;
    }
}