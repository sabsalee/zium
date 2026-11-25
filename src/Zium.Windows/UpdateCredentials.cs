using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Zium.Windows;

internal static class ApplicationData
{
    internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zium");
}

internal static class UpdateCredentials
{
    private static string FilePath => Path.Combine(ApplicationData.Folder, "update-credential.bin");

    internal static string? Read(string path)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    internal static string? Read() => Read(FilePath);

    internal static void Write(string path, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        var bytes = Encoding.UTF8.GetBytes(token);
        try
        {
            File.WriteAllBytes(temp, ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    internal static void Write(string token) => Write(FilePath, token);

    internal static void Remove() => File.Delete(FilePath);
}
