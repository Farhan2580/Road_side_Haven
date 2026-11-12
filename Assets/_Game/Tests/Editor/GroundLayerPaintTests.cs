using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class GroundLayerPaintTests
    {
        [Test] public void LayerWeightsRemainNormalizedAndNorthGetsGravel()
        {
            var weights=GroundLayerPaint.Bake(new Rect(-100,-1000,200,2000),128,new[]{new Rect(-100,-14,200,12)},new[]{new Rect(20,30,16,12)},new Rect[0]);
            for(int z=0;z<128;z++)for(int x=0;x<128;x++){float total=0;for(int i=0;i<4;i++){Assert.GreaterOrEqual(weights[z,x,i],0);total+=weights[z,x,i];}Assert.AreEqual(1,total,.0001f);}
            Assert.Greater(weights[125,64,3],.7f);Assert.Greater(weights[20,40,0],.9f);
        }
        [Test] public void RoadsAndBuildingEdgesGetDifferentBlends()
        {
            var weights=GroundLayerPaint.Bake(new Rect(-50,-50,100,100),101,new[]{new Rect(-50,-5,100,10)},new[]{new Rect(20,20,10,10)},new Rect[0]);
            Assert.Greater(weights[57,50,1],.3f);Assert.Greater(weights[76,76,2],.5f);Assert.Greater(weights[10,10,0],.99f);
        }
    }
}
