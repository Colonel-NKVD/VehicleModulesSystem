using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace VehicleModulesSystem
{
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
        public bool IsRepairing;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            
            Console.WriteLine("!!! [DEBUG] VEHICLE_MODULES_SYSTEM: STARTING LOAD !!!");
            Logger.Log("================================================");
            Logger.Log("--- [OBSERVER] Инициализация системы модулей ---");

            try 
            {
                UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;

                if (VehicleManager.vehicles == null)
                    Logger.LogWarning("[OBSERVER] VehicleManager еще не инициализирован, ожидаем...");

                if (Configuration.Instance.AllowedVehicleIds == null || Configuration.Instance.AllowedVehicleIds.Count == 0)
                {
                    Configuration.Instance.AllowedVehicleIds = new List<ushort>();
                    Logger.LogWarning("[OBSERVER] ВНИМАНИЕ: Список AllowedVehicleIds пуст! Плагин не будет обрабатывать технику.");
                }
                else
                {
                    Logger.Log($"[OBSERVER] Загружено разрешенных ID техники: {Configuration.Instance.AllowedVehicleIds.Count}");
                    // Выводим все загруженные ID для дебага
                    foreach(var id in Configuration.Instance.AllowedVehicleIds)
                    {
                        Logger.Log($"[OBSERVER] Взят на прицел ID: {id}");
                    }
                }
                
                StartCoroutine(VehicleHealthWatcher());
                Logger.Log("[OBSERVER] Корутина мониторинга запущена успешно.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[OBSERVER] КРИТИЧЕСКАЯ ОШИБКА ПРИ ЗАГРУЗКЕ: {ex.Message}");
            }

            Logger.Log("================================================");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Logger.Log("[OBSERVER] Плагин успешно выгружен.");
        }

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
                Logger.Log($"[DEBUG] Новая цель в трекере: {v.asset.name} (ID: {v.asset.id}) | Instance: {v.instanceID}");
            }
            return state;
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            Logger.Log("[OBSERVER] Рабочий цикл проверки состояния запущен.");

            while (true)
            {
                try 
                {
                    if (VehicleManager.vehicles == null) goto CycleEnd;

                    var allowedIds = Configuration.Instance.AllowedVehicleIds;

                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        // Внутренний try-catch: если одна машина забагована, остальные продолжат проверяться
                        try 
                        {
                            var vehicle = VehicleManager.vehicles[i];
                            
                            // 1. Строгая проверка на null и "смерть" техники
                            if (vehicle == null || vehicle.asset == null || vehicle.isExploded || vehicle.isDead) 
                                continue;
                            
                            // 2. Безопасное получение ID из ассета
                            ushort currentId = vehicle.asset.id;
                            
                            // 3. Проверка на нахождение в списке
                            if (allowedIds == null || !allowedIds.Contains(currentId)) 
                            {
                                if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                {
                                    TrackedVehicles.Remove(vehicle.instanceID);
                                    Logger.Log($"[DEBUG] Техника {vehicle.asset.name} ({currentId}) исключена (не в списке).");
                                }
                                continue;
                            }

                            // 4. Получаем состояние (гарантированно нужная машина)
                            VehicleState state = GetVehicleState(vehicle);

                            // 5. Детекция урона
                            if (vehicle.health < state.LastHealth)
                            {
                                int damageTaken = state.LastHealth - vehicle.health;
                                Logger.Log($"[DAMAGE] Урон по {vehicle.asset.name} ({currentId}): {damageTaken} ед. HP: {vehicle.health}/{vehicle.asset.health}");
                                
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                            // 6. Детекция ручной починки (Горелкой)
                            else if (vehicle.health > state.LastHealth)
                            {
                                Logger.Log($"[REPAIR] Техника {vehicle.asset.name} починена: +{vehicle.health - state.LastHealth} HP.");
                            }

                            // 7. Применение активных эффектов (Контузия / Трансмиссия)
                            if (state.IsStunned)
                            {
                                var rb = vehicle.GetComponent<Rigidbody>();
                                if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                            }

                            if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                            {
                                vehicle.batteryCharge = 0;
                                VehicleManager.sendVehicleBatteryCharge(vehicle, 0);
                            }

                            // 8. Сохраняем актуальное здоровье для следующего тика
                            state.LastHealth = vehicle.health;
                        }
                        catch (Exception innerEx)
                        {
                            Logger.LogError($"[WATCHER] Ошибка обработки конкретной машины: {innerEx.Message}");
                        }
                    }
                }
                catch (Exception outerEx)
                {
                    Logger.LogError($"[WATCHER ERROR] Глобальная ошибка цикла: {outerEx.Message}");
                }

                CycleEnd:
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
