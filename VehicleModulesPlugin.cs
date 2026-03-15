using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    // Класс для хранения состояния каждой конкретной машины
    public class VehicleState
    {
        public ushort LastHealth;
        public uint InstanceID;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        // Хранилище датчиков: InstanceID -> Данные о машине
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            
            // Запускаем корутину-наблюдатель
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система остановлена ---");
        }

        private IEnumerator VehicleHealthWatcher()
        {
            // Ждем инициализации мира
            yield return new WaitForSeconds(3.0f);

            while (true)
            {
                if (VehicleManager.vehicles == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // Перебираем всю технику на сервере
                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    
                    // Пропускаем пустые объекты или уже взорванные машины
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) 
                    {
                        // Если машина была в списке, но теперь взорвана/удалена — убираем датчик
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    // 1. УСТАНОВКА ДАТЧИКА (если машина новая для плагина)
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState 
                        { 
                            InstanceID = vehicle.instanceID,
                            LastHealth = vehicle.health 
                        };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        
                        Rocket.Core.Logging.Logger.Log($"[NEW] Датчик установлен: {vehicle.asset.vehicleName} (ID: {vehicle.instanceID}) | Текущее ХП: {vehicle.health}");
                        continue;
                    }

                    // 2. ОТСЛЕЖИВАНИЕ УРОНА (сравнение текущего ХП с сохраненным)
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        
                        // Логируем ВЕСЬ полученный урон, как ты и просил
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE] Машина {vehicle.asset.vehicleName} ({vehicle.instanceID}) получила {damageTaken} урона! (Осталось ХП: {vehicle.health})");
                        
                        // Здесь в будущем будет вызов логики модулей:
                        // ProcessModules(vehicle, damageTaken);
                    }

                    // 3. ОБНОВЛЕНИЕ СОСТОЯНИЯ
                    // Мы обновляем LastHealth только после проверки на урон
                    state.LastHealth = vehicle.health;
                }

                // Сканирование каждые 0.5 секунды для высокой точности отслеживания твоих действий
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
