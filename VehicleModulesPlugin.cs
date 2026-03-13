using Rocket.Unturned;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;
using System.Collections.Generic;
using Rocket.API.Collections;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;
        
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;
            // Подписываемся на событие через координаты
            VehicleManager.onVehicleRegionAdded += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            VehicleManager.onVehicleRegionAdded -= OnVehicleSpawned;
        }

        // Исправлено: принимаем byte x, byte y вместо VehicleRegion
        private void OnVehicleSpawned(byte x, byte y, InteractableVehicle vehicle)
        {
            if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
            {
                vehicle.gameObject.AddComponent<VehicleTracker>();
            }
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "Status_Perfect", "Исправен" },
            { "Status_Damaged", "Поврежден" },
            { "Status_Critical", "Критическое состояние" },
            { "Status_Destroyed", "Уничтожен" }
        };
    }
}
