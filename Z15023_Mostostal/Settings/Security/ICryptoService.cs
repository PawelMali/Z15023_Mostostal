using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Z25023_Mostostal.Settings.Security;

// 1. Definiujemy interfejs (Dobre praktyki DI i testowania)
public interface ICryptoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

// 2. Implementacja AES-256
public class AesCryptoService : ICryptoService
{
    // Pola przechowujące klucz (32 bajty = 256 bitów) oraz wektor inicjujący IV (16 bajtów)
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesCryptoService()
    {
        // "Twarde" dane aplikacji. Dzięki nim szyfrowanie jest w 100% przenośne.
        // Zmień te ciągi znaków przed wdrożeniem na maszynę na własne, długie i losowe.
        string appSecretPassword = "Mostostal_Line_2026_!SecureAppKey#99";
        byte[] salt = Encoding.UTF8.GetBytes("Ostrow_Wlkp_Salt_2026_System_Z25023");

        // Używamy standardu PBKDF2 do wygenerowania bezpiecznego, kryptograficznego klucza.
        // 100 000 iteracji z użyciem SHA256 to obecny standard dla systemów Enterprise.
        // Generujemy 48 bajtów (32 dla klucza + 16 dla IV)
        byte[] keyAndIv = Rfc2898DeriveBytes.Pbkdf2(
            appSecretPassword,
            salt,
            100000,
            HashAlgorithmName.SHA256,
            48);

        _key = new byte[32];
        _iv = new byte[16];
        
        Array.Copy(keyAndIv, 0, _key, 0, 32);  // Pierwsze 32 bajty to klucz
        Array.Copy(keyAndIv, 32, _iv, 0, 16);  // Następne 16 bajtów to IV
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        // Tworzymy strumienie szyfrujące w pamięci
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream();
        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        using (var streamWriter = new StreamWriter(cryptoStream))
        {
            // Zapisujemy tekst jawny, który "w locie" jest zamieniany na bajty AES
            streamWriter.Write(plainText);
        }

        // Zwracamy wynik jako bezpieczny dla plików JSON ciąg Base64 (np. "AQwertyUi...==")
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream(cipherBytes);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);

            return streamReader.ReadToEnd();
        }
        catch (Exception)
        {
            // Obsługa błędu (np. plik JSON został zepsuty ręcznie lub użyto złego klucza).
            // Zwracamy puste, aby zapobiec wywaleniu całej aplikacji podczas startu.
            return string.Empty;
        }
    }
}
