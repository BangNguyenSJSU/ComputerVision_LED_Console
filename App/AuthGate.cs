using System;
using System.Security.Cryptography;
using ComputerVision_LED_Console.Config;

namespace ComputerVision_LED_Console.App
{
    public class AuthGate
    {
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        private readonly SecurityConfig _cfg;
        private readonly AppState _state;

        public AuthGate(SecurityConfig cfg, AppState state)
        {
            _cfg = cfg;
            _state = state;

            // Lock is bypassed when disabled by config OR when no password has been configured yet.
            // Both states leave the user free to edit; first-run flow in AppController is responsible
            // for turning the empty state into a configured one.
            _state.Unlocked = !_cfg.LockEnabled || !IsConfigured;
        }

        public bool IsUnlocked => _state.Unlocked;

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_cfg.PasswordHash) && !string.IsNullOrEmpty(_cfg.PasswordSalt);

        public bool VerifyPassword(string password)
        {
            if (!IsConfigured) return true;

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(_cfg.PasswordSalt);
                expected = Convert.FromBase64String(_cfg.PasswordHash);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] derived = Pbkdf2(password, salt, _cfg.PasswordIterations, expected.Length);
            return CryptographicOperations.FixedTimeEquals(derived, expected);
        }

        public bool TryUnlock(string password)
        {
            if (!VerifyPassword(password)) return false;
            _state.Unlocked = true;
            return true;
        }

        public void Lock() => _state.Unlocked = false;

        public (string hash, string salt) HashNewPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
            byte[] hash = Pbkdf2(password, salt, _cfg.PasswordIterations, HashBytes);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int outputBytes)
        {
            using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return derive.GetBytes(outputBytes);
        }
    }
}
