using Rocket.API;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        // Базовый список ID, которые плагин будет игнорировать или обрабатывать
        // В будущем сюда добавим конкретные шансы для разных типов урона
        public List<ushort> TargetedVehicleIds;

        public void LoadDefaults()
        {
            TargetedVehicleIds = new List<ushort> { 120, 121, 137 };
        }
    }
}
