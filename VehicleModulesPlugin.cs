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

            if (Configuration.Instance.AllowedVehicleIds == null)
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();

            // Запуск корутины мониторинга
            StartCoroutine(VehicleHealthWatcher());
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [VehicleModules] СИСТЕМА ДАТЧИКОВ АКТИВИРОВАНА ---");
            Rocket.Core.Logging.Logger.Log($"--- Загружено ID техники: {Configuration.Instance.AllowedVehicleIds.Count} ---");
            Rocket.Core.Logging.Logger.Log("================================================");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Instance = null;
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

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            // Небольшая задержка перед началом работы
            yield return new WaitForSeconds(2.0f);

            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                try
                {
                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        if (vehicle == null || vehicle.asset == null) continue;

                        // Если техника уничтожена или не в списке — удаляем из отслеживания
                        if (vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vehicle.id))
                        {
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        VehicleState state = GetVehicleState(vehicle);

                        // --- ГЛАВНЫЙ ДАТЧИК УРОНА ---
                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            
                            // 1. Логируем в консоль (всегда, для отладки)
                            Rocket.Core.Logging.Logger.Log($"[HIT] {vehicle.asset.vehicleName} ({vehicle.id}): -{damageTaken} HP. Текущее: {vehicle.health}");

                            // 2. Оповещаем экипаж (как в старых версиях)
                            ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона! Состояние: {vehicle.health}/{vehicle.asset.health}", Color.yellow);

                            // 3. Проверка на рикошет
                            if (damageTaken < (vehicle.asset.health * 0.15f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                            {
                                // Возвращаем здоровье (визуальный рикошет)
                                vehicle.askRepair((ushort)damageTaken);
                                VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                                ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Броня не пробита.", Color.green);
                            }
                            // 4. Иначе проверяем на критические повреждения
                            else if (damageTaken >= Configuration.Instance.MinDamageForCrit)
                            {
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                        }

                        // Сброс состояний при полной починке
                        if (vehicle.health == vehicle.asset.health && vehicle.health > state.LastHealth)
                        {
                            ResetVehicleEffects(state);
                        }

                        // Применение постоянных эффектов (например, Стан или Поломка трансмиссии)
                        ApplyActiveEffects(vehicle, state);

                        state.LastHealth = vehicle.health;
                    }
                }
                catch (Exception ex)
                {
                    Rocket.Core.Logging.Logger.LogError("[VehicleModules] Ошибка цикла: " + ex.Message);
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private void ApplyActiveEffects(InteractableVehicle vehicle, VehicleState state)
        {
            if (state.IsStunned)
            {
                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }

            if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
            {
                vehicle.batteryCharge = 0;
                VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
            }
        }

        private void ResetVehicleEffects(VehicleState state)
        {
            state.IsFuelTankBroken = false;
            state.IsTransmissionBroken = false;
            state.IsGunBroken = false;
            state.IsOnFire = false;
            state.IsSmoking = false;
            state.IsStunned = false;
        }

        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);
            if (player != null && player.IsInVehicle)
            {
                player.Heal(Configuration.Instance.BandageHealAmount);
                player.Inventory.removeItem(player.Inventory.getIndex(bandageId), 0);
                UnturnedChat.Say(player, "Раны перевязаны.", Color.green);
            }
        }
    }
}
