using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        [XmlArrayItem(ElementName = "VehicleID")]
        public List<ushort> AllowedVehicleIds = new List<ushort>();

        // Настройки ремстанции и медицины
        public ushort RepairStationId;
        public ushort BandageItemId;
        public float BandageUseTimeSeconds;
        public byte BandageHealAmount;

        // Настройки урона и брони (НОВЫЙ ФУНКЦИОНАЛ)
        public int MinDamageForCrit;
        public float ChanceDeflect; 
        public ushort SmokeVisualEffectId;

        // Шансы критических повреждений (0.0 - 1.0)
        public float ChanceFuelLeak;
        public float ChanceTransmission;
        public float ChanceGunBroken;
        public float ChanceFire;
        public float ChanceSmoke;
        public float ChanceStun;

        public void LoadDefaults()
        {
            AllowedVehicleIds = new List<ushort> { 120, 121, 137 };
            
            RepairStationId = 287; 
            BandageItemId = 393; 
            BandageUseTimeSeconds = 4.0f; 
            BandageHealAmount = 20; 
            
            MinDamageForCrit = 15; 
            ChanceDeflect = 0.35f; 
            SmokeVisualEffectId = 110; 

            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceGunBroken = 0.12f;
            ChanceFire = 0.05f;
            ChanceSmoke = 0.20f;
            ChanceStun = 0.10f;
        }
    }
}
