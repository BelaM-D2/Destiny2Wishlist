using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

class BungieWeaponPerksFetcher
{
    private const string ApiKey = "YOUR-API-KEY-HERE";
    private const string BaseUrl = "https://www.bungie.net";
    private const string PerkIconDirectory = "PerkIcons";

    public static async Task<List<Perk>> FetchWeaponPerks(string weaponId)
    {
        var fetcher = new BungieWeaponPerksFetcher();
        var perks = await fetcher.GetWeaponPerksAsync(weaponId);

        // Save perk icons locally
        await fetcher.SavePerkIcons(perks);

        return perks;
    }

    public async Task<List<Perk>> GetWeaponPerksAsync(string weaponId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        // Fetch weapon definition
        var response = await client.GetAsync($"{BaseUrl}/Platform/Destiny2/Manifest/DestinyInventoryItemDefinition/{weaponId}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch weapon definition: {response.StatusCode}");
            return new List<Perk>();
        }

        var json = await response.Content.ReadAsStringAsync();
        var weaponDefinition = JObject.Parse(json);

        // Extract sockets
        var sockets = weaponDefinition["Response"]?["sockets"]?["socketEntries"];
        if (sockets == null)
        {
            Console.WriteLine("No sockets found for this weapon.");
            return new List<Perk>();
        }

        var perks = new List<Perk>();

        // Extract perk details from sockets
        for (int socketIndex = 0; socketIndex < sockets.Count(); socketIndex++)
        {
            var socket = sockets[socketIndex];
            var reusablePlugItems = socket["reusablePlugItems"];
            if (reusablePlugItems == null) continue;

            foreach (var plug in reusablePlugItems)
            {
                var perkHash = plug["plugItemHash"]?.ToString();
                if (!string.IsNullOrEmpty(perkHash))
                {
                    // Only fetch if this hash corresponds to a valid perk
                    if (await IsValidPerk(perkHash))
                    {
                        var perk = await GetPerkDetailsAsync(perkHash, socketIndex);
                        if (perk != null) perks.Add(perk);
                    }
                }
            }
        }

        return perks;
    }

    private async Task<bool> IsValidPerk(string perkHash)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        var response = await client.GetAsync($"{BaseUrl}/Platform/Destiny2/Manifest/DestinyInventoryItemDefinition/{perkHash}");
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        var perkDefinition = JObject.Parse(json);

        // Check if the item has display properties (e.g., a name and description)
        var hasDisplayProperties = perkDefinition["Response"]?["displayProperties"] != null;

        // Optionally: Add additional filtering logic based on expected fields
        return hasDisplayProperties;
    }

    private async Task<Perk?> GetPerkDetailsAsync(string perkHash, int socketIndex)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        var response = await client.GetAsync($"{BaseUrl}/Platform/Destiny2/Manifest/DestinyInventoryItemDefinition/{perkHash}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch perk details: {response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var perkDefinition = JObject.Parse(json);

        var name = perkDefinition["Response"]?["displayProperties"]?["name"]?.ToString();
        var description = perkDefinition["Response"]?["displayProperties"]?["description"]?.ToString();
        var iconPath = perkDefinition["Response"]?["displayProperties"]?["icon"]?.ToString();

        // Only return if the perk has valid display properties
        if (string.IsNullOrEmpty(name)) return null;

        return new Perk
        {
            Name = name ?? "Unknown Perk",
            Description = description ?? "No description available.",
            IconPath = iconPath,
            SocketIndex = socketIndex
        };
    }

    private async Task SavePerkIcons(List<Perk> perks)
    {
        using var client = new HttpClient();
        var iconDirectory = Path.Combine(Directory.GetCurrentDirectory(), PerkIconDirectory);

        if (!Directory.Exists(iconDirectory))
        {
            Directory.CreateDirectory(iconDirectory);
        }

        foreach (var perk in perks.Where(p => !string.IsNullOrEmpty(p.IconPath)))
        {
            try
            {
                var iconFilePath = Path.Combine(iconDirectory, $"{perk.Name.Replace(" ", "_")}.png");

                // Skip downloading if the file already exists
                if (File.Exists(iconFilePath))
                {
                    Console.WriteLine($"Icon for {perk.Name} already exists at {iconFilePath}. Skipping download.");
                    continue;
                }

                // Download the icon
                var iconUrl = $"{BaseUrl}{perk.IconPath}";
                var iconResponse = await client.GetAsync(iconUrl);

                if (iconResponse.IsSuccessStatusCode)
                {
                    await using var fs = new FileStream(iconFilePath, FileMode.Create, FileAccess.Write);
                    await iconResponse.Content.CopyToAsync(fs);
                    Console.WriteLine($"Saved icon for {perk.Name} to {iconFilePath}");
                }
                else
                {
                    Console.WriteLine($"Failed to download icon for {perk.Name}: {iconResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving icon for {perk.Name}: {ex.Message}");
            }
        }
    }

    public class Perk
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        public int SocketIndex { get; set; } // New property for socket information
    }
}
