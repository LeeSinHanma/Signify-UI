using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SignifyUI.Models;

namespace SignifyUI.Services
{
    /// <summary>
    /// Simple file-based authentication service for user registration and login.
    /// Stores users in a JSON file with hashed passwords.
    /// </summary>
    public static class AuthService
    {
        private static readonly string DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Signify"
        );

        private static readonly string UsersFile = Path.Combine(DataFolder, "users.json");

        private static readonly string LearnProgressRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SignifyUI",
            "learn_progress");

        /// <summary>Canonical username for the signed-in user, or null when logged out.</summary>
        public static string? CurrentUsername { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(CurrentUsername);

        /// <summary>Fired when the user logs in or out (including <see cref="Logout"/>).</summary>
        public static event EventHandler? SessionChanged;

        static AuthService()
        {
            // Ensure data folder exists
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }
        }

        /// <summary>
        /// JSON path for the current user's Learn-mode letter progress, or null when not signed in.
        /// </summary>
        public static string? GetLearnProgressFilePath()
        {
            if (string.IsNullOrWhiteSpace(CurrentUsername))
            {
                return null;
            }

            string safe = SanitizeFileNameSegment(CurrentUsername);
            if (string.IsNullOrEmpty(safe))
            {
                return null;
            }

            return Path.Combine(LearnProgressRoot, $"{safe}.json");
        }

        private static string SanitizeFileNameSegment(string name)
        {
            var chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string s = new string(chars);
            return s.Length > 80 ? s[..80] : s;
        }

        public static void Logout()
        {
            if (!IsLoggedIn)
            {
                return;
            }

            CurrentUsername = null;
            SessionChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void SetLoggedInUser(string username)
        {
            CurrentUsername = username;
            SessionChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Register a new user with username and password.
        /// Returns success message or error message.
        /// </summary>
        public static (bool success, string message) RegisterUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username cannot be empty.");

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password cannot be empty.");

            if (username.Length < 3)
                return (false, "Username must be at least 3 characters.");

            if (password.Length < 6)
                return (false, "Password must be at least 6 characters.");

            var users = LoadUsers();

            // Check if user already exists
            if (users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return (false, "Username already exists.");

            // Hash the password and add user
            string passwordHash = HashPassword(password);
            var newUser = new User(username, passwordHash);
            users.Add(newUser);

            SaveUsers(users);
            return (true, $"Account created successfully! You can now log in.");
        }

        /// <summary>
        /// Authenticate a user with username and password.
        /// Returns success status and message.
        /// </summary>
        public static (bool success, string message) LoginUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Username and password required.");

            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return (false, "Username or password incorrect.");

            if (!VerifyPassword(password, user.PasswordHash))
                return (false, "Username or password incorrect.");

            SetLoggedInUser(user.Username);
            return (true, $"Welcome back, {user.Username}!");
        }

        /// <summary>
        /// Hash a password using SHA256.
        /// </summary>
        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var salt = Encoding.UTF8.GetBytes("Signify_Salt_2024");
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var combined = new byte[salt.Length + passwordBytes.Length];
                Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                Buffer.BlockCopy(passwordBytes, 0, combined, salt.Length, passwordBytes.Length);

                var hash = sha256.ComputeHash(combined);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Verify a password against its hash.
        /// </summary>
        private static bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput.Equals(hash);
        }

        /// <summary>
        /// Load all users from the JSON file.
        /// </summary>
        private static List<User> LoadUsers()
        {
            if (!File.Exists(UsersFile))
                return new List<User>();

            try
            {
                var json = File.ReadAllText(UsersFile);
                var users = JsonSerializer.Deserialize<List<User>>(json);
                return users ?? new List<User>();
            }
            catch
            {
                return new List<User>();
            }
        }

        /// <summary>
        /// Save all users to the JSON file.
        /// </summary>
        private static void SaveUsers(List<User> users)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(users, options);
                File.WriteAllText(UsersFile, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving user data: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
