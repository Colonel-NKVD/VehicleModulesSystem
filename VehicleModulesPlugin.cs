using System.Collections.Generic;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfiguration>
    {
        public static VehicleModulesPlugin Instance;
        
        // Поля для VehicleTracker
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        public bool IsDirty = false;

        // Таймер для сканера
        private float scanTimer = 0f;

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem загружен! Запуск авто-сканера...", System.ConsoleColor.Green);
            
            // Запускаем первичное сканирование сразу при загрузке плагина
            ScanVehicles();
        }

        protected override void Unload()
        {
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem выгружен.", System.ConsoleColor.Red);
        }

        // Встроенный цикл Unity, работает независимо от событий Unturned
        public void FixedUpdate()
        {
            scanTimer += Time.fixedDeltaTime;
            
            // Каждые 3 секунды плагин проверяет, не появились ли новые машины
            if (scanTimer >= 3f) 
            {
                scanTimer = 0f;
                ScanVehicles();
            }
        }

        private void ScanVehicles()
        {
            // FindObjectsOfType — метод Unity, который 100% найдет все машины на карте
            InteractableVehicle[] vehicles = FindObjectsOfType<InteractableVehicle>();
            int newTrackersAdded = 0;

            foreach (InteractableVehicle vehicle in vehicles)
            {
                // Если машина есть, но на ней нет нашего трекера — вешаем его
                if (vehicle != null && vehicle.gameObject.GetComponent<VehicleTracker>() == null)
                {
                    vehicle.gameObject.AddComponent<VehicleTracker>();
                    newTrackersAdded++;
                }
            }

            // Логируем только если нашли новые машины (чтобы не спамить в консоль)
            if (newTrackersAdded > 0)
            {
                Rocket.Core.Logging.Logger.LogWarning($"[Iron & Mud] Интегрирована система модулей на {newTrackersAdded} новых ед. техники.");
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
