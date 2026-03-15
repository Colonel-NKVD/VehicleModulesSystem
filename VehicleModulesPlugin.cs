using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleState
    {
        public ushort LastKnownHealth; // Основной показатель для детекции урона
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
        public bool IsSmoking;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("--- VehicleModules: Запуск системы мониторинга урона ---");
            StartCoroutine(SafeStart());
        }

        private IEnumerator SafeStart()
        {
            yield return new WaitForSeconds(3.0f);
            StartCoroutine(DamageWatcherLoop());
            Rocket.Core.Logging.Logger.Log("--- VehicleModules: Наблюдатель активен (Интервал: 5 сек) ---");
        }

        private IEnumerator DamageWatcherLoop()
        {
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1f); continue; }

                // Работаем через цикл for для стабильности
                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;

                    // Проверка ID из конфига
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    // РЕГИСТРАЦИЯ ДАТЧИКА
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastKnownHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        Rocket.Core.Logging.Logger.Log($"[SCAN] Установлен датчик ХП на {vehicle.asset.vehicleName} ({vehicle.instanceID}). ХП: {vehicle.health}");
                        continue;
                    }

                    // ПРОВЕРКА УРОНА (Главное исправление)
                    if (vehicle.health < state.LastKnownHealth)
                    {
                        int damageAmount = state.LastKnownHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE DETECTED] Техника {vehicle.instanceID} получила урон! (Было: {state.LastKnownHealth}, Стало: {vehicle.health}, Урон: {damageAmount})");
                        
                        // Обработка логики повреждений
                        ProcessModuleDamage(vehicle, state, damageAmount);
                    }
                    else if (vehicle.health > state.LastKnownHealth)
                    {
                        // Если технику починили, просто обновляем данные без логов
                        state.LastKnownHealth = vehicle.health;
                    }

                    // DEBUG: Искусственный тест (гарантированный урон раз в 5 сек)
                    if (UnityEngine.Random.value < 0.20f) 
                    {
                        Rocket.Core.Logging.Logger.Log($"[DEBUG TEST] Имитация попадания по {vehicle.instanceID}");
                        ProcessModuleDamage(vehicle, state, 10);
                    }

                    // Обновляем последнее известное здоровье для следующего цикла
                    state.LastKnownHealth = vehicle.health;

                    // Физическая блокировка трансмиссии
                    if (state.IsTransmissionBroken) ApplyTransmissionLock(vehicle);
                }

                yield return new WaitForSeconds(5.0f);
            }
        }

        private void ProcessModuleDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            // Шансы в режиме Debug (0.3 базовая вероятность + бонус от урона)
            float chance = 0.3f + (dmg / 500f);

            if (!s.IsFuelTankBroken && UnityEngine.Random.value < chance)
            {
                s.IsFuelTankBroken = true;
                Rocket.Core.Logging.Logger.Log($"[MODULE] Бак пробит на {v.instanceID}");
                Notify(v, "ВНИМАНИЕ: Пробит топливный бак!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && UnityEngine.Random.value < chance)
            {
                s.IsTransmissionBroken = true;
                Rocket.Core.Logging.Logger.Log($"[MODULE] Трансмиссия уничтожена на {v.instanceID}");
                Notify(v, "КРИТ: Трансмиссия выведена из строя!", Color.red);
            }
        }

        private void ApplyTransmissionLock(InteractableVehicle v)
        {
            var rb = v.GetComponent<Rigidbody>();
            if (rb != null && rb.velocity.magnitude > 0.1f)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 15);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private void Notify(InteractableVehicle v, string m, Color c)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, m, c);
        }
    }
}
