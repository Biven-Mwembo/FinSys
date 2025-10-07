using FinSys.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System; // Required for Console.WriteLine and System.Net.Http.HttpRequestException
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
        private readonly string _baseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/rest/v1";
        private readonly string _authBaseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/auth/v1";

        // 🚨 CRITICAL SECURITY FIX: Use the Public Anon Key for general service config
        private readonly string _publicApiKey = "YOUR_SUPABASE_PUBLIC_ANON_KEY"; 
        
        // 🔑 Keep the Service Role Key for Admin operations.
        private readonly string _serviceRoleKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZ5YWxibnhyeGxoaW5kbGRlemhxIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc1ODkxNzE3NywiZXhwIjoyMDc0NDkzMTc3fQ.P8BIaA4uCvxdTCRqIhIEW0Ti1uxNgpZxu0aOXbcoM8E";
        
        private readonly IWebHostEnvironment _env;

        public SupabaseService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Helper to get an HttpClient configured for **Standard User** (RLS-enabled) access
        // The token must be passed from the controller, which extracts it from the request.
        private HttpClient GetClientForUser(string userJwt)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("apikey", _publicApiKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userJwt); 
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        // Helper to get an HttpClient configured for **Admin/Service Role** access
        private HttpClient GetClientForAdmin()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("apikey", _serviceRoleKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey); 
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        // Helper for anonymous access (e.g., Sign Up)
        private HttpClient GetClientForAnon()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("apikey", _publicApiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        // ------------------------------------------------------------------
        // CORE FILE SAVING (Restored)
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
        // USER CRUD METHODS 
        // ------------------------------------------------------------------

        // 🔑 GetUsers is for Admin only (uses Admin Client)
        public async Task<List<User>> GetUsers()
        {
            var adminClient = GetClientForAdmin();
            var selectQuery = "*, role";

            var response = await adminClient.GetAsync($"{_baseUrl}/users?select={selectQuery}");
            var json = await response.Content.ReadAsStringAsync();
            System.Console.WriteLine($"[GetUsers] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to fetch users. Status: {response.StatusCode}, Response: {json}"
                );
            }

            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }

        // 🔑 AddUser is for Sign Up (uses Anonymous Client)
        public async Task<User> AddUser(User user)
        {
            var anonClient = GetClientForAnon();

            var userToCreate = new Dictionary<string, object?>()
            {
                ["name"] = user.Name,
                ["surname"] = user.Surname,
                ["dob"] = user.Dob,
                ["email"] = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
                ["address"] = string.IsNullOrWhiteSpace(user.Address) ? null : user.Address,
                ["photo"] = string.IsNullOrWhiteSpace(user.PhotoUrl) ? null : user.PhotoUrl,
                ["password"] = user.Password,
                ["role"] = user.Role
            };

            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(userToCreate, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/users");
            request.Content = content;
            request.Headers.Add("Prefer", "return=representation");

            var response = await anonClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            System.Console.WriteLine($"[AddUser] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Supabase User Add failed ({response.StatusCode}). Response: {json}",
                    null,
                    response.StatusCode
                );
            }

            var users = JsonSerializer.Deserialize<List<User>>(json, jsonOptions);
            return users?[0] ?? user;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            // Uses Admin Client to reliably check login credentials/roles
            var adminClient = GetClientForAdmin(); 
            var selectQuery = "*, role";

            var response = await adminClient.GetAsync($"{_baseUrl}/users?email=eq.{email}&select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[GetUserByEmail] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to fetch user by email. Status: {response.StatusCode}, Response: {json}"
                );
            }

            var users = JsonSerializer.Deserialize<List<User>>(json);
            return users?.FirstOrDefault();
        }

        // --- AUTHENTICATION METHODS (UNCHANGED LOGIC) ---

        public async Task<User?> SimpleLoginAsync(string email, string password)
        {
            var user = await GetUserByEmail(email);
            if (user == null || string.IsNullOrWhiteSpace(user.Password)) return null;
            if (user.Password == password) return user;
            return null;
        }

        // ------------------------------------------------------------------
        // TRANSACTION CRUD METHODS (FIXED _httpClient USAGE)
        // ------------------------------------------------------------------

        public async Task<Transaction> AddTransaction(Transaction transaction)
        {
            // You should update your controller to pass the JWT and use GetClientForUser().
            // For now, we use the anonymous client to avoid the original _httpClient error.
            var anonClient = GetClientForAnon(); 
            
            var jsonContent = JsonSerializer.Serialize(transaction);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transactions");
            request.Content = content;
            request.Headers.Add("Prefer", "return=representation");
            
            var response = await anonClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Supabase Post failed: {response.StatusCode}. Response: {json}");
            
            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions?[0] ?? transaction;
        }

        public async Task<List<Transaction>> GetTransactionsByUser(string userId, string userJwt)
        {
            var userClient = GetClientForUser(userJwt);
            var selectQuery = "*,UserDetails:users(name,surname,email)";
            
            var response = await userClient.GetAsync($"{_baseUrl}/transactions?user_id=eq.{userId}&select={selectQuery}");
            var json = await response.Content.ReadAsStringAsync();

            System.Console.WriteLine($"[GetTransactionsByUser] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Supabase query failed for user {userId}. Status: {response.StatusCode}. Response: {json}",
                    null,
                    response.StatusCode
                );
            }

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions ?? new List<Transaction>();
        }

        // 🔑 GetAllTransactionsWithUsers is for Admin only (uses Admin Client)
        public async Task<List<Transaction>> GetAllTransactionsWithUsers()
        {
            var adminClient = GetClientForAdmin();
            var selectQuery = "*,UserDetails:users(name,surname,email)";

            var response = await adminClient.GetAsync($"{_baseUrl}/transactions?select={selectQuery}");
            var json = await response.Content.ReadAsStringAsync();

            System.Console.WriteLine($"[GetAllTransactionsWithUsers] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to fetch all transactions (Admin). Status: {response.StatusCode}. Supabase Response: {json}",
                    null,
                    response.StatusCode
                );
            }

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions ?? new List<Transaction>();
        }

        // 🔑 GetTransactionById needs a JWT, assuming Admin for simplicity here.
        public async Task<Transaction?> GetTransactionById(string id)
        {
            var adminClient = GetClientForAdmin();
            var selectQuery = "*,UserDetails:users(name,surname,email)";

            var response = await adminClient.GetAsync($"{_baseUrl}/transactions?id=eq.{id}&select={selectQuery}");
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetTransactionById] Status: {response.StatusCode}, Body: {json}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions?.FirstOrDefault();
        }

        // 🔑 GetTransactions - assuming this is a simple fetch, needs a JWT. Using Admin client to avoid error.
        public async Task<List<Transaction>> GetTransactions()
        {
             var adminClient = GetClientForAdmin();
             var response = await adminClient.GetAsync($"{_baseUrl}/transactions");
             var json = await response.Content.ReadAsStringAsync();
             response.EnsureSuccessStatusCode();
             return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();
        }

        // 🔑 UpdateTransaction - needs a JWT. Using Admin client to avoid error.
        public async Task<bool> UpdateTransaction(string id, TransactionUpdateRequest request)
        {
            var adminClient = GetClientForAdmin();
            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/transactions?id=eq.{id}");
            requestMessage.Content = content;
            requestMessage.Headers.Add("Prefer", "return=representation");

            var response = await adminClient.SendAsync(requestMessage);
            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[UpdateTransaction] Status: {response.StatusCode}, Body: {json}");

            return response.IsSuccessStatusCode;
        }

        // 🔑 DeleteTransaction - needs a JWT. Using Admin client to avoid error.
        public async Task<bool> DeleteTransaction(string id)
        {
            var adminClient = GetClientForAdmin();
            var response = await adminClient.DeleteAsync($"{_baseUrl}/transactions?id=eq.{id}");

            Console.WriteLine($"[DeleteTransaction] Status: {response.StatusCode}");

            return response.IsSuccessStatusCode;
        }
    }
}
