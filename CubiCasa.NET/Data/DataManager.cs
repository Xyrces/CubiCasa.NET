using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace CubiCasa.Data
{
    public static class DataManager
    {
        private const string DatasetUrl = "https://zenodo.org/records/2613548/files/cubicasa5k.zip?download=1";
        private const string DefaultDataDirName = "CubiCasaData";
        private const string UserHomeDataDirName = ".cubicasa";

        public static async Task EnsureDataAsync(Action<string> logger = null)
        {
            var log = logger ?? Console.WriteLine;

            var targetDir = GetDataPath();
            if (IsValidDataDir(targetDir))
            {
                log($"Data found at {targetDir}");
                return;
            }

            // If we are here, targetDir is likely the default location but empty or missing.
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
            }
            catch (Exception ex)
            {
                log($"Error downloading or extracting dataset: {ex.Message}");
                // Attempt cleanup
                if (File.Exists(zipPath)) File.Delete(zipPath);
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

            // 1. Check ~/.cubicasa
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userHomeData = Path.Combine(userProfile, UserHomeDataDirName);
            if (IsValidDataDir(userHomeData)) return userHomeData;

            // 2. Check ./CubiCasaData
            var localData = Path.Combine(workingDir, DefaultDataDirName);
            if (IsValidDataDir(localData)) return localData;

            // 3. Check for ./CubiCasa5k (legacy/alternative)
            var legacyData = Path.Combine(workingDir, "CubiCasa5k");
            if (IsValidDataDir(legacyData)) return legacyData;

            // Default to ./CubiCasaData
            return localData;
        }

        private static bool IsValidDataDir(string path)
        {
            if (!Directory.Exists(path)) return false;
            // Check for at least one SVG to be sure it's the dataset
            return Directory.GetFiles(path, "model.svg", SearchOption.AllDirectories).Length > 0;
        }
    }
}
