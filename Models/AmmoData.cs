using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AmmoClarity.Models;

public class AmmoData
{
    public List<Caliber> Calibers { get; internal set; } = [];
    public List<Ammo> AllAmmo { get; internal set; } = [];
    public List<Ammo> AllMagazines { get; internal set; } = [];

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
            caliber = new Caliber(caliberFullName, caliberShortName);
            Calibers.Add(caliber);
        }

        return caliber;
    }

    private readonly List<string> _calibersScratch = [];

    public string GetFirstCaliberShortName(TemplateItem magazineTemplate)
    {
        var ammoIds = magazineTemplate.Properties?.Cartridges?.FirstOrDefault()?.Properties?.Filters?.FirstOrDefault()?.Filter;
        if (ammoIds is null) return string.Empty;

        foreach (var caliber in Calibers)
        {
            var caliberAmmoIds = caliber.Ammos.Select(a => a.Id);
            if (ammoIds.Overlaps(caliberAmmoIds))
            {
                return caliber.ShortName;
            }
        }

        return string.Empty;
    }

    public string GetCalibersShortName(TemplateItem magazineTemplate)
    {
        _calibersScratch.Clear();

        var ammoIds = magazineTemplate.Properties?.Cartridges?.FirstOrDefault()?.Properties?.Filters?.FirstOrDefault()?.Filter;
        if (ammoIds is null) return string.Empty;

        foreach (var caliber in Calibers)
        {
            var caliberAmmoIds = caliber.Ammos.Select(a => a.Id);
            if (ammoIds.Overlaps(caliberAmmoIds))
            {
                if (_calibersScratch.Contains(caliber.ShortName)) continue;

                _calibersScratch.Add(caliber.ShortName);
            }
        }

        return string.Join(", ", _calibersScratch);
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
