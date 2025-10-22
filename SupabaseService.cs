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



            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);

            _httpClient.DefaultRequestHeaders.Authorization =

                new AuthenticationHeaderValue("Bearer", _apiKey);

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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



        // transactionId is now string (e.g., "TR001")

        public async Task<bool> UpdateTransactionStatus(string transactionId, string newStatus)

        {

            var updateData = new Dictionary<string, object?>

            {

                ["status"] = newStatus,

                ["updated_at"] = DateTime.UtcNow

            };



            var jsonContent = JsonSerializer.Serialize(updateData);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");



            // Targeting the new string Primary Key 'id'

            var encodedTransactionId = Uri.EscapeDataString(transactionId);

            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/transactions?id=eq.{encodedTransactionId}");


            request.Content = content;

            request.Headers.Add("Prefer", "return=representation");



            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();



            Console.WriteLine($"[UpdateTransactionStatus] Status: {response.StatusCode}, Body: {json}");



            return response.IsSuccessStatusCode;

        }



        public async Task<List<Transaction>> GetPendingTransactions()

        {

            var selectQuery = "*,user:users(name,surname,email)";

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

        // TRANSACTION CRUD METHODS (Updated for string IDs)

        // ------------------------------------------------------------------



        // userId is now string

        public async Task<List<Transaction>> GetTransactionsByUser(string userId)

        {

            var selectQuery = "*,user:users(name,surname,email)";



            // user_id is now a string FK, no uuid cast needed

            var encodedUserId = Uri.EscapeDataString(userId);

            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?user_id=eq.{encodedUserId}&select={selectQuery}");

            var json = await response.Content.ReadAsStringAsync();



            Console.WriteLine($"[GetTransactionsByUser] Status: {response.StatusCode}, Body: {json}");



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



        public async Task<List<Transaction>> GetAllTransactionsWithUsers()

        {

            var selectQuery = "*,user:users(name,surname,email)";

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



        // id is now string

      // Inside FinSys/Services/SupabaseService.cs

// id is now string
public async Task<bool> UpdateTransaction(string id, TransactionUpdateRequest request)
{
    var jsonContent = JsonSerializer.Serialize(request);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

    // Targeting new string PK 'id'
    var encodedId = Uri.EscapeDataString(id);
    
    // Using 'eq.' first, since Delete works with 'eq.'
    var requestMessage = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/transactions?id=eq.{encodedId}");

    requestMessage.Content = content;
    
    // This header is crucial: it makes Supabase return the updated record(s) if successful.
    requestMessage.Headers.Add("Prefer", "return=representation"); 

    var response = await _httpClient.SendAsync(requestMessage);
    var json = await response.Content.ReadAsStringAsync();
    
    Console.WriteLine($"[UpdateTransaction] Status: {response.StatusCode}, Body: {json}");

    // --- 🚨 CRITICAL FIX ---
    if (response.IsSuccessStatusCode)
    {
        // If Supabase returns success (2xx) but the body is '[]', it means 0 rows were affected.
        // We check if the trimmed JSON is empty or specifically "[]"
        if (json.Trim() == "[]" || string.IsNullOrWhiteSpace(json.Trim('[', ']', ' ', '\n', '\r')))
        {
            // The record was not found or not updated.
            return false;
        }
        // If we get a success code AND a non-empty body (the updated object), it worked.
        return true;
    }
    
    // If we get a clear error status code (e.g., 4xx, 5xx)
    return false;
}



        // id is now string

        public async Task<bool> DeleteTransaction(string id)

        {

            // Targeting new string PK 'id'

            var encodedId = Uri.EscapeDataString(id);

            var response = await _httpClient.DeleteAsync($"{_baseUrl}/transactions?id=eq.{encodedId}");



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



        // userId is now string

        public async Task<User?> GetUserById(string userId)

        {

            var selectQuery = "*, role";



            // Targeting new string PK 'id'

            var encodedUserId = Uri.EscapeDataString(userId);

            var response = await _httpClient.GetAsync($"{_baseUrl}/users?id=eq.{encodedUserId}&select={selectQuery}");

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



        public async Task<User?> GetUserByEmail(string email)

        {

            var selectQuery = "*, role";



            // Targeting new string PK 'email'

            var encodedEmail = Uri.EscapeDataString(email);

            var response = await _httpClient.GetAsync($"{_baseUrl}/users?email=eq.{encodedEmail}&select={selectQuery}");

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



        public async Task<Transaction> AddTransaction(Transaction transaction)

        {

            // FIX: Set the primary key ID to null so that the database's 

            // sequential DEFAULT function ('TR' || lpad(nextval...)) is used,

            // avoiding the "duplicate key value" constraint violation.

            transaction.Id = null;



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



        public async Task<User> AddUser(User user)

        {

            // ID is automatically generated by the database

            var jsonContent = JsonSerializer.Serialize(user);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/users");

            request.Content = content;

            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)

                throw new HttpRequestException($"Supabase Post failed: {response.StatusCode}. Response: {json}");

            var users = JsonSerializer.Deserialize<List<User>>(json);

            return users?[0] ?? user;

        }



        // --- AUTHENTICATION METHODS (Updated for string IDs) ---



        public async Task<User?> SimpleLoginAsync(string email, string password)

        {

            var user = await GetUserByEmail(email);



            if (user == null || string.IsNullOrWhiteSpace(user.Password))

            {

                return null;

            }



            if (user.Password == password)

            {

                return user; // Returns User object with string ID

            }

            return null;

        }



 

        

        // id is now string
        public async Task<Transaction?> GetTransactionById(string id)
        {
            var selectQuery = "*,user:users(name,surname,email)";

            var encodedId = Uri.EscapeDataString(id);
            
            // ⭐ CRITICAL FIX: Changed 'id=eq.' to 'id=ilike.' for case-insensitive matching
            // This ensures "TR065" matches "tr065" in the database.
            var response = await _httpClient.GetAsync($"{_baseUrl}/transactions?id=ilike.{encodedId}&select={selectQuery}"); 
            
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to fetch transaction by ID. Status: {response.StatusCode}, Response: {json}"
                );
            }

            var transactions = JsonSerializer.Deserialize<List<Transaction>>(json);
            return transactions?.FirstOrDefault();
        }
    }
}
