using System.Reflection;
using AmmoClarity.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace AmmoClarity.Core;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 5)]
public class Mod(
    ISptLogger<Mod> logger,
    LocaleService localeService,
    DatabaseService databaseService,
    ModHelper modHelper,
    ItemHelper itemHelper)
    : IOnLoad
{
    public Task OnLoad()
    {
        ModConfig config = GetConfig();

        AmmoData data = GetAmmoData(config);
        List<Ammo> allAmmo = data.GetAllAmmo();
        List<Ammo> allMagazines = data.AllMagazines;

        if (config.LogAllAmmos)
        {
            LogAllAmmos(data);
        }

        if (!config.STFU)
        {
            ShortNameWarnings(data);
        }

        if (databaseService.GetLocales().Global.TryGetValue(config.Language, out var lazyLoadedValue))
        {
            lazyLoadedValue.AddTransformer(localeData =>
            {
                if (localeData is null) return null;

                foreach (Ammo ammo in allAmmo)
                {
                    localeData[$"{ammo.Id} Name"] = ammo.NewLongName;
                    localeData[$"{ammo.Id} ShortName"] = ammo.NewShortname;
                }

                foreach (Ammo magazine in allMagazines)
                {
                    localeData[$"{magazine.Id} Name"] = magazine.NewLongName;
                    localeData[$"{magazine.Id} ShortName"] = magazine.NewShortname;
                }

                return localeData;
            });
        }

        return Task.CompletedTask;
    }

    public ModConfig GetConfig()
    {
        string pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        return modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json");
    }

    public AmmoData GetAmmoData(ModConfig config)
    {
        Dictionary<string, string> locales = localeService.GetLocaleDb();
        AmmoData ammoData = new();

        // AMMO
        MongoId[] ammoBaseClasses = [BaseClasses.AMMO, BaseClasses.AMMO_BOX];
        foreach (TemplateItem ammoTemplate in databaseService.GetItems().Values.Where(i => itemHelper.IsOfBaseclasses(i.Id, ammoBaseClasses)))
        {
            if (!locales.ContainsKey($"{ammoTemplate.Id} Name")) continue;

            string caliberFullName = locales[$"{ammoTemplate.Id} Name"].Split(" ")[0];
            if (!config.Calibers.TryGetValue(caliberFullName, out string? caliberShortName))
            {
                if (config.LogAllAmmos)
                {
                    logger.Warning($"Missing config caliber for: {caliberFullName}");
                }
                continue;
            }

            string originalShortName = locales[$"{ammoTemplate.Id} ShortName"];

            // override short name if config specifies NameUpdates
            string newShortName = config.NameUpdates.GetValueOrDefault(originalShortName, originalShortName);

            newShortName = config.LeadingCaliberName
                ? $"{caliberShortName} {newShortName}"
                : $"{newShortName} {caliberShortName}";

            string penDam = string.Empty;
            if (ammoTemplate.Properties?.StackSlots is { } stackSlots)
            {
                // AmmoBox
                var ammoId = stackSlots.FirstOrDefault()?.Properties?.Filters?.FirstOrDefault()?.Filter?.FirstOrDefault();
                if (ammoId is not null)
                {
                    var ammo = itemHelper.GetItem(ammoId.Value);
                    if (ammo.Key)
                    {
                        var ammoProp = ammo.Value!.Properties!;
                        penDam = $"({ammoProp.PenetrationPower}/{ammoProp.Damage}) ";
                    }
                }
            }
            else
            {
                // Ammo
                penDam = $"({ammoTemplate.Properties!.PenetrationPower}/{ammoTemplate.Properties.Damage}) ";
            }
            string newLongName = $"{penDam}{locales[$"{ammoTemplate.Id} Name"]}";

            Caliber caliber = ammoData.GetOrCreateCaliber(caliberFullName, caliberShortName);
            caliber.Ammos.Add(new Ammo(originalShortName, newShortName, newLongName, ammoTemplate.Id));
        }

        // MAGAZINES
        foreach (TemplateItem magazineTemplate in databaseService.GetItems().Values.Where(i => itemHelper.IsOfBaseclass(i.Id, BaseClasses.MAGAZINE)))
        {
            if (!locales.ContainsKey($"{magazineTemplate.Id} Name")) continue;

            var caliberShortName = ammoData.GetFirstCaliberShortName(magazineTemplate);
            var magCapacity = magazineTemplate.Properties?.Cartridges?.FirstOrDefault()?.MaxCount;
            if (string.IsNullOrEmpty(caliberShortName) || magCapacity is null) continue;

            var originalShortName = locales[$"{magazineTemplate.Id} ShortName"];
            var newShortName = $"{magCapacity} {caliberShortName}";
            var originalName = locales[$"{magazineTemplate.Id} Name"];
            var newLongName = $"{magCapacity} {caliberShortName} {originalName}";
            ammoData.AllMagazines.Add(new Ammo(originalShortName, newShortName, newLongName, magazineTemplate.Id));
        }

        return ammoData;
    }

    public void ShortNameWarnings(AmmoData ammoData)
    {
        bool anyFails = false;
        foreach (Ammo ammo in ammoData.GetAllAmmo())
        {
            if (ammo.NewShortname.Length > 9)
            {
                anyFails = true;
                logger.Error($"{ammo.NewShortname} ({ammo.OriginalShortname}'s new short name is {ammo.NewShortname.Length - 9} char(s) too long!)");
            }
        }

        if (anyFails)
        {
            logger.Error("[AmmoClarity]: Check errors above!");
        }
    }

    public void LogAllAmmos(AmmoData ammoData)
    {
        foreach (Caliber caliber in ammoData.Calibers)
        {
            logger.Success(caliber.FullName);

            foreach (Ammo ammo in caliber.Ammos)
            {
                if (ammo.NewShortname.Length <= 9)
                {
                    logger.Warning(ammo.NewShortname);
                }
                else
                {
                    logger.Error(ammo.NewShortname);
                }
            }
        }
    }
}
