using Unity.Burst;
using Unity.Mathematics;

namespace WorldGeneration.Generation
{
    /// <summary>
    /// Структура для хранения данных о ячейке сетки
    /// </summary>
    public struct GridCell
    {
        public float v00, v10, v01, v11;
        public int2 coordinates;
        
        public GridCell(float v00, float v10, float v01, float v11, int2 coords)
        {
            this.v00 = v00; this.v10 = v10;
            this.v01 = v01; this.v11 = v11;
            
            coordinates = coords;
        }
        
        [BurstCompile]
        public static int GetCellType(float v00, float v10, float v01, float v11, float threshold)
        {
          return (v00 >= threshold ? 1 : 0) | (v10 >= threshold ? 2 : 0) | (v01 >= threshold ? 4 : 0) | (v11 >= threshold ? 8 : 0) ;
        }
    }
}