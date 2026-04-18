using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        [XmlArrayItem(ElementName = "VehicleID")]
        public List<ushort> AllowedVehicleIds;
        
        public float ChanceFuelLeak;
        public float ChanceTransmission;
        public float ChanceGunBroken;
        public float ChanceFire;
        public float ChanceSmoke;
        public float ChanceStun;

        // --- НОВЫЕ ПАРАМЕТРЫ ---
        public int MinDamageForCritical; // Минимальный урон для срабатывания любого крита
        public float RicochetThresholdPercent; // Урон ниже этого % от макс. ХП может отрикошетить
        public float RicochetChance; // Шанс рикошета (0.0 - 1.0)
        public bool RepairFixesTransmission; // Чинит ли станция трансмиссию
        public ushort SmokeUIEffectID; // ID UI эффекта для задымления (например, 31000)
        public ushort BandageItemID; // ID предмета для команды /b (15 - бинт)

        public void LoadDefaults()
        {
            AllowedVehicleIds = new List<ushort> { 120, 121, 137 };
            
            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceGunBroken = 0.12f;
            ChanceFire = 0.05f;
            ChanceSmoke = 0.20f;
            ChanceStun = 0.10f;

            // Дефолтные значения для новых функций
            MinDamageForCritical = 40; 
            RicochetThresholdPercent = 0.05f; // 5% от макс ХП
            RicochetChance = 0.40f; 
            RepairFixesTransmission = true;
            SmokeUIEffectID = 122; // Пример ID (стандартный эффект затуманивания)
            BandageItemID = 15; 
        }
    }
}
