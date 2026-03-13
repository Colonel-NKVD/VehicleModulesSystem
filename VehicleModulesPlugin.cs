using Rocket.Unturned;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events; // Важно для событий
using SDG.Unturned;
using UnityEngine;
using System.Collections.Generic;
using Rocket.API.Collections;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;

        // Эти поля необходимы для работы твоего VehicleTracker.cs
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;

            // Используем событие RocketMod — оно стабильнее
            UnturnedVehicleEvents.OnVehicleSpawned += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            UnturnedVehicleEvents.OnVehicleSpawned -= OnVehicleSpawned;
        }

        // Сигнатура RocketMod принимает только сам транспорт
        private void OnVehicleSpawned(InteractableVehicle vehicle)
        {
            if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
            {
                vehicle.gameObject.AddComponent<VehicleTracker>();
            }
        }

        // Переводы, которые запрашивает трекер
        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "Status_Perfect", "Исправен" },
            { "Status_Damaged", "Поврежден" },
            { "Status_Critical", "Критическое состояние" },
            { "Status_Destroyed", "Уничтожен" }
        };
    }
}
