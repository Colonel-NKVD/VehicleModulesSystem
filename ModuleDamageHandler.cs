using System.Collections.Generic;
using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            if (s.IsOnFire) return;

            // Контузия (Stun)
            if (!s.IsStunned && Random.value < cfg.ChanceStun)
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }

            // Шанс крита увеличивается от силы урона
            float damageMultiplier = Mathf.Clamp(dmg / 500f, 1f, 2f);

            if (!s.IsFuelTankBroken && Random.value < (cfg.ChanceFuelLeak * damageMultiplier))
            {
                s.IsFuelTankBroken = true;
                SendChat(v, "!!! КРИТ: Пробит топливный бак !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && Random.value < (cfg.ChanceTransmission * damageMultiplier))
            {
                s.IsTransmissionBroken = true;
                v.batteryCharge = 0;
                VehicleManager.sendVehicleFuel(v, v.fuel);
                SendChat(v, "!!! КРИТ: Повреждение трансмиссии !!!", Color.red);
            }

            if (!s.IsSmoking && Random.value < cfg.ChanceSmoke)
            {
                s.IsSmoking = true;
                SendChat(v, "[СИСТЕМА] Двигатель поврежден, наблюдается задымление.", Color.gray);
            }
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            SendChat(v, ">> ЭКИПАЖ КОНТУЖЕН <<", Color.red);
            yield return new WaitForSeconds(4.0f);
            s.IsStunned = false;
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 20);
                VehicleManager.sendVehicleFuel(v, v.fuel);
                yield return new WaitForSeconds(2.0f);
            }
        }

        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
            {
                if (p.player != null)
                {
                    UnturnedChat.Say(p.player.playerID.steamID, msg, c);
                }
            }
        }
    }
}
