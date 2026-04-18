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
            
            // Прямой вызов через полный путь, чтобы точно сработало
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Попытка запуска системы ---");

            try 
            {
                UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;

                if (Configuration.Instance.AllowedVehicleIds == null)
                {
                    Configuration.Instance.AllowedVehicleIds = new List<ushort>();
                    Rocket.Core.Logging.Logger.LogWarning("[OBSERVER] Список ID пуст, создан новый список.");
                }

                Rocket.Core.Logging.Logger.Log($"[OBSERVER] В списке разрешенных: {Configuration.Instance.AllowedVehicleIds.Count} ID.");
                
                StartCoroutine(VehicleHealthWatcher());
                Rocket.Core.Logging.Logger.Log("[OBSERVER] Корутина мониторинга запущена успешно.");
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[OBSERVER] Ошибка при загрузке: {ex.Message}");
            }

            Rocket.Core.Logging.Logger.Log("================================================");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("[OBSERVER] Плагин выгружен.");
        }

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
                Rocket.Core.Logging.Logger.Log($"[DEBUG] Новая цель: {v.asset.name} ({v.id}) | Instance: {v.instanceID}");
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
            // Ждем чуть дольше перед началом, чтобы мир прогрузился
            yield return new WaitForSeconds(3.0f);
            Rocket.Core.Logging.Logger.Log("[OBSERVER] Цикл проверки здоровья запущен.");

            while (true)
            {
                try 
                {
                    if (VehicleManager.vehicles == null) goto CycleEnd;

                    var allowedIds = Configuration.Instance.AllowedVehicleIds;

                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        
                        // Проверка на null и нахождение в списке
                        if (vehicle == null || vehicle.isExploded) continue;
                        
                        if (!allowedIds.Contains(vehicle.id)) 
                        {
                            // Если машина была в трекере, но теперь не подходит (например, взорвана или конфиг сменился)
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        VehicleState state = GetVehicleState(vehicle);

                        // Детекция урона
                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            Rocket.Core.Logging.Logger.Log($"[DAMAGE] Техника {vehicle.id} получила {damageTaken} урона.");
                            
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }

                        // Синхронизация состояний
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

                        state.LastHealth = vehicle.health;
                    }
                }
                catch (Exception ex)
                {
                    // Если случилась ошибка внутри цикла, мы её увидим, но цикл не прервется
                    Rocket.Core.Logging.Logger.LogError($"[WATCHER ERROR] {ex.Message}");
                }

                CycleEnd:
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
