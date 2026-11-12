using System.Collections.Generic;
namespace RoadsideHaven
{
    /// <summary>Deterministic downtown plot plan; road reservations stay separate from building footprints.</summary>
    public static class ModernCityLayout
    {
        public readonly struct TowerPlot
        {
            public readonly float X,Z,Height,Width,Depth;
            public readonly int Style;
            public readonly bool Landmark;
            public TowerPlot(float x,float z,int style,bool landmark)
            {X=x;Z=z;Style=style;Landmark=landmark;Height=landmark?215:60+style*18;Width=style%2==0?24:20;Depth=style%3==0?28:22;}
        }
        public static float[] StreetX=>new[]{-950f,-810f,-670f,-530f,-390f};
        public static float[] StreetZ=>new[]{20f,160f,300f,460f,600f};
        public static IEnumerable<TowerPlot> Towers()
        {
            float[] rowZ={90,230,380,530};float[] offsetX={-27,25,0},offsetZ={-24,-10,30};
            for(int row=0;row<4;row++)for(int column=0;column<4;column++)
            {
                if(column==0&&row<2||column==1&&row==2||column==2&&row==1||column==3&&row==1)continue;
                for(int plot=0;plot<3;plot++)yield return new TowerPlot(-880+column*140+offsetX[plot],rowZ[row]+offsetZ[plot],(row*5+column*3+plot)%8,row==3&&column==1&&plot==0);
            }
        }
    }
}
