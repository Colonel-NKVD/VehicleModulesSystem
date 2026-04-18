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

            // Проверка инициализации списка ID
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
            }

            // Запускаем мониторинг
            StartCoroutine(VehicleHealthWatcher());
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [VehicleModules] ПЛАГИН ЗАГРУЖЕН ---");
            Rocket.Core.Logging.Logger.Log($"--- [VehicleModules] ID в белом списке: {Configuration.Instance.AllowedVehicleIds.Count} ---");
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

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            // Небольшая задержка, чтобы мир успел прогрузиться
            yield return new WaitForSeconds(3.0f);
            Rocket.Core.Logging.Logger.Log("[VehicleModules] Поток мониторинга ХП запущен.");

            while (true)
            {
                if (VehicleManager.vehicles == null) 
                { 
                    yield return new WaitForSeconds(1.0f); 
                    continue; 
                }

                try
                {
                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        if (vehicle == null || vehicle.asset == null) continue;

                        ushort vId = vehicle.id;

                        // ПРОВЕРКА: Если техники нет в списке — игнорируем её
                        if (vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vId)) 
                        {
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        VehicleState state = GetVehicleState(vehicle);

                        // Детекция получения урона
                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            
                            // ВАЖНО: Вывод в консоль сервера для диагностики
                            Rocket.Core.Logging.Logger.Log($"[HIT] Техника {vId} (Inst: {vehicle.instanceID}) получила {damageTaken} урона. ХП: {vehicle.health}/{vehicle.asset.health}");

                            // Сообщение игрокам внутри (если они есть)
                            ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона!", Color.yellow);
                            
                            // Логика рикошета
                            if (damageTaken < (vehicle.asset.health * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                            {
                                vehicle.askRepair((ushort)damageTaken);
                                VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                                ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Урон поглощен броней.", Color.green);
                            }
                            else if (damageTaken >= Configuration.Instance.MinDamageForCrit) 
                            {
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                        }

                        // Сброс всех критов при полной починке
                        if (vehicle.health > state.LastHealth && vehicle.health == vehicle.asset.health)
                        {
                            state.IsFuelTankBroken = false;
                            state.IsTransmissionBroken = false;
                            state.IsGunBroken = false;
                            state.IsOnFire = false;
                            state.IsSmoking = false;
                            state.IsStunned = false;
                        }

                        // Активные эффекты (Трансмиссия/Аккумулятор)
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
                    Rocket.Core.Logging.Logger.LogError("[VehicleModules] Ошибка в цикле мониторинга: " + ex.Message);
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Вспомогательный метод для лечения (используется в CommandBandage)
        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);
            
            if (player != null && player.IsInVehicle)
            {
                player.Heal(Configuration.Instance.BandageHealAmount);
                player.Inventory.removeItem(player.Inventory.getIndex(bandageId), 0);
                UnturnedChat.Say(player, "Вы перевязали раны.", Color.green);
            }
        }
    }
}
