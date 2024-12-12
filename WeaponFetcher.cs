using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

class WeaponFetcher
{
    private const string ApiKey = "YOUR-API-KEY-HERE";
    private const string BaseUrl = "https://www.bungie.net";

    public async Task<List<Weapon>> FetchWeapons()
    {
        return await GetWeaponDataAsync();
    }

    private async Task<List<Weapon>> GetWeaponDataAsync()
    {
        // Step 1: Fetch the manifest
        var manifestUrl = await GetManifestUrlAsync();
        if (string.IsNullOrEmpty(manifestUrl))
        {
            Console.WriteLine("Failed to fetch the manifest URL.");
            return new List<Weapon>();
        }

        // Step 2: Download and parse the manifest file dynamically
        var weaponData = await ParseWeaponDataAsync(manifestUrl);
        if (weaponData != null)
        {
            await SaveWeaponIcons(weaponData); // Save icons locally
        }
        return weaponData ?? new List<Weapon>();
    }

    private async Task<string?> GetManifestUrlAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        var response = await client.GetAsync($"{BaseUrl}/Platform/Destiny2/Manifest/");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed to fetch manifest: " + response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine("Manifest Response: " + json);

        var manifest = JObject.Parse(json);

        var path = manifest["Response"]?["jsonWorldComponentContentPaths"]?["en"]?["DestinyInventoryItemDefinition"]?.ToString();
        if (path == null)
        {
            Console.WriteLine("Failed to parse manifest JSON.");
            return null;
        }

        return $"{BaseUrl}{path}";
    }

    private async Task<List<Weapon>> ParseWeaponDataAsync(string manifestUrl)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(manifestUrl);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed to fetch manifest file: " + response.StatusCode);
            return new List<Weapon>();
        }

        using var stream = await response.Content.ReadAsStreamAsync();

        // Detect file type dynamically
        string fileType = await DetectFileType(stream);
        Console.WriteLine("Detected file type: " + fileType);

        switch (fileType)
        {
            case "json":
                return await ParseFromJson(stream); // Handle plain JSON
            default:
                Console.WriteLine("Unsupported file type.");
                return new List<Weapon>();
        }
    }

    private async Task<List<Weapon>> ParseFromJson(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var items = JObject.Parse(json);

        return items
            .Properties()
            .Select(prop => new Weapon
            {
                Id = prop.Name,
                Name = prop.Value["displayProperties"]?["name"]?.ToString() ?? "Unknown",
                IconPath = prop.Value["displayProperties"]?["icon"]?.ToString(),
                ItemType = prop.Value["itemType"]?.ToString()
            })
            .Where(w => w.ItemType == "3") // Filter weapons
            .ToList();
    }

    private async Task<string> DetectFileType(Stream stream)
    {
        byte[] buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, buffer.Length);

        // Reset stream position for further processing
        stream.Position = 0;

        string signature = BitConverter.ToString(buffer);
        Console.WriteLine("File signature: " + signature);

        if (buffer[0] == 0x7B) // 0x7B is '{', indicating a JSON file
        {
            return "json";
        }

        return "unknown";
    }

    private async Task SaveWeaponIcons(List<Weapon> weapons)
    {
        using var client = new HttpClient();
        var iconDirectory = Path.Combine(Directory.GetCurrentDirectory(), "WeaponIcons");

        if (!Directory.Exists(iconDirectory))
        {
            Directory.CreateDirectory(iconDirectory);
        }

        foreach (var weapon in weapons.Where(w => !string.IsNullOrEmpty(w.IconPath)))
        {
            try
            {
                // Define the local icon file path
                var iconFilePath = Path.Combine(iconDirectory, $"{weapon.Id}.png");

                // Skip downloading if the file already exists
                if (File.Exists(iconFilePath))
                {
                    Console.WriteLine($"Icon for {weapon.Name} already exists at {iconFilePath}. Skipping download.");
                    continue;
                }

                // Download the icon
                var iconUrl = $"{BaseUrl}{weapon.IconPath}";
                var iconResponse = await client.GetAsync(iconUrl);

                if (iconResponse.IsSuccessStatusCode)
                {
                    await using var fs = new FileStream(iconFilePath, FileMode.Create, FileAccess.Write);
                    await iconResponse.Content.CopyToAsync(fs);
                    Console.WriteLine($"Saved icon for {weapon.Name} to {iconFilePath}");
                }
                else
                {
                    Console.WriteLine($"Failed to download icon for {weapon.Name}: {iconResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving icon for {weapon.Name}: {ex.Message}");
            }
        }
    }

    public class Weapon
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        public string? ItemType { get; set; }
    }
}