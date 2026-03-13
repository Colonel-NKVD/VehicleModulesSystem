using Rocket.Unturned;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;

        protected override void Load()
        {
            Instance = this;

            // Используем событие добавления в регион
            VehicleManager.onVehicleRegionAdded += OnVehicleSpawned;
        }

        protected override void Unload()
        {
            VehicleManager.onVehicleRegionAdded -= OnVehicleSpawned;
        }

        // Обновленная сигнатура метода: принимает регион и транспорт
        private void OnVehicleSpawned(VehicleRegion region, InteractableVehicle vehicle)
        {
            if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
            {
                vehicle.gameObject.AddComponent<VehicleTracker>();
            }
        }
    }
}
