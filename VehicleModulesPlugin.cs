using System.Collections.Generic;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;
        
        // Поля, без которых VehicleTracker.cs выдаст ошибку при компиляции
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;

            // Используем наиболее вероятное имя события для твоей версии
            VehicleManager.onVehicleSpawned += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            VehicleManager.onVehicleSpawned -= OnVehicleSpawned;
        }

        private void OnVehicleSpawned(InteractableVehicle vehicle)
        {
            if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
            {
                vehicle.gameObject.AddComponent<VehicleTracker>();
            }
        }

        // Переводы для вывода статуса в CommandVStat
        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "Status_Perfect", "Исправен" },
            { "Status_Damaged", "Поврежден" },
            { "Status_Critical", "Критическое состояние" },
            { "Status_Destroyed", "Уничтожен" }
        };
    }
}
