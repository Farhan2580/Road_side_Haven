using System;
namespace RoadsideHaven
{
    /// <summary>Physical world solids versus nonblocking decals, foliage and visual effects.</summary>
    public static class WorldCollisionRules
    {
        public static bool IsSolidName(string objectName)
        {
            if(string.IsNullOrWhiteSpace(objectName))return false;
            string name=objectName.ToLowerInvariant();
            if(name=="plant"||name=="signboard"||name=="top frame"||name=="bottom frame"||name=="sign support"||name=="back lettering")return false;
            foreach(string visual in new[]{"dash","stripe","marking","marker","rug","runner","duvet","pillow","goods","window","pane","mullion","water","spray","stream","portal","tree crown","crystal","headlight","lamp shade"," bulb"," crop","crop row","helipad h"})
                if(name.Contains(visual))return false;
            return true;
        }
    }
}
