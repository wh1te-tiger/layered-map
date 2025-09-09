using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs; 
using UnityEngine;

namespace WorldGeneration.Generation
{
    public class Test : MonoBehaviour
    {
        const int Size = 1000000;
            
        public void Start()
        {
            var t1 = DateTime.Now;
            var inArray = new NativeArray<byte>(Size, Allocator.TempJob);
            var job1 = new CreateDataJob
            {
                intArray = inArray
            };
            var job1Handle = job1.Schedule(Size, 32);
            
            var t2 = DateTime.Now;
            var outArray = new NativeList<byte>(Size, Allocator.TempJob);
            var job2 = new ProcessDataJob
            {
                intArray = inArray,
                outArray = outArray.AsParallelWriter(),
            };
            var jobHandle2 = job2.Schedule(Size, 32, job1Handle);


            var array = outArray.AsDeferredJobArray();
            var counter = new NativeReference<int>(Allocator.TempJob);
            var job3 = new SumJob
            {
                inArray = array,
                counter = counter,
            };
            var jobHandle3 =  job3.Schedule(jobHandle2);
            
            var newArray = new NativeArray<int>(Size/2, Allocator.TempJob);
            var job4 = new FinalJob
            {
                inArray = inArray,
                outArray = newArray,
                count = counter.Value,
            };
            job4.Schedule(counter.Value, 32, jobHandle3).Complete();
            
            inArray.Dispose();
            outArray.Dispose();
            newArray.Dispose();
            counter.Dispose();
        }
    }
    
    [BurstCompile]
    public struct CreateDataJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<byte> intArray;
        
        public void Execute(int index)
        {
            intArray[index] = (byte)(index % 2);
        }
    }
    
    [BurstCompile]
    public struct ProcessDataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> intArray;
        
        [WriteOnly] public NativeList<byte>.ParallelWriter outArray;
        
        public void Execute(int index)
        {
            if (intArray[index] != 0)
            {
                outArray.AddNoResize((byte)index);
            }
        }
    }

    public struct SumJob : IJob
    {
        [ReadOnly] public NativeArray<byte> inArray;

        [WriteOnly] public NativeReference<int> counter;
        
        public void Execute()
        {
            var count = 0;
            for (int i = 0; i < inArray.Length; i++)
            {
                count++;
            }
            
            counter.Value = count;
        }
    }

    public struct FinalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> inArray;
        [WriteOnly] public NativeArray<int> outArray;
        public int count;
        
        public void Execute(int index)
        {
            outArray[index] = inArray[index] * count;
        }
    }
}