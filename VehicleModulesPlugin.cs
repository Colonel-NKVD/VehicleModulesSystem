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
        
        // Эти данные необходимы для работы VehicleTracker.cs
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;
            VehicleManager.onVehicleRegionAdded += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            VehicleManager.onVehicleRegionAdded -= OnVehicleSpawned;
        }

        // Если VehicleRegion вызывает ошибку, используй: byte x, byte y, InteractableVehicle vehicle
        private void OnVehicleSpawned(VehicleRegion region, InteractableVehicle vehicle)
        {
            if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
            {
                vehicle.gameObject.AddComponent<VehicleTracker>();
            }
        }

        // Метод для переводов, который запрашивает трекер
        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "Status_Perfect", "Исправен" },
            { "Status_Damaged", "Поврежден" },
            { "Status_Critical", "Критическое состояние" },
            { "Status_Destroyed", "Уничтожен" }
        };
    }
}
