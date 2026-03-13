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
        
        // Поля, необходимые для VehicleTracker.cs
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;
            // Прямая подписка на стандартный делегат Unturned
            VehicleManager.onVehicleRegionAdded += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            VehicleManager.onVehicleRegionAdded -= OnVehicleSpawned;
        }

        // Самая стабильная сигнатура для старых и средних версий игры
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
