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
using HarmonyLib; // ТРЕБУЕТСЯ 0Harmony.dll

namespace VehicleModulesSystem
{
    public class VehicleState
    {
        public bool IsFuelTankBroken;
        public bool IsOnFire;
        public bool IsSmoking;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();
        private Harmony _harmony;

        protected override void Load()
        {
            Instance = this;

            // ГРЯЗНЫЙ ХАК: Вместо подписки на событие с битой структурой, мы патчим метод напрямую
            _harmony = new Harmony("com.vehiclemodules.patch");
            _harmony.PatchAll();

            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: [HARMONY BYPASS] Система внедрена в движок. Игнорируем DamageVehicleParameters.");
        }

        protected override void Unload()
        {
            _harmony.UnpatchAll("com.vehiclemodules.patch");
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // Этот метод будет вызываться каждый раз, когда КТО-ТО или ЧТО-ТО наносит урон машине
        public void InternalOnVehicleDamage(InteractableVehicle vehicle, ushort damage)
        {
            if (vehicle == null || vehicle.asset == null) return;
            if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            // Шанс возгорания при любом уроне
            if (UnityEngine.Random.value < Configuration.Instance.ChanceFire && !state.IsOnFire)
            {
                StartCoroutine(FireRoutine(vehicle, state));
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            SpawnEffect(125, v.transform.position);
            for (int i = 0; i < 5; i++)
            {
                if (v == null || v.isExploded) break;
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1.2f);
            }
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
    }

    // ПАТЧ: Врезаемся в основной метод урона техники Unturned
    [HarmonyPatch(typeof(VehicleManager), "damage")]
    public static class VehicleDamagePatch
    {
        [HarmonyPrefix]
        public static void Prefix(InteractableVehicle vehicle, ushort damage, float times, bool canBeRepairing)
        {
            // Вызываем логику нашего плагина, обходя все структуры DamageVehicleParameters
            if (VehicleModulesPlugin.Instance != null && !canBeRepairing)
            {
                VehicleModulesPlugin.Instance.InternalOnVehicleDamage(vehicle, (ushort)(damage * times));
            }
        }
    }
}
