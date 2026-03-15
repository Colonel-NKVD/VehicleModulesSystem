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
        public ushort LastHealth;
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
            
            // Логируем начало загрузки
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Инициализация плагина...");

            // Запускаем через безопасную корутину-загрузчик
            StartCoroutine(SafeStartSequence());
        }

        private IEnumerator SafeStartSequence()
        {
            // Ждем 3 секунды, чтобы Unturned и RocketMod прогрузили все ассеты и конфиги
            yield return new WaitForSeconds(3.0f);

            if (Configuration.Instance == null)
            {
                Rocket.Core.Logging.Logger.LogError("КРИТИЧЕСКАЯ ОШИБКА: Конфигурация не найдена!");
                yield break;
            }

            Rocket.Core.Logging.Logger.Log($"Загружено ID техники для отслеживания: {Configuration.Instance.TargetedVehicleIds.Count}");
            
            // Запускаем основной цикл сканирования (каждые 5 секунд по ТЗ)
            StartCoroutine(VehicleAdvancedObserver());
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Наблюдатель успешно запущен.");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Плагин выгружен.");
        }

        private IEnumerator VehicleAdvancedObserver()
        {
            while (true)
            {
                // Проверка на наличие машин в мире
                if (VehicleManager.vehicles == null)
                {
                    Rocket.Core.Logging.Logger.LogWarning("VehicleManager.vehicles еще не инициализирован.");
                    yield return new WaitForSeconds(5.0f);
                    continue;
                }

                Rocket.Core.Logging.Logger.Log($"[Debug] Сканирование техники... (Всего в мире: {VehicleManager.vehicles.Count})");

                // Работаем с копией списка для безопасности
                var vehiclesInWorld = VehicleManager.vehicles.ToList();

                foreach (var vehicle in vehiclesInWorld)
                {
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;

                    // Проверка: входит ли машина в список целей
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    // УСТАНОВКА ДАТЧИКА
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        Rocket.Core.Logging.Logger.Log($"[ДАТЧИК] Установлен на {vehicle.asset.vehicleName} (ID: {vehicle.instanceID})");
                        continue;
                    }

                    // --- DEBUG: ГАРАНТИРОВАННАЯ СЛУЧАЙНАЯ ПОЛОМКА (раз в 5 сек) ---
                    if (UnityEngine.Random.value < 0.25f) // 25% шанс поломки при каждом сканировании
                    {
                        Rocket.Core.Logging.Logger.Log($"[Debug] Искусственный износ для {vehicle.instanceID}");
                        ExecuteDamageLogic(vehicle, state, 10);
                    }

                    // Проверка реального урона
                    if (vehicle.health < state.LastHealth)
                    {
                        int dmg = state.LastHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[УРОН] Машина {vehicle.instanceID} получила {dmg} урона.");
                        ExecuteDamageLogic(vehicle, state, dmg);
                    }

                    // Принудительный стоп при поломке трансмиссии
                    if (state.IsTransmissionBroken)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }

                    state.LastHealth = vehicle.health;
                }

                yield return new WaitForSeconds(5.0f);
            }
        }

        private void ExecuteDamageLogic(InteractableVehicle v, VehicleState s, int damage)
        {
            // Шансы в дебаг-версии завышены (0.4 бонус)
            float bonus = 0.4f;

            if (!s.IsFuelTankBroken && UnityEngine.Random.value < (0.1f + bonus))
            {
                s.IsFuelTankBroken = true;
                Rocket.Core.Logging.Logger.Log($"[СОБЫТИЕ] У машины {v.instanceID} пробит бак!");
                Notify(v, "ВНИМАНИЕ: Пробит топливный бак!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && UnityEngine.Random.value < (0.1f + bonus))
            {
                s.IsTransmissionBroken = true;
                Rocket.Core.Logging.Logger.Log($"[СОБЫТИЕ] У машины {v.instanceID} выбита трансмиссия!");
                Notify(v, "КРИТ: Трансмиссия уничтожена!", Color.red);
            }

            if (!s.IsOnFire && UnityEngine.Random.value < (0.05f + bonus))
            {
                Rocket.Core.Logging.Logger.Log($"[СОБЫТИЕ] Машина {v.instanceID} загорелась!");
                StartCoroutine(FireRoutine(v, s));
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 10);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            s.IsSmoking = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                TriggerModernEffect(125, v.transform.position);
                VehicleManager.damage(v, 120, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void TriggerModernEffect(ushort id, Vector3 pos)
        {
            if (Assets.find(EAssetType.EFFECT, id) is EffectAsset asset)
            {
                TriggerEffectParameters p = new TriggerEffectParameters(asset);
                p.position = pos;
                p.relevantDistance = 128f;
                EffectManager.triggerEffect(p);
            }
        }

        private void Notify(InteractableVehicle v, string msg, Color c)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, msg, c);
        }
    }
}
