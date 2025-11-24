using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

    /// <summary>
    /// Provides secure storage and retrieval of sensitive credentials using encryption
    /// This makes it significantly harder (though not impossible) to extract credentials from the APK
    /// </summary>
    public static class SecureCredentials
    {
        // Obfuscated encryption keys - split and scattered to make static analysis harder
        private static readonly byte[] Part1 = { 0x4B, 0x65, 0x79, 0x50, 0x61, 0x72, 0x74 };
        private static readonly byte[] Part2 = { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37 };
        private static readonly byte[] Part3 = { 0x38, 0x39, 0x30, 0x41, 0x42, 0x43, 0x44 };

        // XOR keys for additional obfuscation
        private static readonly int[] XorPattern = { 0x5A, 0x3C, 0x6B, 0x2F, 0x4D };

        /// <summary>
        /// Gets the decrypted Google Client ID
        /// </summary>
        public static string GetClientId()
        {
#if UNITY_EDITOR
            // In editor, use environment variable or return empty for manual config
            return "175758872112-vqhmq6rvt3hg10hpn29estrd8mqlbcd5.apps.googleusercontent.com";
#else
            // Encrypted client ID - replace with your encrypted value
            byte[] encryptedId = {
                0x20, 0x84, 0x7F, 0xCF, 0xF3, 0x38, 0x2C, 0x1D, 0x27, 0xC4, 0x82, 0x71, 0xF4, 0x78, 0x22, 0xD6, 0x71, 0x34, 0x91, 0x11, 0x83, 0x43, 0x73, 0x76, 0x00, 0x70, 0x8A, 0x31, 0xC9, 0xD0, 0xB3, 0x11, 
  0x4C, 0xE8, 0x36, 0xFA, 0x76, 0x9C, 0xC1, 0xE0, 0x4A, 0x77, 0x0D, 0x85, 0x38, 0xEC, 0x44, 0x4B, 
  0x08, 0xF6, 0x68, 0xA7, 0xBD, 0x7B, 0x06, 0x57, 0x28, 0x8A, 0x79, 0x0F, 0x98, 0x9F, 0xD6, 0xD9, 
  0x6D, 0x00, 0xD1, 0x56, 0xE8, 0x2F, 0xBF, 0xA4 
            };
            
            if (encryptedId.Length == 0)
            {
                Debug.LogError("Client ID not configured!");
                return "";
            }
            
            return DecryptString(encryptedId);
#endif
        }

        /// <summary>
        /// Gets the decrypted Google Client Secret
        /// </summary>
        public static string GetClientSecret()
        {
#if UNITY_EDITOR
            // In editor, use environment variable or return empty for manual config
            return "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";
#else
            // Encrypted client secret - replace with your encrypted value
            byte[] encryptedSecret = {
               0x56, 0xFC, 0x09, 0xAB, 0x96, 0x58, 0x39, 0x13, 0x2D, 0xC1, 0x81, 0x30, 0x86, 0x47, 0x66, 0xD8, 
  0x51, 0x3D, 0xC3, 0x14, 0xA2, 0x55, 0x3A, 0x64, 0x1E, 0x37, 0xDD, 0x1D, 0xD7, 0xF3, 0xF0, 0x4D, 
  0x5D, 0xCA, 0x04
            };
            
            if (encryptedSecret.Length == 0)
            {
                Debug.LogError("Client Secret not configured!");
                return "";
            }
            
            return DecryptString(encryptedSecret);
#endif
        }

        /// <summary>
        /// Reconstructs the encryption key from scattered parts
        /// </summary>
        private static byte[] GetKey()
        {
            byte[] key = new byte[Part1.Length + Part2.Length + Part3.Length];
            Array.Copy(Part1, 0, key, 0, Part1.Length);
            Array.Copy(Part2, 0, key, Part1.Length, Part2.Length);
            Array.Copy(Part3, 0, key, Part1.Length + Part2.Length, Part3.Length);

            // Apply XOR pattern for additional obfuscation
            for (int i = 0; i < key.Length; i++)
            {
                key[i] ^= (byte)(XorPattern[i % XorPattern.Length]);
            }

            return key;
        }

        /// <summary>
        /// Decrypts an encrypted byte array to a string
        /// </summary>
        private static string DecryptString(byte[] encryptedData)
        {
            try
            {
                byte[] key = GetKey();
                byte[] decrypted = new byte[encryptedData.Length];

                // Multi-layer XOR decryption with key rotation
                for (int i = 0; i < encryptedData.Length; i++)
                {
                    byte keyByte = key[i % key.Length];
                    byte rotatedKey = (byte)((keyByte << (i % 8)) | (keyByte >> (8 - (i % 8))));
                    decrypted[i] = (byte)(encryptedData[i] ^ rotatedKey ^ (byte)(i % 256));
                }

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"Decryption failed: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// Encrypts a string to a byte array (Editor only - for generating encrypted values)
        /// </summary>
        public static byte[] EncryptString(string plainText)
        {
#if UNITY_EDITOR
            try
            {
                byte[] key = GetKey();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = new byte[plainBytes.Length];

                // Multi-layer XOR encryption with key rotation
                for (int i = 0; i < plainBytes.Length; i++)
                {
                    byte keyByte = key[i % key.Length];
                    byte rotatedKey = (byte)((keyByte << (i % 8)) | (keyByte >> (8 - (i % 8))));
                    encrypted[i] = (byte)(plainBytes[i] ^ rotatedKey ^ (byte)(i % 256));
                }

                return encrypted;
            }
            catch (Exception e)
            {
                Debug.LogError($"Encryption failed: {e.Message}");
                return new byte[0];
            }
#else
            Debug.LogError("Encryption is only available in the editor!");
            return new byte[0];
#endif
        }

        /// <summary>
        /// Helper method to generate encrypted byte arrays for your credentials (Editor only)
        /// Call this from the Unity Editor to get the encrypted values to paste into the code
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void GenerateEncryptedValues(string clientId, string clientSecret)
        {
#if UNITY_EDITOR
            byte[] encryptedId = EncryptString(clientId);
            byte[] encryptedSecret = EncryptString(clientSecret);

            Debug.Log("=== ENCRYPTED CLIENT ID ===");
            Debug.Log(ByteArrayToString(encryptedId));
            Debug.Log("\n=== ENCRYPTED CLIENT SECRET ===");
            Debug.Log(ByteArrayToString(encryptedSecret));
            Debug.Log("\nCopy these byte arrays into SecureCredentials.cs");
#endif
        }

        private static string ByteArrayToString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder("{ ");
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append($"0x{bytes[i]:X2}");
                if (i < bytes.Length - 1)
                    sb.Append(", ");
                if ((i + 1) % 16 == 0 && i < bytes.Length - 1)
                    sb.Append("\n  ");
            }
            sb.Append(" }");
            return sb.ToString();
        }
    }