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
using Logger = Rocket.Core.Logging.Logger; // Для удобства

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
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            if (Configuration.Instance.AllowedVehicleIds == null || Configuration.Instance.AllowedVehicleIds.Count == 0)
            {
                Logger.LogWarning("[OBSERVER] Список AllowedVehicleIds пуст! Плагин не будет обрабатывать технику.");
            }
            else 
            {
                Logger.Log($"[OBSERVER] Загружено ID техники: {Configuration.Instance.AllowedVehicleIds.Count}");
            }
            
            Logger.Log("================================================");
            Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Logger.Log("[OBSERVER] Система остановлена.");
        }

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
                // Лог для отладки отслеживания
                Logger.Log($"[DEBUG] Начато отслеживание техники: {v.asset.name} (ID: {v.id})");
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
            yield return new WaitForSeconds(1.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                var allowedIds = Configuration.Instance?.AllowedVehicleIds;

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    
                    // Если техника не в списке или уничтожена — убираем из трекера
                    if (vehicle == null || vehicle.isExploded || allowedIds == null || !allowedIds.Contains(vehicle.id)) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    VehicleState state = GetVehicleState(vehicle);

                    // Детекция урона
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        
                        // ЛОГ В КОНСОЛЬ (теперь ты увидишь это без захода в танк)
                        Logger.Log($"[DAMAGE] Техника {vehicle.id} получила {damageTaken} урона. HP: {vehicle.health}/{vehicle.asset.health}");
                        
                        ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона!", Color.yellow);
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }

                    // Логика блокировки движения при контузии
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) 
                        { 
                            rb.velocity = Vector3.zero; 
                            rb.angularVelocity = Vector3.zero; 
                        }
                    }

                    // Логика поломки трансмиссии (постоянный разряд батареи)
                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        VehicleManager.sendVehicleBattery(vehicle, 0); // Исправлено: синхронизация именно батареи
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
