using System;
using System.Collections.Generic;
using System.Reflection;
using SDG.Unturned;
using UnityEngine;
using VehicleModulesSystem;
using Steamworks;

public class VehicleTracker : MonoBehaviour
{
    private InteractableVehicle vehicle;
    private ushort lastHealth;
    public Dictionary<TankModule, float> ModuleHP = new Dictionary<TankModule, float>();
    private float tickTimer = 0f;
    private float effectTimer = 0f;

    void Start()
    {
        vehicle = GetComponent<InteractableVehicle>();
        if (vehicle == null) return;
        lastHealth = vehicle.health;

        // Инициализация модулей
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

        if (vehicle.health < lastHealth)
        {
            ApplyImpact((ushort)(lastHealth - vehicle.health));
            lastHealth = vehicle.health;
        }
        else if (vehicle.health > lastHealth) lastHealth = vehicle.health;

        if (Time.time - effectTimer > 0.6f)
        {
            effectTimer = Time.time;
            #pragma warning disable CS0618
            if (ModuleHP[TankModule.FuelLeak] < 50f)
                EffectManager.sendEffect(134, 32f, transform.position + Vector3.up);
            if (ModuleHP[TankModule.Engine] < 30f)
                EffectManager.sendEffect(45, 32f, transform.position + Vector3.up * 1.5f);
            #pragma warning restore CS0618
        }

        if (Time.time - tickTimer > 1f)
        {
            tickTimer = Time.time;
            if (ModuleHP[TankModule.FuelLeak] < 40f)
            {
                float fireDmg = VehicleModulesPlugin.Instance.Configuration.Instance.FireDamage;
                // Наносим урон напрямую, чтобы не возиться с askDamage
                if (vehicle.health > fireDmg) vehicle.health -= (ushort)fireDmg;
                else vehicle.health = 0;

                // ВЫЗОВ СИНХРОНИЗАЦИИ ЧЕРЕЗ ОБХОД (Reflection)
                SmartInvoke(vehicle, "tellHealth", CSteamID.Nil, vehicle.health);

                if (ModuleHP[TankModule.Fire] > 0)
                {
                    ModuleHP[TankModule.FuelLeak] += 2f;
                    ModuleHP[TankModule.Fire] -= 1f; 
                }
            }
            if (ModuleHP[TankModule.Engine] <= 0 && vehicle.isEngineOn) vehicle.askFillFuel(0);
        }
    }

    // Тот самый "Умный вызов", который клал на количество аргументов
    private void SmartInvoke(object target, string methodName, params object[] args)
    {
        try {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return;

            ParameterInfo[] parameters = method.GetParameters();
            object[] finalArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++) {
                if (i < args.Length) finalArgs[i] = args[i];
                else finalArgs[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
            }
            method.Invoke(target, finalArgs);
        } catch { /* Игнорируем ошибки вызова в рантайме */ }
    }

    public void ApplyImpact(ushort damage)
    {
        if (damage < VehicleModulesPlugin.Instance.Configuration.Instance.MinDamageThreshold) return;

        var values = Enum.GetValues(typeof(TankModule));
        TankModule hit = (TankModule)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        ModuleHP[hit] = Mathf.Max(0, ModuleHP[hit] - (damage * 1.3f));
        
        NotifyCrew($"<color=red>[СИСТЕМА]</color> Поврежден модуль: {hit}!");
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
        if (vehicle.passengers == null) return;
        foreach (var p in vehicle.passengers)
            if (p?.player != null)
                ChatManager.serverSendMessage(msg, Color.white, null, p.player.player.channel.owner, EChatMode.SAY, null, true);
    }

    public string GetModuleStatus(TankModule mod)
    {
        float hp = ModuleHP[mod];
        if (hp >= 90) return VehicleModulesPlugin.Instance.Translate("Status_Perfect");
        if (hp > 40) return VehicleModulesPlugin.Instance.Translate("Status_Damaged");
        if (hp > 0) return VehicleModulesPlugin.Instance.Translate("Status_Critical");
        return VehicleModulesPlugin.Instance.Translate("Status_Destroyed");
    }
}
