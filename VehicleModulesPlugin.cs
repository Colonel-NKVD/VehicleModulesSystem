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
        
        public const string HarmonyInstanceId = "com.ironandmud.vehiclemodules";
        private Harmony harmony;

        protected override void Load()
        {
            Instance = this;
            
            // Инициализация Harmony
            try 
            {
                harmony = new Harmony(HarmonyInstanceId);
                harmony.PatchAll();
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] Патчи успешно применены ---");
            }
            catch (Exception e) 
            {
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] ОШИБКА: " + e.Message);
            }

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            Rocket.Core.Logging.Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            harmony?.UnpatchAll(HarmonyInstanceId);
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

        // КОРУТИНА БИНТОВАНИЯ (Команда /b)
        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);

            if (player == null || player.Dead || !player.IsInVehicle)
            {
                UnturnedChat.Say(player, "Перевязка прервана!", Color.red);
                yield break;
            }

            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count > 0)
            {
                player.Inventory.removeItem(items[0].page, player.Inventory.getIndex(items[0].page, items[0].jar.x, items[0].jar.y));
                player.Player.life.askHeal(Configuration.Instance.BandageHealAmount, true, true);
                UnturnedChat.Say(player, "Вы успешно перевязали раны.", Color.green);
            }
        }

        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vehicle.id)) continue;

                    VehicleState state = GetVehicleState(vehicle);

                    // --- ЛОГИКА УРОНА ---
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        float maxHealth = vehicle.asset.health;

                        // 1. Механика непробития (Аннулирование урона)
                        if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                        {
                            vehicle.askRepair((ushort)damageTaken);
                            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                            UnturnedChat.Say("Рикошет! Урон аннулирован.", Color.green); // Опционально для теста
                            state.LastHealth = vehicle.health;
                            continue;
                        }

                        // 2. Проверка на минимальный урон для критов
                        if (damageTaken >= Configuration.Instance.MinDamageForCrit)
                        {
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }
                    }
                    // --- ЛОГИКА ПОЧИНКИ (Ремстанция) ---
                    else if (vehicle.health > state.LastHealth)
                    {
                        // Если ХП выросло — сбрасываем критические повреждения
                        state.IsTransmissionBroken = false;
                        state.IsFuelTankBroken = false;
                        state.IsGunBroken = false;
                        state.IsOnFire = false;
                        state.IsSmoking = false;
                        state.IsStunned = false;
                    }

                    // --- ЭФФЕКТ ЗАДЫМЛЕНИЯ (1 хп/сек) ---
                    if (state.IsSmoking)
                    {
                        EffectManager.sendEffect(Configuration.Instance.SmokeVisualEffectId, 128, vehicle.transform.position + Vector3.up * 1.5f);
                        foreach (var passenger in vehicle.passengers)
                        {
                            if (passenger.player != null)
                                passenger.player.player.life.askDamage(1, Vector3.up, EDeathCause.BREATH, ELimb.SPINE, CSteamID.Nil, out _);
                        }
                    }

                    // --- КОСТЫЛЬ ТРАНСМИССИИ (Слив батареи + Инерция) ---
                    if (state.IsTransmissionBroken)
                    {
                        if (vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0;
                            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                        }
                        
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null && rb.velocity.magnitude > 0.5f)
                            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.deltaTime);
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
