using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            // Проверка инициализации списка
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
            }
            
            StartCoroutine(VehicleHealthWatcher());
            Rocket.Core.Logging.Logger.Log("--- [VehicleModules] ПЛАГИН ЗАПУЩЕН ---");
            Rocket.Core.Logging.Logger.Log($"--- [VehicleModules] В белом списке ID: {string.Join(", ", Configuration.Instance.AllowedVehicleIds)} ---");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
            }
            return state;
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            // Ждем загрузки карты
            yield return new WaitForSeconds(5.0f);
            Rocket.Core.Logging.Logger.Log("[SENSOR] Датчик поллинга активирован.");
            
            int tickCounter = 0;

            while (true)
            {
                if (VehicleManager.vehicles == null) 
                { 
                    yield return new WaitForSeconds(1.0f); 
                    continue; 
                }

                try
                {
                    tickCounter++;
                    if (tickCounter >= 20) 
                    {
                        // Каждые 10 секунд пишем статус в консоль
                        Rocket.Core.Logging.Logger.Log($"[SENSOR-HEARTBEAT] Машин в мире: {VehicleManager.vehicles.Count}, Отслеживается: {TrackedVehicles.Count}");
                        tickCounter = 0;
                    }

                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        if (vehicle == null || vehicle.asset == null) continue;

                        ushort vId = vehicle.id; // Используем vehicle.id напрямую

                        // 1. ПРОВЕРКА ВАЙТЛИСТА (Самое важное место)
                        if (vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vId)) 
                        {
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        VehicleState state = GetVehicleState(vehicle);

                        // 2. ДЕТЕКЦИЯ ЛЮБОГО ИЗМЕНЕНИЯ ХП
                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            
                            // ДИАГНОСТИЧЕСКИЙ ЛОГ В КОНСОЛЬ (Будет виден всегда)
                            Rocket.Core.Logging.Logger.Log($"[HIT] Техника ID {vId} получила {damageTaken} урона. (Текущее ХП: {vehicle.health})");

                            // Логика рикошета
                            if (damageTaken < (vehicle.asset.health * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                            {
                                vehicle.askRepair((ushort)damageTaken);
                                VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                                ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Броня не пробита.", Color.green);
                            }
                            // Логика Крита
                            else if (damageTaken >= Configuration.Instance.MinDamageForCrit) 
                            {
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                        }

                        // Сброс состояний при починке
                        if (vehicle.health > state.LastHealth && vehicle.health == vehicle.asset.health)
                        {
                            state.IsFuelTankBroken = false;
                            state.IsTransmissionBroken = false;
                            state.IsGunBroken = false;
                            state.IsOnFire = false;
                            state.IsSmoking = false;
                            state.IsStunned = false;
                        }

                        // Обработка активных эффектов
                        if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0;
                            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel); 
                        }

                        state.LastHealth = vehicle.health;
                    }
                }
                catch (Exception ex)
                {
                    Rocket.Core.Logging.Logger.LogError("[SENSOR-CRITICAL] Ошибка: " + ex.Message);
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
