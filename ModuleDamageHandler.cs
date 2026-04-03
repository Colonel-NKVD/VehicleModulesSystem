using System.Collections.Generic;
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
            if (s.IsOnFire) return;

            float intensity = Mathf.Clamp(dmg / 1500f, 0f, 0.25f); 

            if (!s.IsStunned && Random.value < (cfg.ChanceStun + intensity))
            {
                Rocket.Core.Logging.Logger.Log($"[CRIT] Экипаж {v.id} контужен.");
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }

            int criticalsThisHit = 0;
            int maxCriticals = dmg > 600 ? 2 : 1; 

            List<System.Action> moduleChecks = new List<System.Action>
            {
                () => {
                    if (!s.IsFuelTankBroken && Random.value < (cfg.ChanceFuelLeak + intensity)) {
                        s.IsFuelTankBroken = true;
                        SendChat(v, "!!! КРИТ: Пробит топливный бак !!!", Color.red);
                        Rocket.Core.Logging.Logger.Log($"[CRIT] Бак пробит на {v.id}.");
                        VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (!s.IsTransmissionBroken && Random.value < (cfg.ChanceTransmission + intensity)) {
                        s.IsTransmissionBroken = true;
                        SendChat(v, "!!! КРИТ: Трансмиссия выбита !!!", Color.red);
                        Rocket.Core.Logging.Logger.Log($"[CRIT] Трансмиссия уничтожена на {v.id}.");
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (s.IsGunBroken) {
                        if (Random.value < 0.25f) ExplodeBreach(v);
                    } else if (Random.value < (cfg.ChanceGunBroken + intensity)) {
                        s.IsGunBroken = true;
                        SendChat(v, "!!! КРИТ: Орудие заклинило !!!", Color.red);
                        Rocket.Core.Logging.Logger.Log($"[CRIT] Орудие выбито на {v.id}.");
                        criticalsThisHit++;
                    }
                }
            };

            for (int i = 0; i < moduleChecks.Count; i++) {
                int randomIndex = Random.Range(i, moduleChecks.Count);
                var temp = moduleChecks[i];
                moduleChecks[i] = moduleChecks[randomIndex];
                moduleChecks[randomIndex] = temp;
            }

            foreach (var check in moduleChecks) {
                if (criticalsThisHit >= maxCriticals) break;
                check.Invoke();
            }

            if (!s.IsOnFire && Random.value < (cfg.ChanceFire + (intensity * 0.5f)))
            {
                SendChat(v, "!!! ПОЖАР В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
                Rocket.Core.Logging.Logger.Log($"[CRIT] ПОЖАР на {v.id}.");
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }
            else if (!s.IsSmoking && Random.value < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                Rocket.Core.Logging.Logger.Log($"[CRIT] Задымление на {v.id}.");
                s.IsSmoking = true;
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            int duration = 15; 
            int elapsed = 0;

            while (s.IsSmoking && v != null && !v.isExploded && elapsed < duration)
            {
                foreach (var p in v.passengers)
                {
                    if (p.player != null)
                        p.player.player.life.askDamage(1, Vector3.up, EDeathCause.BREATH, ELimb.SPINE, CSteamID.Nil, out _);
                }
                yield return new WaitForSeconds(1.0f);
                elapsed++;
            }
            
            if (s != null)
            {
                s.IsSmoking = false;
                if (v != null && !v.isExploded) SendChat(v, "[СИСТЕМА] Боевое отделение проветрено.", Color.green);
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 randomOffset = v.transform.right * Random.Range(-1.5f, 1.5f) + v.transform.forward * Random.Range(-3.5f, 3.5f) + Vector3.up * Random.Range(1.8f, 3.0f);              
                    EffectManager.sendEffect(139, 128, v.transform.position + randomOffset);
                }
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
                if (p.player != null) p.player.player.life.askDamage(80, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out _);
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

        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
