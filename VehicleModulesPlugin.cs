using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using Rocket.Unturned.Events;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    // Класс состояния с поддержкой всех модулей
    public class VehicleState
    {
        public ushort LastHealth;
        public uint InstanceID;
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
        public bool IsSmoking;
        public bool IsStunned;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;

            // ФИКС ВЕЧНОГО КУРСОРA: Используем надежное событие RocketMod
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система остановлена ---");
        }

        // Обработчик смерти для сброса блокировки управления
        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player != null && player.Player != null)
            {
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
        }

        // ОБЪЕДИНЕННЫЙ ЭТАЛОННЫЙ ДАТЧИК
        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    
                    // 1. Проверка валидности
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    // 2. Инициализация состояния
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { InstanceID = vehicle.instanceID, LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        Rocket.Core.Logging.Logger.Log($"[NEW] Датчик установлен: {vehicle.asset.vehicleName}");
                        continue;
                    }

                    // 3. МАГИЯ ОТСЛЕЖИВАНИЯ УРОНА
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE] {vehicle.asset.vehicleName} получил {damageTaken} урона!");
                        UnturnedChat.Say($"[Датчик] {vehicle.asset.vehicleName} получил {damageTaken} ед. урона!", Color.yellow);
                        
                        // Отправка данных в обработчик эффектов
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }
                    else if (vehicle.health > state.LastHealth)
                    {
                        Rocket.Core.Logging.Logger.Log($"[REPAIR] Техника {vehicle.asset.vehicleName} была починена.");
                    }

                    // 4. ПРИНУДИТЕЛЬНОЕ ПОДАВЛЕНИЕ (ФИЗИКА И АККУМУЛЯТОР)
                    // Остановка танка при контузии или поломке трансмиссии
                    if (state.IsStunned || (state.IsTransmissionBroken && vehicle.batteryCharge == 0))
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) 
                        { 
                            rb.velocity = Vector3.zero; 
                            rb.angularVelocity = Vector3.zero; 
                        }
                    }

                    // ФИКС АККУМУЛЯТОРА: Постоянный сброс в 0 при выбитой трансмиссии
                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
