using System;
using Unity.Collections;

namespace WorldGeneration.Generation
{
    public struct OffsetData
    {
        public NativeReference<int> CornerCount;
        public NativeReference<int> HTopCount;
        public NativeReference<int> VTopCount;
        public NativeReference<int> HSideCount;
        public NativeReference<int> VSideCount;
            
        public OffsetData(NativeReference<int> cornerCount,
            NativeReference<int> hTopCount,
            NativeReference<int> vTopCount,
            NativeReference<int> hSideCount,
            NativeReference<int> vSideCount)
        {
            CornerCount = cornerCount;
            HTopCount = hTopCount;
            VTopCount = vTopCount;
            HSideCount = hSideCount;
            VSideCount = vSideCount;
        }
            
        public int GetOffset(OffsetType offsetType)
        {
            switch (offsetType)
            {
                case OffsetType.None:
                    return 0;
                case OffsetType.Corner:
                    return 0;
                case OffsetType.HTop:
                    return CornerCount.Value;
                case OffsetType.VTop:
                    return CornerCount.Value + HTopCount.Value;
                case OffsetType.HSide:
                    return CornerCount.Value + HTopCount.Value +  VTopCount.Value;
                case OffsetType.VSide:
                    return CornerCount.Value + HTopCount.Value +  VTopCount.Value + HSideCount.Value;
                case OffsetType.All:
                    return CornerCount.Value + HTopCount.Value +  VTopCount.Value + HSideCount.Value + VSideCount.Value;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void DisposeTemps()
        {
            CornerCount.Dispose();
            HTopCount.Dispose();
            VTopCount.Dispose();
            HSideCount.Dispose();
            VSideCount.Dispose();
        }
    }
    
    public enum OffsetType
    {
        None,
        Corner,
        HTop,
        VTop,
        HSide,
        VSide,
        All
    }
}