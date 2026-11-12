using System;
using System.Linq;
using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public sealed class ModernCityLayoutTests
    {
        [Test] public void TowersFitReservedLotsWithoutBlockingBoulevards()
        {
            var towers=ModernCityLayout.Towers().ToArray();Assert.AreEqual(33,towers.Length);Assert.AreEqual(1,towers.Count(x=>x.Landmark));
            foreach(var tower in towers)
            {
                float halfX=(tower.Width+8)*.5f,halfZ=tower.Depth*.5f+8.5f;
                foreach(float x in ModernCityLayout.StreetX)Assert.GreaterOrEqual(Math.Abs(tower.X-x),halfX+9);
                foreach(float z in ModernCityLayout.StreetZ)Assert.GreaterOrEqual(Math.Abs(tower.Z-z),halfZ+9);
                Assert.That(tower.X-halfX,Is.GreaterThan(-1000));Assert.That(tower.X+halfX,Is.LessThan(1000));
                Assert.That(tower.Z-halfZ,Is.GreaterThan(-1000));Assert.That(tower.Z+halfZ,Is.LessThan(1000));
            }
            for(int i=0;i<towers.Length;i++)for(int j=i+1;j<towers.Length;j++)
            {
                var a=towers[i];var b=towers[j];
                Assert.IsFalse(Math.Abs(a.X-b.X)<(a.Width+b.Width+16)*.5f && Math.Abs(a.Z-b.Z)<(a.Depth+b.Depth+16)*.5f,"Office podiums overlap.");
            }
        }
    }
}
