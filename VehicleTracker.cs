using System;
using System.Collections.Generic;
using System.Reflection;
using SDG.Unturned;
using UnityEngine;
using VehicleModulesSystem;

public class VehicleTracker : MonoBehaviour
{
    private InteractableVehicle vehicle;
    private ushort lastHealth;
    public Dictionary<TankModule, float> ModuleHP = new Dictionary<TankModule, float>();
    private float tickTimer = 0f;
    private static FieldInfo _engineField = typeof(InteractableVehicle).GetField("_isEngineOn", BindingFlags.NonPublic | BindingFlags.Instance);

    void Start()
    {
        vehicle = GetComponent<InteractableVehicle>();
        if (vehicle == null) return;
        lastHealth = vehicle.health;

        if (VehicleModulesPlugin.Instance.SavedVehicleData.TryGetValue(vehicle.instanceID, out var saved))
        {
            foreach (var entry in saved) 
                if (Enum.TryParse(entry.Key, out TankModule m)) ModuleHP[m] = entry.Value;
        }
        else
        {
            foreach (TankModule m in Enum.GetValues(typeof(TankModule))) ModuleHP[m] = 100f;
        }
    }

    void Update()
    {
        if (vehicle == null || vehicle.isDead) return;

        // Детекция внешнего урона
        if (vehicle.health < lastHealth)
        {
            ApplyImpact((ushort)(lastHealth - vehicle.health));
            lastHealth = vehicle.health;
        }

        // Системные тики раз в секунду
        if (Time.time - tickTimer > 1f)
        {
            tickTimer = Time.time;
            HandleInterdependency();
        }
    }

    private void HandleInterdependency()
    {
        var conf = VehicleModulesPlugin.Instance.Configuration.Instance;

        // 1. Цепная реакция: Утечка топлива -> Пожар
        if (ModuleHP[TankModule.FuelLeak] < 50 && ModuleHP[TankModule.Fire] > 0)
        {
            if (UnityEngine.Random.value < conf.ChainReactionChance)
            {
                ModuleHP[TankModule.Fire] = 0; // Резкое возгорание
                NotifyCrew(VehicleModulesPlugin.Instance.Translate("Alert_ChainReaction"));
                MarkDirty();
            }
        }

        // 2. Влияние на двигатель
        if (ModuleHP[TankModule.Transmission] <= 0 && vehicle.isEngineOn)
            _engineField?.SetValue(vehicle, false);

        // 3. Урон от пожара корпусу
        if (ModuleHP[TankModule.Fire] <= 0)
            VehicleManager.damage(vehicle, conf.FireDamage, 1, false, Steamworks.CSteamID.Nil, EDamageOrigin.Sentry);
    }

    private void ApplyImpact(ushort damage)
    {
        if (damage < VehicleModulesPlugin.Instance.Configuration.Instance.MinDamageThreshold) return;

        // Тряска камеры для экипажа (замена UI эффекта попадания)
        if (VehicleModulesPlugin.Instance.Configuration.Instance.EnableCameraShake)
            EffectManager.sendEffect(45, 16f, transform.position); 

        TankModule hit = (TankModule)UnityEngine.Random.Range(0, 4);
        ModuleHP[hit] = Mathf.Max(0, ModuleHP[hit] - (damage * 1.5f));
        MarkDirty();
    }

    private void MarkDirty()
    {
        var data = new Dictionary<string, float>();
        foreach (var entry in ModuleHP) data.Add(entry.Key.ToString(), entry.Value);
        VehicleModulesPlugin.Instance.SavedVehicleData[vehicle.instanceID] = data;
        VehicleModulesPlugin.Instance.IsDirty = true;
    }

    private void NotifyCrew(string msg)
    {
        foreach (var p in vehicle.passengers)
            if (p?.player != null)
                ChatManager.serverSendMessage(msg, Color.red, null, p.player.player.channel.owner, EChatMode.SAY, null, true);
    }

    public string GetModuleStatus(TankModule mod)
    {
        float hp = ModuleHP[mod];
        if (hp >= 100) return VehicleModulesPlugin.Instance.Translate("Status_Perfect");
        if (hp > 40) return VehicleModulesPlugin.Instance.Translate("Status_Damaged");
        if (hp > 0) return VehicleModulesPlugin.Instance.Translate("Status_Critical");
        return VehicleModulesPlugin.Instance.Translate("Status_Destroyed");
    }
}
