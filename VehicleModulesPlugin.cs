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
    // Расширенное состояние модуля
    public class VehicleState
    {
        public ushort LastHealth;
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        // Словарь состояний: ключ - уникальный ID экземпляра машины
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            
            // Запускаем корутину-наблюдатель. Это сердце плагина.
            StartCoroutine(VehicleHealthObserver());
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Наблюдатель за здоровьем активен. Модули запущены.");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // Профессиональный цикл отслеживания
        private IEnumerator VehicleHealthObserver()
        {
            while (true)
            {
                // Используем глобальный менеджер машин Unturned
                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null) continue;

                    // Проверяем, входит ли ID машины в список обрабатываемых в конфиге
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    // Если машина взорвана, удаляем её из слежки
                    if (vehicle.isExploded)
                    {
                        TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    // Если машина новая, регистрируем её начальное здоровье
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // КЛЮЧЕВОЙ МОМЕНТ: Сравнение текущего здоровья с прошлым кадром
                    if (vehicle.health < state.LastHealth)
                    {
                        // Вычисляем размер полученного урона
                        int damageTaken = state.LastHealth - vehicle.health;
                        ExecuteDamageLogic(vehicle, state, damageTaken);
                    }

                    // Обновляем "последнее известное здоровье"
                    state.LastHealth = vehicle.health;
                }

                // Пауза 0.1 сек (оптимально для производительности и точности)
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ExecuteDamageLogic(InteractableVehicle v, VehicleState s, int damage)
        {
            var cfg = Configuration.Instance;

            // Вероятность повреждения зависит от тяжести урона
            float damageMultiplier = damage / 100f; 

            // 1. Пробитие бака (утечка топлива)
            if (!s.IsFuelTankBroken && UnityEngine.Random.value < (cfg.ChanceFuelLeak + damageMultiplier))
            {
                s.IsFuelTankBroken = true;
                NotifyVehicle(v, "ВНИМАНИЕ: Пробит топливный бак!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            // 2. Повреждение трансмиссии (остановка)
            if (!s.IsTransmissionBroken && UnityEngine.Random.value < (cfg.ChanceTransmission + damageMultiplier))
            {
                s.IsTransmissionBroken = true;
                NotifyVehicle(v, "КРИТ: Трансмиссия выведена из строя!", Color.red);
                v.speed = 0; // Мгновенное замедление
            }

            // 3. Возгорание
            if (!s.IsOnFire && UnityEngine.Random.value < (cfg.ChanceFire + damageMultiplier))
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        // --- Подсистемы модулей ---

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 5);
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            EffectManager.sendEffect(125, 128, v.transform.position); // Эффект огня
            
            for (int i = 0; i < 7; i++)
            {
                if (v == null || v.isExploded) break;
                // Наносим постепенный урон от огня
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void NotifyVehicle(InteractableVehicle v, string text, Color color)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, text, color);
        }
    }
}
