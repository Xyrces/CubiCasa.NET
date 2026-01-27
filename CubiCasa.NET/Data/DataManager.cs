using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CubiCasa.Data
{
    public static class DataManager
    {
        private const string DatasetUrl = "https://zenodo.org/records/2613548/files/cubicasa5k.zip?download=1";
        private const string DefaultDataDirName = "CubiCasaData";
        private const string UserHomeDataDirName = ".cubicasa";
        private const string CompletionMarkerFile = ".dataset_ready";

        public static async Task EnsureDataAsync(Action<string> logger = null)
        {
            var log = logger ?? Console.WriteLine;

            var targetDir = GetDataPath();
            if (IsValidDataDir(targetDir))
            {
                log($"Data found at {targetDir}");
                return;
            }

            // targetDir is the authoritative path from GetDataPath(). If valid data is missing, we download there.
            // Ensure the directory exists.
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var zipPath = Path.Combine(targetDir, "cubicasa5k.zip");

            log($"Dataset not found. Downloading to {zipPath}...");

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60); // Large file
                    using (var response = await client.GetAsync(DatasetUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            await contentStream.CopyToAsync(fileStream);
                        }
                    }
                }

                log("Download complete. Extracting...");

                // Extract
                ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);

                log($"Extraction complete to {targetDir}");

                // Validate extraction
                if (!Directory.EnumerateFiles(targetDir, "model.svg", SearchOption.AllDirectories).Any())
                {
                     throw new InvalidDataException("Extraction completed but no 'model.svg' files were found.");
                }

                // Mark as ready
                File.WriteAllText(Path.Combine(targetDir, CompletionMarkerFile), DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                log($"Error downloading or extracting dataset: {ex.Message}");
                // Attempt cleanup
                if (File.Exists(zipPath)) File.Delete(zipPath);

                // Remove partial data if we can identify it, or at least ensure no marker exists.
                var markerPath = Path.Combine(targetDir, CompletionMarkerFile);
                if (File.Exists(markerPath)) File.Delete(markerPath);

                throw;
            }
            finally
            {
                 // Cleanup zip if it exists (successful extraction)
                 if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }

        public static string GetDataPath()
        {
            var workingDir = Directory.GetCurrentDirectory();

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userHomeData = Path.Combine(userProfile, UserHomeDataDirName);
            var localData = Path.Combine(workingDir, DefaultDataDirName);
            var legacyData = Path.Combine(workingDir, "CubiCasa5k");

            // Priority 1: Valid Data (Where data definitely exists)
            if (IsValidDataDir(userHomeData)) return userHomeData;
            if (IsValidDataDir(localData)) return localData;
            if (IsValidDataDir(legacyData)) return legacyData;

            // Priority 2: Existing Directory (Candidate for repair/download)
            // If the user created ~/.cubicasa, they probably want the data there.
            if (Directory.Exists(userHomeData)) return userHomeData;

            // Priority 3: Default
            return localData;
        }

        private static bool IsValidDataDir(string path)
        {
            if (!Directory.Exists(path)) return false;
            // Check for completion marker and at least one SVG
            return File.Exists(Path.Combine(path, CompletionMarkerFile)) &&
                   Directory.EnumerateFiles(path, "model.svg", SearchOption.AllDirectories).Any();
        }
    }
}
