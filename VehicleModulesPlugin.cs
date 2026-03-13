using System.Collections.Generic;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;
        
        // Поля для работы VehicleTracker.cs
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        protected override void Load()
        {
            Instance = this;

            // КРЕАТИВНЫЙ ОБХОД: Вместо спавна машин следим за игроками.
            // При входе любого игрока проверяем все машины на наличие трекера.
            U.Events.OnPlayerConnected += OnPlayerConnected;
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            // Сканируем все машины в мире и вешаем наш компонент
            foreach (InteractableVehicle vehicle in VehicleManager.vehicles)
            {
                if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
                {
                    vehicle.gameObject.AddComponent<VehicleTracker>();
                }
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
