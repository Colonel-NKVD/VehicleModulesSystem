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
using Steamworks;

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
            
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
            }
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Датчик урона: Режим ПОЛЛИНГА ---");
            Rocket.Core.Logging.Logger.Log("--- Протокол: Дизельпанк / Grimdark 1917+ ---");
            Rocket.Core.Logging.Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("[OBSERVER] Система остановлена.");
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
            if (player != null && player.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        // РАБОЧИЙ ДАТЧИК ИЗ ТВОЕЙ СТАРОЙ ВЕРСИИ
        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                var allowedIds = Configuration.Instance?.AllowedVehicleIds;

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    
                    // Проверка списка техники (из твоего рабочего кода)
                    if (vehicle == null || vehicle.isExploded || allowedIds == null || !allowedIds.Contains(vehicle.id)) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    VehicleState state = GetVehicleState(vehicle);

                    // --- СЕКЦИЯ ДЕТЕКЦИИ УРОНА ---
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        float maxHealth = vehicle.asset.health;

                        // Логика рикошета (встроена в датчик)
                        if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < 0.15f) // Шанс можно вынести в конфиг
                        {
                            vehicle.askRepair((ushort)damageTaken);
                            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                            ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Броня выдержала.", Color.green);
                        }
                        else
                        {
                            // Вызов обработки критов
                            ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона!", Color.yellow);
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }
                    }

                    // --- ОБРАБОТКА СОСТОЯНИЙ (Контузия, Трансмиссия и т.д.) ---
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) 
                        { 
                            rb.velocity = Vector3.zero; 
                            rb.angularVelocity = Vector3.zero; 
                        }
                    }

                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                    }

                    // Обновляем последнее известное ХП для следующей проверки
                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
