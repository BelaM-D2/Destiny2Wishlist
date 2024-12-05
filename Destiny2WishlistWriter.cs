using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        string inputFile = "WishlistBlueprint.txt";
        string outputFile = "Wishlist.txt";

        Dictionary<string, string> weaponIds = new Dictionary<string, string> {
            {"Edge of Action", "2535142413"}
        };

        Dictionary<string, string> perkIds = new Dictionary<string, string> {
            {"Perk1", "839105230"}
        };

        // Read lines from the input file
        List<string> lines = File.ReadAllText(inputFile).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList<string>();
        List<string> wishlistLines = new List<string>();

        string weaponName = "";
        bool isEnhanced = false;
        string usage = "";
        string[] masterworks = new string[0];
        string[] barrels = new string[0];
        string[] mags = new string[0];
        string perk1 = "";
        string perk2 = "";
        List<string[]> overallRolls = new List<string[]>();
        List<string[]> magRolls = new List<string[]>();
        int overallCounter = 0;

        foreach(string line in lines) {
            if(line.StartsWith("// ")) { // Is not a comment or anything else
                string[] splitLine = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                switch(splitLine[1]) {
                    case "Name":
                    // Reset all variables to default values
                    weaponName = "";
                    isEnhanced = false;
                    usage = "";
                    masterworks = new string[0];
                    barrels = new string[0];
                    mags = new string[0];
                    perk1 = "";
                    perk2 = "";
                    overallRolls = new List<string[]>();
                    magRolls = new List<string[]>();
                    overallCounter = 0;

                    // Save name, if the weapon has enhanced perks, and masterworks
                    weaponName = splitLine[2];
                    isEnhanced = splitLine[3] == "E";
                    masterworks = splitLine[5..];
                    break;

                    case "Usage":
                    // Save usage and barrels
                    usage = splitLine[2];
                    barrels = splitLine[4..];
                    break;

                    case "Perks":
                    // Save perks and mags
                    perk1 = splitLine[2];
                    perk2 = splitLine[3];
                    mags = splitLine[5..];

                    overallRolls.Add(new string[] {perk1, perk2});
                    magRolls.Add(mags);

                    // Write god rolls
                    string masterworksString = "";
                    for(int i = 0; i < masterworks.Length; i++) {
                        if(i < masterworks.Length - 2) {
                            masterworksString += masterworks[i] + ", ";
                        } else {
                            masterworksString += masterworks[i];
                        }
                    }
                    wishlistLines.Add("//notes:" + usage + " God Roll; Preferred Masterworks: " + masterworksString);
                    for(int i = 0; i < barrels.Length; i++) {
                        for(int j = 0; j < mags.Length; i++) {
                            wishlistLines.Add("dimwishlist:item=" + (usage == "PvP" ? "-" : "") + weaponIds[weaponName] + "&perks=" + perkIds[barrels[i]] + "," + perkIds[mags[j]] + "," + perkIds[perk1] + "," + perkIds[perk2]);
                        }
                    }

                    // Cleanup
                    perk1 = "";
                    perk2 = "";
                    mags = new string[0];
                    break;

                    case "Mags":
                    // Write mag rolls
                    string barrelsString = "";
                    for(int i = 0; i < barrels.Length; i++) {
                        if(i < barrels.Length - 2) {
                            barrelsString += barrels[i] + ", ";
                        } else {
                            barrelsString += barrels[i];
                        }
                    }
                    wishlistLines.Add("//notes:" + usage + " Good Roll; Preferred Barrels: " + barrelsString);
                    for(int i = 0; i < magRolls.Count; i++) {
                        for(int j = 0; j < magRolls[i].Length; j++) {
                            wishlistLines.Add("dimwishlist:item=" + (usage == "PvP" ? "-" : "") + weaponIds[weaponName] + "&perks=" + perkIds[magRolls[i][j]] + "," + perkIds[overallRolls[overallCounter + i][0]] + "," + perkIds[overallRolls[overallCounter + i][1]]);
                        }
                    }

                    // Cleanup
                    magRolls = new List<string[]>();
                    overallCounter = overallRolls.Count();
                    break;

                    case "Overall":
                    // Write overall rolls
                    wishlistLines.Add("//notes: Okay Roll");
                    for(int i = 0; i < overallRolls.Count; i++) {
                        wishlistLines.Add("dimwishlist:item=" + weaponIds[weaponName] + "&perks=" + perkIds[overallRolls[i][0]] + "," + perkIds[overallRolls[i][1]]);
                    }

                    // Cleanup
                    overallRolls = new List<string[]>();
                    break;
                }
            } else {
                wishlistLines.Add(line);
            }
        }

        // Write the wishlist to the output file
        File.WriteAllLines(outputFile, wishlistLines);
    }
}