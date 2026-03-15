using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        // Атрибут XmlArrayItem делает конфиг читаемым: <VehicleID>120</VehicleID>
        [XmlArrayItem(ElementName = "VehicleID")]
        public List<ushort> AllowedVehicleIds;
        
        // Шансы критических повреждений (0.0 - 1.0)
        public float ChanceFuelLeak;
        public float ChanceTransmission;
        public float ChanceGunBroken;
        public float ChanceFire;
        public float ChanceSmoke;
        public float ChanceStun;

        // Метод LoadDefaults вызывается RocketMod при первом запуске
        public void LoadDefaults()
        {
            // Стартовый набор техники для твоего проекта
            AllowedVehicleIds = new List<ushort> { 120, 121, 137 };
            
            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceGunBroken = 0.12f;
            ChanceFire = 0.05f;
            ChanceSmoke = 0.20f;
            ChanceStun = 0.10f;
        }
    }
}
