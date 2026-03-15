using Rocket.API;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        public List<ushort> TargetedVehicleIds; // Список ID танков/броневиков
        public float ChanceFuelLeak;            // Шанс пробития бака (0.0 - 1.0)
        public float ChanceTransmission;        // Шанс поломки коробки
        public float ChanceFire;                // Шанс пожара
        
        public void Defaults()
        {
            TargetedVehicleIds = new List<ushort> { 120, 121, 137 }; // Примеры ID
            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceFire = 0.05f;
        }
    }
}
