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
            
            try 
            {
                harmony = new Harmony(HarmonyInstanceId);
                harmony.PatchAll();
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] Патчи успешно применены ---");
            }
            catch (Exception e) 
            {
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] КРИТИЧЕСКАЯ ОШИБКА: " + e.Message);
            }

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
                Rocket.Core.Logging.Logger.LogWarning("[ВНИМАНИЕ] AllowedVehicleIds был null. Создан пустой список.");
            }
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyInstanceId);
            }
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
            if (player != null && player.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);

            if (player == null || player.Player == null || player.Dead || !player.IsInVehicle)
            {
                yield break; 
            }

            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count > 0)
            {
                player.Inventory.removeItem(items[0].page, player.Inventory.getIndex(items[0].page, items[0].jar.x, items[0].jar.y));
                player.Player.life.askHeal(Configuration.Instance.BandageHealAmount, true, true);
                UnturnedChat.Say(player, ">> ПЕРЕВЯЗКА ЭКИПАЖА ЗАВЕРШЕНА <<", Color.green);
            }
            else
            {
                UnturnedChat.Say(player, "[ОШИБКА] Бинт не найден в инвентаре!", Color.red);
            }
        }

        // ====================================================================
        // СТАРЫЙ ПРОВЕРЕННЫЙ ДАТЧИК ЧЕРЕЗ ЦИКЛ (ПОЛЛИНГ)
        // ====================================================================
        private IEnumerator VehicleHealthWatcher()
        {
            Rocket.Core.Logging.Logger.Log("[SENSOR-DEBUG] Система мониторинга через поллинг запущена.");
            yield return new WaitForSeconds(3.0f);
            
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
                        Rocket.Core.Logging.Logger.Log($"[SENSOR-HEARTBEAT] Датчик активен. Машин в базе: {TrackedVehicles.Count}.");
                        tickCounter = 0;
                    }

                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        if (vehicle == null || vehicle.asset == null) continue;

                        ushort vId = vehicle.asset.id;

                        // Очистка и фильтрация
                        if (vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vId)) 
                        {
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        VehicleState state = GetVehicleState(vehicle);

                        // --- ПРОВЕРКА ПОЛУЧЕНИЯ УРОНА (СТАРЫЙ ПАТЧ) ---
                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            float maxHealth = vehicle.asset.health;

                            // 1. Проверка на рикошет
                            if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                            {
                                vehicle.askRepair((ushort)damageTaken);
                                VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                                ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Броня не пробита.", Color.green);
                            }
                            // 2. Проверка на Крит (если урон выше порога в конфиге)
                            else if (damageTaken >= Configuration.Instance.MinDamageForCrit) 
                            {
                                ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона!", Color.yellow);
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                        }

                        // Сброс состояний при полной починке
                        if (vehicle.health > state.LastHealth && vehicle.health == vehicle.asset.health)
                        {
                            state.IsTransmissionBroken = false;
                            state.IsFuelTankBroken = false;
                            state.IsGunBroken = false;
                            state.IsOnFire = false;
                            state.IsSmoking = false;
                            state.IsStunned = false;
                        }

                        // --- ОБРАБОТКА АКТИВНЫХ СОСТОЯНИЙ ---
                        if (state.IsStunned)
                        {
                            var rb = vehicle.GetComponent<Rigidbody>();
                            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                        }

                        if (state.IsSmoking)
                        {
                            EffectManager.sendEffect(36009, 128, vehicle.transform.position + Vector3.up * 1.5f);
                        }

                        // Фикс трансмиссии: разрядка аккумулятора
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
                    Rocket.Core.Logging.Logger.LogError("[SENSOR-CRITICAL] Ошибка цикла: " + ex.Message);
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
