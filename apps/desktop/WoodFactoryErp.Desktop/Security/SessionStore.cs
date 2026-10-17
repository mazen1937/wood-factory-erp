using System.Security.Cryptography;
using System.Text;

namespace WoodFactoryErp.Desktop.Security;

internal static class SessionStore
{
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoodFactoryErp", "session.dat");
    public static void Save(string token) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!); File.WriteAllBytes(Path, ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser)); }
    public static string? Read() { if (!File.Exists(Path)) return null; try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path), null, DataProtectionScope.CurrentUser)); } catch { Delete(); return null; } }
    public static void Delete() { if (File.Exists(Path)) File.Delete(Path); }
}
