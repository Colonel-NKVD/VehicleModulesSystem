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
        public bool IsOnFire;
        public bool IsSmoking;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        // Словарь для хранения состояния: Ключ — уникальный InstanceID машины
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            
            // Запускаем "Наблюдателя", который будет следить за уроном без всяких событий
            StartCoroutine(VehicleObserverLoop());
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: [OBSERVER MODE] Система запущена. Игнорируем API событий.");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // ТОТ САМЫЙ "ГРЯЗНЫЙ" ЦИКЛ
        private IEnumerator VehicleObserverLoop()
        {
            while (true)
            {
                // Проверяем все машины на сервере каждые 0.1 секунды
                // Это неоптимизированно, но зато сработает на любой версии игры
                foreach (var vehicle in VehicleManager.vehicles.ToList())
                {
                    if (vehicle == null || vehicle.asset == null) continue;
                    
                    // Фильтруем технику по вашему списку ID из конфига
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // Если текущее здоровье меньше, чем было 0.1 сек назад — значит получен урон!
                    if (vehicle.health < state.LastHealth)
                    {
                        OnVehicleDamageDetected(vehicle, state);
                    }

                    state.LastHealth = vehicle.health;
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private void OnVehicleDamageDetected(InteractableVehicle v, VehicleState s)
        {
            var config = Configuration.Instance;

            // Шанс пробития бака
            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                NotifyDriver(v, "КРИТИЧЕСКИЙ УРОН: Бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            // Шанс возгорания
            if (UnityEngine.Random.value < config.ChanceFire && !s.IsOnFire)
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 2);
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            SpawnEffect(125, v.transform.position);
            for (int i = 0; i < 5; i++)
            {
                if (v == null || v.isExploded) break;
                // Наносим урон через API, наш Обсерватор его увидит, но LastHealth не даст зациклиться
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void SpawnEffect(ushort id, Vector3 pos)
        {
            if (Assets.find(EAssetType.EFFECT, id) is EffectAsset asset)
            {
                TriggerEffectParameters e = new TriggerEffectParameters(asset);
                e.position = pos;
                e.relevantDistance = 128f;
                EffectManager.triggerEffect(e);
            }
        }

        private void NotifyDriver(InteractableVehicle v, string msg, Color c)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, msg, c);
        }
    }
}
