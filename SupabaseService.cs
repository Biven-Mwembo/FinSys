using FinSys.Controllers;

using FinSys.Models;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Hosting; // REQUIRED for IWebHostEnvironment

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

        private readonly string _baseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/rest/v1";

        private readonly string _authBaseUrl = "https://vyalbnxrxlhindldezhq.supabase.co/auth/v1";

        // ⚠️ CRITICAL NOTE: Using service_role key for all requests is DANGEROUS and bypasses RLS.

        // For production, you must use the standard public key and pass the user's JWT 

        // in the Authorization header for transaction-related calls.

        private readonly string _apiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZ5YWxibnhyeGxoaW5kbGRlemhxIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTg5MTcxNzcsImV4cCI6MjA3NDQ5MzE3N30.khe9gkuYTBnb50d6SMtoJkqbKU8NKzIJ-j2Pd7_yDHE";

        private readonly HttpClient _httpClient;

        private readonly IWebHostEnvironment _env;



        public SupabaseService(IWebHostEnvironment env)

        {

            _env = env;

            _httpClient = new HttpClient();

            _httpClient.DefaultRequestHeaders.Clear();



            // Set the headers needed for Supabase access

            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);

            _httpClient.DefaultRequestHeaders.Authorization =

                new AuthenticationHeaderValue("Bearer", _apiKey); // Using service_role key

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        }



        // ------------------------------------------------------------------

        // CORE FILE SAVING (Keep as-is)

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

        public async Task<bool> UpdateTransactionStatus(string transactionId, string newStatus)
{
    var updateData = new Dictionary<string, object?>
    {
        ["status"] = newStatus,
        ["updated_at"] = DateTime.UtcNow
    };

    var jsonContent = JsonSerializer.Serialize(updateData);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

    // Supabase PATCH request (update specific record by ID)
    var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/transactions?id=eq.{transactionId}");
    request.Content = content;
    request.Headers.Add("Prefer", "return=representation");

    var response = await _httpClient.SendAsync(request);
    var json = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"[UpdateTransactionStatus] Status: {response.StatusCode}, Body: {json}");

    return response.IsSuccessStatusCode;
}
public async Task<List<Transaction>> GetPendingTransactions()
{
    var selectQuery = "*,UserDetails:users(name,surname,email)";
    var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?status=eq.Pending&select={selectQuery}");
    var json = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"[GetPendingTransactions] Status: {response.StatusCode}, Body: {json}");

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException($"Failed to fetch pending transactions. Status: {response.StatusCode}, Response: {json}");
    }

    var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
    return transactions ?? new List<Transaction>();
}




        // ------------------------------------------------------------------

        // TRANSACTION CRUD METHODS (Fixes applied)

        // ------------------------------------------------------------------



        // 🔑 FIX: Update to include user join for deserialization success

        public async Task<List<Transaction>> GetTransactionsByUser(string userId)

        {

            // Select all transaction fields AND join the user table for details

            // The join needs to be aliased to the property name in the Transaction model that holds the joined object.

            // If your Transaction model uses:

            // [JsonPropertyName("user_id")] public JoinedUser? UserDetails { get; set; }

            // Then the query is:

            var selectQuery = "*,UserDetails:users(name,surname,email)"; // Aliased to UserDetails



            // Filter by user_id

            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?user_id=eq.{userId}&select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();



            Console.WriteLine($"[GetTransactionsByUser] Status: {response.StatusCode}, Body: {json}"); // Log response



            if (!response.IsSuccessStatusCode)

            {

                // Throw an exception with details to help debug the 500 error

                throw new HttpRequestException(

                    $"Supabase query failed for user {userId}. Status: {response.StatusCode}. Response: {json}",

                    null,

                    response.StatusCode

                );

            }



            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);

            return transactions ?? new List<Transaction>();

        }



        public async Task<List<Transaction>> GetAllTransactionsWithUsers()

        {

            // Select all transaction fields AND join the user table for details

            // This is likely working because your front-end only uses this for Admin view.

            var selectQuery = "*,UserDetails:users(name,surname,email)";

            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();



            Console.WriteLine($"[GetAllTransactionsWithUsers] Status: {response.StatusCode}, Body: {json}");



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



        // ... (rest of the methods: UpdateTransaction, DeleteTransaction, AddTransaction, GetTransactions, GetUsers, SimpleLoginAsync, GetUserByEmail) ...



        public async Task<bool> UpdateTransaction(string id, TransactionUpdateRequest request)

        {

            var jsonContent = JsonSerializer.Serialize(request);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");



            var requestMessage = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/transactions?id=eq.{id}");

            requestMessage.Content = content;

            requestMessage.Headers.Add("Prefer", "return=representation");



            var response = await _httpClient.SendAsync(requestMessage);

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[UpdateTransaction] Status: {response.StatusCode}, Body: {json}");



            return response.IsSuccessStatusCode;

        }



        public async Task<bool> DeleteTransaction(string id)

        {

            var response = await _httpClient.DeleteAsync($"{_baseUrl}/transactions?id=eq.{id}");



            Console.WriteLine($"[DeleteTransaction] Status: {response.StatusCode}");



            return response.IsSuccessStatusCode;

        }



        public async Task<List<Transaction>> GetTransactions()

        {

            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions");

            var json = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();

        }



        public async Task<List<User>> GetUsers()

        {

            var selectQuery = "*, role";



            var response = await _httpClient.GetAsync($"{_baseUrl}/users?select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[GetUsers] Status: {response.StatusCode}, Body: {json}");



            if (!response.IsSuccessStatusCode)

            {

                throw new HttpRequestException(

                    $"Failed to fetch users. Status: {response.StatusCode}, Response: {json}"

                );

            }



            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();

        }
public async Task<List<User>> GetAllUsers()
{
    var selectQuery = "*, role";

    var response = await _httpClient.GetAsync($"{_baseUrl}/users?select={selectQuery}");
    var json = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"[GetAllUsers] Status: {response.StatusCode}, Body: {json}");

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Failed to fetch all users. Status: {response.StatusCode}, Response: {json}"
        );
    }

    var users = JsonSerializer.Deserialize<List<User>>(json);
    return users ?? new List<User>();
}
public async Task<User?> GetUserById(string userId)
{
    var selectQuery = "*, role";

    var response = await _httpClient.GetAsync($"{_baseUrl}/users?id=eq.{userId}&select={selectQuery}");
    var json = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"[GetUserById] Status: {response.StatusCode}, Body: {json}");

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Failed to fetch user by ID. Status: {response.StatusCode}, Response: {json}"
        );
    }

    var users = JsonSerializer.Deserialize<List<User>>(json);
    return users?.FirstOrDefault();
}



        public async Task<Transaction> AddTransaction(Transaction transaction)

        {

            var jsonContent = JsonSerializer.Serialize(transaction);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transactions");

            request.Content = content;

            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)

                throw new HttpRequestException($"Supabase Post failed: {response.StatusCode}. Response: {json}");

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);

            return transactions?[0] ?? transaction;

        }



        // --- AUTHENTICATION METHODS (UNCHANGED) ---



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



        public async Task<User?> GetUserByEmail(string email)

        {

            var selectQuery = "*, role";



            var response = await _httpClient.GetAsync($"{_baseUrl}/users?email=eq.{email}&select={selectQuery}");



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

        // FinSys/Services/SupabaseService.cs



public async Task<Transaction?> GetTransactionById(string id)

        {

            // The select query ensures you get the full transaction data along with joined user details

            var selectQuery = "*,UserDetails:users(name,surname,email)";



            // Fetch by ID, which is the primary key

            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?id=eq.{id}&select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();



            Console.WriteLine($"[GetTransactionById] Status: {response.StatusCode}, Body: {json}");



            if (!response.IsSuccessStatusCode)

            {

                // Log or handle the failed request

                return null;

            }



            // Supabase returns a list even for a single-item query

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);



            // Return the first, or null if the list is empty

            return transactions?.FirstOrDefault();

        }



        public async Task<User> AddUser(User user)

        {

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



            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[AddUser] Status: {response.StatusCode}, Body: {json}");



            if (!response.IsSuccessStatusCode)

            {

                throw new HttpRequestException(

                    $"Supabase User Add failed ({response.StatusCode}). " +

                    $"Check for UNIQUE constraint violations (e.g., duplicate email). Response: {json}",

                    null,

                    response.StatusCode

                );

            }



            var users = JsonSerializer.Deserialize<List<User>>(json, jsonOptions);

            return users?[0] ?? user;

        }

    }

}
