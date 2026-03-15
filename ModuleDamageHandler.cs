using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Steamworks;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            float roll = Random.value;
            float intensity = dmg / 500f;

            if (s.IsOnFire) return;

            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                s.IsFuelTankBroken = true;
                SendChat(v, "!!! КРИТ: Пробит топливный бак !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                SendChat(v, "[СИСТЕМА] Трансмиссия повреждена!", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
            }

            if (s.IsGunBroken)
            {
                if (Random.value < 0.40f) 
                {
                    ExplodeBreach(v);
                    return; 
                }
            }
            else if (roll < (cfg.ChanceGunBroken + intensity))
            {
                s.IsGunBroken = true;
                SendChat(v, "[СИСТЕМА] Орудие заклинило!", Color.red);
            }

            if (!s.IsOnFire && roll < (cfg.ChanceFire + intensity))
            {
                SendChat(v, "!!! ПОЖАР В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }

            if (!s.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }

            if (!s.IsStunned && roll < (cfg.ChanceStun + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
                // Оставляем только визуал задымления
                EffectManager.sendEffect(110, 128, v.transform.position + Vector3.up * 1.5f);
                
                // Вся логика с кислородом убрана, чтобы не тревожить readonly свойства
                yield return new WaitForSeconds(2.0f); 
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(139, 128, v.transform.position + Vector3.up * 2.5f);
                EffectManager.sendEffect(139, 128, v.transform.position + v.transform.forward * 2.5f + Vector3.up * 2.0f);
                EffectManager.sendEffect(139, 128, v.transform.position - v.transform.forward * 2.5f + Vector3.up * 2.0f);

                VehicleManager.damage(v, 130, 1, false);
                yield return new WaitForSeconds(0.8f);
            }
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            EffectManager.sendEffect(45, 128, v.transform.position + Vector3.up * 2f);
            SendChat(v, "!!! РАЗРЫВ КАЗЕННИКА !!!", Color.red);
            
            VehicleManager.damage(v, 1000, 1, false);
            foreach (var p in v.passengers)
            {
                if (p.player != null)
                {
                    p.player.player.life.askDamage(80, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out EPlayerKill k);
                }
            }
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            SendChat(v, ">> ЭКИПАЖ КОНТУЖЕН <<", Color.yellow);
            foreach (var p in v.passengers)
                if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
            yield return new WaitForSeconds(5.0f);
            if (v != null)
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            s.IsStunned = false;
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                EffectManager.sendEffect(16, 128, v.transform.position + Vector3.up);
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 35);
                VehicleManager.sendVehicleFuel(v, v.fuel);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static IEnumerator TransRoutine(InteractableVehicle v, VehicleState s)
        {
            yield return new WaitForSeconds(Random.Range(10, 20));
            if (v != null)
            {
                s.IsTransmissionBroken = true;
                v.batteryCharge = 0;
                VehicleManager.sendVehicleFuel(v, v.fuel); 
                SendChat(v, "!!! КРИТ: Трансмиссия рассыпалась !!!", Color.red);
            }
        }

        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
