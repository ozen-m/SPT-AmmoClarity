using AmmoClarity.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Reflection;

namespace AmmoClarity.Core;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class Mod(
    ISptLogger<Mod> logger,
    LocaleService localeService,
    DatabaseService databaseService,
    ModHelper modHelper)
    : IOnLoad
{
    public Task OnLoad()
    {
        ModConfig config = GetConfig();

        AmmoData data = GetAmmoData(config);
        List<Ammo> allAmmo = data.GetAllAmmo();

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
                    localeData[$"{ammo.Id} ShortName"] = ammo.NewShortname;
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

        foreach (TemplateItem ammoTemplate in databaseService.GetItems().Values.Where(i => i.Parent == BaseClasses.AMMO))
        {
            if (!locales.ContainsKey($"{ammoTemplate.Id} Name")) continue;

            string caliberFullName = locales[$"{ammoTemplate.Id} Name"].Split(" ")[0];
            if (!config.Calibers.ContainsKey(caliberFullName)) continue;

            string caliberShortName = config.Calibers[caliberFullName];
            string originalShortName = locales[$"{ammoTemplate.Id} ShortName"];

            // override short name if config specifies NameUpdates
            string newShortName = config.NameUpdates.ContainsKey(originalShortName)
                ? config.NameUpdates[originalShortName]
                : originalShortName;

            newShortName = config.LeadingCaliberName
                ? $"{caliberShortName} {newShortName}"
                : $"{newShortName} {caliberShortName}";

            Caliber caliber = ammoData.GetOrCreateCaliber(caliberFullName, caliberShortName);
            caliber.Ammos.Add(new Ammo(originalShortName, newShortName, ammoTemplate.Id));
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
