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
            
            // Запускаем наблюдатель с задержкой в 2 секунды, чтобы избежать NullReferenceException при загрузке конфига
            StartCoroutine(StartObserverWithDelay());
            
            Rocket.Core.Logging.Logger.Log("!!! VEHICLE MODULES DEBUG VERSION LOADED !!!");
        }

        private IEnumerator StartObserverWithDelay()
        {
            yield return new WaitForSeconds(2.0f);
            StartCoroutine(VehicleAdvancedObserver());
            Rocket.Core.Logging.Logger.Log("[Debug] Наблюдатель успешно запущен через 2 секунды после старта.");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private IEnumerator VehicleAdvancedObserver()
        {
            while (true)
            {
                // Проверяем существование конфига, чтобы избежать NullRef
                if (Configuration == null || Configuration.Instance == null || Configuration.Instance.TargetedVehicleIds == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // Работаем с копией списка, чтобы не вызвать ошибку при спавне/удалении машин
                var currentVehicles = VehicleManager.vehicles.ToList();

                foreach (var vehicle in currentVehicles)
                {
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;

                    // Фильтр по ID
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    // ВЕШАЕМ ДАТЧИК (Регистрация)
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        Rocket.Core.Logging.Logger.Log($"[Debug] Датчик установлен на: {vehicle.asset.vehicleName} ID:{vehicle.instanceID}");
                        continue;
                    }

                    // --- DEBUG LOGIC: ШАНС ПОЛОМКИ КАЖДЫЕ 5 СЕКУНД ---
                    // 30% шанс, что сработает поломка модуля принудительно
                    if (UnityEngine.Random.value < 0.30f)
                    {
                        Rocket.Core.Logging.Logger.Log($"[Debug] Тестовая попытка поломки модуля для {vehicle.instanceID}");
                        ExecuteDamageLogic(vehicle, state, 15);
                    }

                    // Обычное отслеживание урона
                    if (vehicle.health < state.LastHealth)
                    {
                        ExecuteDamageLogic(vehicle, state, state.LastHealth - vehicle.health);
                    }

                    // Принудительный стоп при сломанной коробке
                    if (state.IsTransmissionBroken) StopPhysical(vehicle);

                    state.LastHealth = vehicle.health;
                }

                // Сканирование и дебаг-урон каждые 5 секунд по ТЗ
                yield return new WaitForSeconds(5.0f);
            }
        }

        private void ExecuteDamageLogic(InteractableVehicle v, VehicleState s, int damage)
        {
            // В Debug версии шансы ОЧЕНЬ высокие (0.5 бонус), чтобы сразу увидеть результат
            float chanceBonus = 0.5f;

            if (!s.IsFuelTankBroken && UnityEngine.Random.value < (0.1f + chanceBonus))
            {
                s.IsFuelTankBroken = true;
                Notify(v, "DEBUG: БАК ПРОБИТ!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && UnityEngine.Random.value < (0.1f + chanceBonus))
            {
                s.IsTransmissionBroken = true;
                Notify(v, "DEBUG: ТРАНСМИССИЯ ВЫБИТА!", Color.red);
            }

            if (!s.IsOnFire && UnityEngine.Random.value < (0.05f + chanceBonus))
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        private void StopPhysical(InteractableVehicle v)
        {
            var rb = v.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 10);
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            s.IsSmoking = true;
            Notify(v, "DEBUG: ПОЖАР В МТО!", Color.red);

            while (s.IsOnFire && v != null && !v.isExploded)
            {
                TriggerEffect(125, v.transform.position);
                VehicleManager.damage(v, 100, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void TriggerEffect(ushort id, Vector3 pos)
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
