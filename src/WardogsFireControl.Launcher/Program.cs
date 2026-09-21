using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            using var payload = OpenPayload();
            var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()[..16];
            payload.Position = 0;

            var installDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WardogsFireControl",
                "App",
                hash);
            var marker = Path.Combine(installDirectory, ".ready");
            var application = Path.Combine(installDirectory, "WardogsFireControl.exe");

            if (!File.Exists(marker) || !File.Exists(application))
            {
                Directory.CreateDirectory(installDirectory);
                ExtractSafely(payload, installDirectory);
                File.WriteAllText(marker, hash);
            }

            Process.Start(new ProcessStartInfo(application)
            {
                WorkingDirectory = installDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBoxW(
                IntPtr.Zero,
                "WARDOGS Fire Control could not start.\n\n" + exception.GetBaseException().Message,
                "WARDOGS Fire Control",
                0x10);
        }
    }

    private static Stream OpenPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("WardogsFireControl-win-x64.zip", StringComparison.Ordinal));
        return resource is null
            ? throw new InvalidOperationException("The embedded application payload is missing.")
            : assembly.GetManifestResourceStream(resource)
              ?? throw new InvalidOperationException("The embedded application payload could not be opened.");
    }

    private static void ExtractSafely(Stream payload, string destination)
    {
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var outputPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!outputPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The application package contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            entry.ExtractToFile(outputPath, overwrite: true);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
