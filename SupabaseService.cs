// FinSys/Services/SupabaseService.cs
using FinSys.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinSys.Services
{
    public class SupabaseService
    {
        // YOUR Supabase REST URL and API key (kept in-file as requested)
        private readonly string _baseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/rest/v1";
        private readonly string _authBaseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/auth/v1";
        private readonly string _apiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZ5YWxibnhyeGxoaW5kbGRlemhxIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc1ODkxNzE3NywiZXhwIjoyMDc0NDkzMTc3fQ.P8BIaA4uCvxdTCRqIhIEW0Ti1uxNgpZxu0aOXbcoM8E";

        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _env;

        public SupabaseService(IWebHostEnvironment env)
        {
            _env = env;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Clear();

            // Required headers for Supabase REST usage
            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // -------------------------------
        // Helper: Build a properly-quoted + encoded PostgREST filter for a string PK
        // Example result for id = TR067 -> id=eq.%27TR067%27
        // -------------------------------
        private static string BuildQuotedFilter(string rawValue)
        {
            // Wrap in single quotes then URI-encode the whole quoted string
            var quoted = $"'{rawValue}'";
            return Uri.EscapeDataString(quoted);
        }

        // ------------------------------------------------------------------
        // CORE FILE SAVING
        // ------------------------------------------------------------------
        public async Task<string> SaveFile(IFormFile file)
        {
            if (file == null) return string.Empty;
            var folder = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{fileName}";
        }

        // ------------------------------------------------------------------
        // TRANSACTION: Update entire record fields (admin)
        // ------------------------------------------------------------------
        public async Task<bool> UpdateTransaction(string id, TransactionUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Build a proper PostgREST filter: id=eq.'TR067' (quotes encoded)
            var encodedQuotedId = BuildQuotedFilter(id);
            var requestUrl = $"{_baseUrl}/transactions?id=eq.{encodedQuotedId}";

            var requestMessage = new HttpRequestMessage(HttpMethod.Patch, requestUrl);
            requestMessage.Content = content;
            // Ask for representation so Supabase will return the updated row(s) on success
            requestMessage.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(requestMessage);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[UpdateTransaction] URL: {requestUrl}");
            Console.WriteLine($"[UpdateTransaction] Status: {response.StatusCode}, Body: {json}");

            // Success (2xx) but body might be [] (means 0 rows updated) — treat as not found
            if (response.IsSuccessStatusCode)
            {
                var trimmed = json?.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed == "[]")
                {
                    return false; // no rows affected / not found / blocked by RLS
                }
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // TRANSACTION: Update only status (approve/reject)
        // ------------------------------------------------------------------
        public async Task<bool> UpdateTransactionStatus(string transactionId, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return false;

            var updateData = new Dictionary<string, object?>
            {
                ["status"] = newStatus,
                ["updated_at"] = DateTime.UtcNow
            };

            var jsonContent = JsonSerializer.Serialize(updateData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var encodedQuotedId = BuildQuotedFilter(transactionId);
            var requestUrl = $"{_baseUrl}/transactions?id=eq.{encodedQuotedId}";

            var request = new HttpRequestMessage(HttpMethod.Patch, requestUrl);
            request.Content = content;
            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[UpdateTransactionStatus] URL: {requestUrl}");
            Console.WriteLine($"[UpdateTransactionStatus] Status: {response.StatusCode}, Body: {json}");

            if (response.IsSuccessStatusCode)
            {
                var trimmed = json?.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed == "[]")
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // TRANSACTION: Delete
        // ------------------------------------------------------------------
        public async Task<bool> DeleteTransaction(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var encodedQuotedId = BuildQuotedFilter(id);
            var requestUrl = $"{_baseUrl}/transactions?id=eq.{encodedQuotedId}";

            var response = await _httpClient.DeleteAsync(requestUrl);
            Console.WriteLine($"[DeleteTransaction] URL: {requestUrl}, Status: {response.StatusCode}");

            return response.IsSuccessStatusCode;
        }

        // ------------------------------------------------------------------
        // TRANSACTION: Get by ID
        // ------------------------------------------------------------------
        public async Task<Transaction?> GetTransactionById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var selectQuery = "*,user:users(name,surname,email)";
            var encodedQuotedId = BuildQuotedFilter(id);
            var requestUrl = $"{_baseUrl}/transactions?id=eq.{encodedQuotedId}&select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetTransactionById] URL: {requestUrl}");
            Console.WriteLine($"[GetTransactionById] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch transaction by ID. Status: {response.StatusCode}. Body: {json}");
            }

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions?.FirstOrDefault();
        }

        // ------------------------------------------------------------------
        // TRANSACTIONS: Get pending / all / by user
        // ------------------------------------------------------------------
        public async Task<List<Transaction>> GetPendingTransactions()
        {
            var selectQuery = "*,user:users(name,surname,email)";
            var requestUrl = $"{_baseUrl}/transactions?status=eq.Pending&select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetPendingTransactions] URL: {requestUrl}");
            Console.WriteLine($"[GetPendingTransactions] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch pending transactions. Status: {response.StatusCode}, Response: {json}");
            }

            return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();
        }

        public async Task<List<Transaction>> GetAllTransactionsWithUsers()
        {
            var selectQuery = "*,user:users(name,surname,email)";
            var requestUrl = $"{_baseUrl}/transactions?select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetAllTransactionsWithUsers] URL: {requestUrl}");
            Console.WriteLine($"[GetAllTransactionsWithUsers] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch all transactions (Admin). Status: {response.StatusCode}. Response: {json}");
            }

            return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();
        }

        public async Task<List<Transaction>> GetTransactionsByUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new List<Transaction>();

            var selectQuery = "*,user:users(name,surname,email)";
            var encodedQuotedUserId = BuildQuotedFilter(userId);
            var requestUrl = $"{_baseUrl}/transactions?user_id=eq.{encodedQuotedUserId}&select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetTransactionsByUser] URL: {requestUrl}");
            Console.WriteLine($"[GetTransactionsByUser] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Supabase query failed for user {userId}. Status: {response.StatusCode}. Response: {json}");
            }

            return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();
        }

        // ------------------------------------------------------------------
        // TRANSACTION: Create
        // ------------------------------------------------------------------
        public async Task<Transaction> AddTransaction(Transaction transaction)
        {
            transaction.Id = null;

            var jsonContent = JsonSerializer.Serialize(transaction);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transactions")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[AddTransaction] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Supabase Post failed: {response.StatusCode}. Response: {json}");

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions?[0] ?? transaction;
        }

        // ------------------------------------------------------------------
        // USER-related helpers (with quoted filters for string values)
        // ------------------------------------------------------------------
        public async Task<List<User>> GetUsers()
        {
            var selectQuery = "*, role";
            var requestUrl = $"{_baseUrl}/users?select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetUsers] URL: {requestUrl}");
            Console.WriteLine($"[GetUsers] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch users. Status: {response.StatusCode}, Response: {json}");
            }

            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }

        public async Task<List<User>> GetAllUsers()
        {
            var selectQuery = "*, role";
            var requestUrl = $"{_baseUrl}/users?select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetAllUsers] URL: {requestUrl}");
            Console.WriteLine($"[GetAllUsers] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch all users. Status: {response.StatusCode}, Response: {json}");
            }

            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }

        public async Task<User?> GetUserById(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var selectQuery = "*, role";
            var encodedQuotedUserId = BuildQuotedFilter(userId);
            var requestUrl = $"{_baseUrl}/users?id=eq.{encodedQuotedUserId}&select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetUserById] URL: {requestUrl}");
            Console.WriteLine($"[GetUserById] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch user by ID. Status: {response.StatusCode}, Response: {json}");
            }

            var users = JsonSerializer.Deserialize<List<User>>(json);
            return users?.FirstOrDefault();
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var selectQuery = "*, role";
            var encodedQuotedEmail = BuildQuotedFilter(email);
            var requestUrl = $"{_baseUrl}/users?email=eq.{encodedQuotedEmail}&select={selectQuery}";

            var response = await _httpClient.GetAsync(requestUrl);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetUserByEmail] URL: {requestUrl}");
            Console.WriteLine($"[GetUserByEmail] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch user by email. Status: {response.StatusCode}, Response: {json}");
            }

            var users = JsonSerializer.Deserialize<List<User>>(json);
            return users?.FirstOrDefault();
        }

        public async Task<User> AddUser(User user)
        {
            var jsonContent = JsonSerializer.Serialize(user);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/users")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[AddUser] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Supabase Post failed: {response.StatusCode}. Response: {json}");

            var users = JsonSerializer.Deserialize<List<User>>(json);
            return users?[0] ?? user;
        }

        // --- Simple auth helper (existing)
        public async Task<User?> SimpleLoginAsync(string email, string password)
        {
            var user = await GetUserByEmail(email);

            if (user == null || string.IsNullOrWhiteSpace(user.Password))
            {
                return null;
            }

            if (user.Password == password)
            {
                return user;
            }
            return null;
        }
    }
}
