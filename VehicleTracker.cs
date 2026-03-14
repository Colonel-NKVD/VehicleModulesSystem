using System;
using System.Collections.Generic;
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

        // Детекция урона
        if (vehicle.health < lastHealth)
        {
            ushort damageTaken = (ushort)(lastHealth - vehicle.health);
            ApplyImpact(damageTaken);
            lastHealth = vehicle.health;
        }
        else if (vehicle.health > lastHealth)
        {
            lastHealth = vehicle.health;
        }

        // Визуальные эффекты (Исправлено: TriggerEffectParameters)
        if (Time.time - effectTimer > 0.5f)
        {
            effectTimer = Time.time;
            SpawnVisualEffects();
        }

        // Системная логика
        if (Time.time - tickTimer > 1f)
        {
            tickTimer = Time.time;
            ProcessModuleLogic();
        }
    }

    private void SpawnVisualEffects()
    {
        // Используем современный способ вызова эффектов через параметры
        if (ModuleHP[TankModule.FuelLeak] < 50f)
        {
            TriggerEffectParameters parameters = new TriggerEffectParameters(AssetReference<EffectAsset>.invalid);
            parameters.relevantPosition = transform.position + Vector3.up;
            parameters.relevantDistance = 128f;
            // ID эффекта огня (134 - стандартный взрыв/огонь)
            EffectAsset asset = Assets.find(EAssetType.EFFECT, 134) as EffectAsset;
            if (asset != null)
            {
                parameters.asset = asset;
                EffectManager.triggerEffect(parameters);
            }
        }

        if (ModuleHP[TankModule.Engine] < 30f)
        {
            TriggerEffectParameters parameters = new TriggerEffectParameters(AssetReference<EffectAsset>.invalid);
            parameters.relevantPosition = transform.position + Vector3.up * 1.5f;
            parameters.relevantDistance = 128f;
            // ID эффекта дыма (45)
            EffectAsset asset = Assets.find(EAssetType.EFFECT, 45) as EffectAsset;
            if (asset != null)
            {
                parameters.asset = asset;
                EffectManager.triggerEffect(parameters);
            }
        }
    }

    private void ProcessModuleLogic()
    {
        if (ModuleHP[TankModule.FuelLeak] < 40f)
        {
            float fireDamage = VehicleModulesPlugin.Instance.Configuration.Instance.FireDamage;
            
            // ИСПРАВЛЕНИЕ ОШИБКИ CS1501: Используем 3 аргумента (Урон, Можно ли чинить, Кто нанес)
            vehicle.askDamage((ushort)fireDamage, false, CSteamID.Nil);
            
            if (ModuleHP[TankModule.Fire] > 0)
            {
                ModuleHP[TankModule.FuelLeak] += 2f;
                ModuleHP[TankModule.Fire] -= 1f; 
            }
        }

        if (ModuleHP[TankModule.Engine] <= 0)
        {
            if (vehicle.isEngineOn) vehicle.askFillFuel(0); 
        }
    }

    public void ApplyImpact(ushort damage)
    {
        if (damage < VehicleModulesPlugin.Instance.Configuration.Instance.MinDamageThreshold) return;

        // Эффект тряски / попадания
        TriggerEffectParameters parameters = new TriggerEffectParameters(AssetReference<EffectAsset>.invalid);
        parameters.relevantPosition = transform.position;
        parameters.relevantDistance = 64f;
        EffectAsset asset = Assets.find(EAssetType.EFFECT, 45) as EffectAsset;
        if (asset != null)
        {
            parameters.asset = asset;
            EffectManager.triggerEffect(parameters);
        }

        Array values = Enum.GetValues(typeof(TankModule));
        TankModule hit = (TankModule)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        
        ModuleHP[hit] = Mathf.Max(0, ModuleHP[hit] - (damage * 1.2f));
        
        NotifyCrew($"<color=red>[СИСТЕМА]</color> Критическое повреждение узла: {hit}!");
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
