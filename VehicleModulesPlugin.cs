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
using HarmonyLib;
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();
        
        public const string HarmonyInstanceId = "com.ironandmud.vehiclemodules";
        private Harmony harmony;

        protected override void Load()
        {
            Instance = this;
            
            harmony = new Harmony(HarmonyInstanceId);
            harmony.PatchAll();

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
            }
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            Rocket.Core.Logging.Logger.Log("--- [HARMONY] Патчи ядра применены ---");
            Rocket.Core.Logging.Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            harmony.UnpatchAll(HarmonyInstanceId);
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("[OBSERVER] Система аварийно остановлена.");
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
            {
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
        }

        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            float waitTime = Configuration.Instance.BandageUseTimeSeconds;
            yield return new WaitForSeconds(waitTime);

            if (player == null || player.Dead || !player.IsInVehicle)
            {
                UnturnedChat.Say(player, "Перевязка прервана!", Color.red);
                yield break;
            }

            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count > 0)
            {
                player.Inventory.removeItem(items[0].page, player.Inventory.getIndex(items[0].page, items[0].jar.x, items[0].jar.y));
                
                byte healAmount = Configuration.Instance.BandageHealAmount;
                player.Player.life.askHeal(healAmount, true, true);
                UnturnedChat.Say(player, "Вы успешно перевязали раны.", Color.green);
            }
            else
            {
                UnturnedChat.Say(player, "Бинт пропал из инвентаря!", Color.red);
            }
        }

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
                    
                    if (vehicle == null || vehicle.isExploded || allowedIds == null || !allowedIds.Contains(vehicle.id)) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    VehicleState state = GetVehicleState(vehicle);

                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        int maxHealth = vehicle.asset.health;

                        // --- МЕХАНИКА НЕПРОБИТИЯ (РИКОШЕТ) ---
                        // Если урон меньше 20% от макс. ХП и прокнул шанс на аннулирование
                        if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                        {
                            ModuleDamageHandler.SendChat(vehicle, $"[БРОНЯ] Непробитие! Попадание ({damageTaken} ед.) прошло по касательной.", Color.green);
                            
                            // Аннулируем урон: моментально лечим технику на количество полученного урона
                            vehicle.askRepair((ushort)damageTaken);
                            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                            
                            state.LastHealth = vehicle.health; // Обновляем состояние, чтобы датчик не сработал снова
                            continue; // Пропускаем проверки на критические модули
                        }

                        // --- ОБРАБОТКА ПРОБИТИЯ ---
                        if (damageTaken >= Configuration.Instance.MinDamageForCrit)
                        {
                            ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона! Состояние: {vehicle.health}/{maxHealth}", Color.yellow);
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }
                    }
                    // Сброс статусов при починке техники на ремстанции или игроками
                    else if (vehicle.health > state.LastHealth)
                    {
                        state.IsTransmissionBroken = false;
                        state.IsFuelTankBroken = false;
                        state.IsGunBroken = false;
                        state.IsOnFire = false;
                        state.IsSmoking = false;
                        state.IsStunned = false;
                    }

                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) 
                        { 
                            rb.velocity = Vector3.zero; 
                            rb.angularVelocity = Vector3.zero; 
                        }
                    }

                    if (state.IsTransmissionBroken)
                    {
                        if (vehicle.isEngineOn)
                        {
                            vehicle.askEngine(CSteamID.Nil, false);
                        }
                        if (vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0;
                            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                        }
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
