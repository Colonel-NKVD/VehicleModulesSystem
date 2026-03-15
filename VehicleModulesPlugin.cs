using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat; // Обязательно для работы с чатом
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

    // Примечание: предполагается, что файл VehicleModulesConfig.cs у вас остался в проекте. 
    // Если вы удалили вообще всё, замените RocketPlugin<VehicleModulesConfig> на просто RocketPlugin
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        // Хранилище датчиков: InstanceID -> Данные о машине
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Базовая система мониторинга запущена ---");
            
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
                        {
                            TrackedVehicles.Remove(vehicle.instanceID);
                        }
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
                        
                        Rocket.Core.Logging.Logger.Log($"[NEW] Датчик установлен: {vehicle.asset.vehicleName} (ID: {vehicle.instanceID})");
                        continue;
                    }

                    // 2. ОТСЛЕЖИВАНИЕ УРОНА (сравнение текущего ХП с сохраненным)
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        
                        // Логируем в консоль
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE] Машина {vehicle.asset.vehicleName} получила {damageTaken} урона!");
                        
                        // ВЫВОД В ИГРОВОЙ ЧАТ
                        // Сообщение о факте урона (желтым цветом)
                        UnturnedChat.Say($"[Датчик] {vehicle.asset.vehicleName} получил {damageTaken} ед. урона!", Color.yellow);
                        
                        // База для будущих модулей: сообщение о возможном повреждении (красным цветом)
                        // Пока что выводим это, если урон больше 10, чтобы вы видели, как это работает
                        if (damageTaken > 10) 
                        {
                            UnturnedChat.Say($"[Внимание] Зафиксирован сильный удар! Возможны повреждения внутренних модулей.", Color.red);
                        }
                    }
                    // Обработка ремонта (если ХП стало больше, чем было)
                    else if (vehicle.health > state.LastHealth)
                    {
                        Rocket.Core.Logging.Logger.Log($"[REPAIR] Техника {vehicle.asset.vehicleName} была починена.");
                    }

                    // 3. ОБНОВЛЕНИЕ СОСТОЯНИЯ
                    // Мы обновляем LastHealth только после проверки на урон или ремонт
                    state.LastHealth = vehicle.health;
                }

                // Сканирование каждые 0.5 секунды для высокой точности
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
