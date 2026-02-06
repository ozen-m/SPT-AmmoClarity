using SPTarkov.Server.Core.Models.Common;

namespace AmmoClarity.Models;

public class AmmoData
{
    public List<Caliber> Calibers { get; internal set; } = [];
    public List<Ammo> AllAmmo { get; internal set; } = [];

    public List<Ammo> GetAllAmmo()
    {
        List<Ammo> allAmmo = [];

        foreach (Caliber caliber in Calibers)
        {
            allAmmo.AddRange(caliber.Ammos);
        }

        return allAmmo;
    }

    public Caliber GetOrCreateCaliber(string caliberFullName, string caliberShortName)
    {
        Caliber? caliber = Calibers.FirstOrDefault(i => i.FullName == caliberFullName);

        if (caliber == null)
        {
            caliber = new(caliberFullName, caliberShortName);
            Calibers.Add(caliber);
        }

        return caliber;
    }
}

public class Caliber(string fullName, string shortName)
{
    public string FullName { get; set; } = fullName;
    public string ShortName { get; set; } = shortName;
    public List<Ammo> Ammos { get; set; } = [];
}

public class Ammo(string originalShortName, string newShortName, string newLongName, MongoId mongoId)
{
    public string OriginalShortname { get; set; } = originalShortName;
    public string NewShortname { get; set; } = newShortName;
    public string NewLongName { get; set; } = newLongName;
    public MongoId Id { get; set; } = mongoId;
}
