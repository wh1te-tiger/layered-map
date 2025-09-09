using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration.Generation;
using WorldGeneration.Generation.Jobs;

#if UNITY_EDITOR
namespace Tests.WorldGeneration
{
    public class MarkNeededJobTests
    {
        // ====== ТЕСТ 1: 1x1 клетка, проверяем флаги для всех масок ======
        [Test]
        public void Flags_AreCorrect_ForAllCases()
        {
            const int cellsX = 1, cellsY = 1;
            const int nodesX = cellsX + 1; // 2
            const int nodesY = cellsY + 1; // 2
            const float iso = 0.5f;

            for (int mask = 0; mask < 16; mask++)
            {
                // ── Входные данные на 1 клетку ──────────────────────────────────────
                var cells = new NativeArray<GridCell>(1, Allocator.TempJob);
                cells[0] = TestCellFactory.MakeCellForMask(0, 0, mask, iso);

                // Выходные флаги (ровно те размеры, что ожидает MarkNeededJob)
                var cornerFlags = new NativeArray<byte>(nodesX * nodesY, Allocator.TempJob); // 4
                var midHTopFlags = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob); // 2: [Bottom, Top]
                var midVTopFlags = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob); // 2: [Left, Right]
                var midHWallFlags = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob); // 2
                var midVWallFlags = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob); // 2
                // trisPerCell обязателен по сигнатуре, но в этом тесте мы его не проверяем
                var trisPerCell = new NativeArray<int>(cellsX * cellsY, Allocator.TempJob);

                var job = new MarkNeededJob
                {
                    nodesX = nodesX, nodesY = nodesY,
                    isoThreshold = iso,
                    cells = cells,

                    cornerFlags = cornerFlags,
                    edgeHTopFlags = midHTopFlags,
                    edgeVTopFlags = midVTopFlags,

                    hSideFlags = midHWallFlags,
                    vSideFlags = midVWallFlags,

                    trisPerCell = trisPerCell
                };

                var h = job.Schedule(cellsX * cellsY, 1);
                h.Complete();

                // ── Проверка corner-флагов ──────────────────────────────────────────
                int cm = MarchingSquaresLookUpTables.CornerMask[mask];
                // порядок индексов: (gx,gy): (0,0)->0, (1,0)->1, (0,1)->2, (1,1)->3
                Assert.AreEqual(((cm & 1) != 0) ? 1 : 0, cornerFlags[0], $"mask {mask}: corner v00");
                Assert.AreEqual(((cm & 2) != 0) ? 1 : 0, cornerFlags[1], $"mask {mask}: corner v10");
                Assert.AreEqual(((cm & 4) != 0) ? 1 : 0, cornerFlags[2], $"mask {mask}: corner v01");
                Assert.AreEqual(((cm & 8) != 0) ? 1 : 0, cornerFlags[3], $"mask {mask}: corner v11");

                // ── Проверка edge-флагов (Top/Wall совпадают по расположению) ───────
                int em = MarchingSquaresLookUpTables.EdgeMask[mask];
                // Horizontal (H): длина 2 → [0]=Bottom(y=0,x=0), [1]=Top(y=1,x=0)
                byte expH_Bottom = (byte)(((em & 8) != 0) ? 1 : 0);
                byte expH_Top = (byte)(((em & 4) != 0) ? 1 : 0);
                Assert.AreEqual(expH_Bottom, midHTopFlags[0], $"mask {mask}: H Bottom (topFlags)");
                Assert.AreEqual(expH_Top, midHTopFlags[1], $"mask {mask}: H Top (topFlags)");
                Assert.AreEqual(expH_Bottom, midHWallFlags[0], $"mask {mask}: H Bottom (wallFlags)");
                Assert.AreEqual(expH_Top, midHWallFlags[1], $"mask {mask}: H Top (wallFlags)");

                // Vertical (V): длина 2 → [0]=Left(x=0,y=0), [1]=Right(x=1,y=0)
                byte expV_Left = (byte)(((em & 1) != 0) ? 1 : 0);
                byte expV_Right = (byte)(((em & 2) != 0) ? 1 : 0);
                Assert.AreEqual(expV_Left, midVTopFlags[0], $"mask {mask}: V Left (topFlags)");
                Assert.AreEqual(expV_Right, midVTopFlags[1], $"mask {mask}: V Right (topFlags)");
                Assert.AreEqual(expV_Left, midVWallFlags[0], $"mask {mask}: V Left (wallFlags)");
                Assert.AreEqual(expV_Right, midVWallFlags[1], $"mask {mask}: V Right (wallFlags)");

                // ── Очистка ─────────────────────────────────────────────────────────
                cells.Dispose();
                cornerFlags.Dispose();
                midHTopFlags.Dispose();
                midVTopFlags.Dispose();
                midHWallFlags.Dispose();
                midVWallFlags.Dispose();
                trisPerCell.Dispose();
            }
        }

        // ====== ТЕСТ 2: Граничные клетки не пишут за пределы массивов ======
        [Test]
        public void Edges_DoNotWriteOutOfBounds()
        {
            const int cellsX = 3, cellsY = 2;
            const int nodesX = cellsX + 1, nodesY = cellsY + 1;
            const float iso = 0.5f;

            var cells = new NativeArray<GridCell>(cellsX * cellsY, Allocator.TempJob);
            // Синтетические значения, чтобы были разные маски и на границах тоже:
            int k = 0;
            for (int y = 0; y < cellsY; y++)
            for (int x = 0; x < cellsX; x++, k++)
            {
                float a = (x + y) % 2 == 0 ? 1f : 0f;
                float b = (x % 2) == 0 ? 1f : 0f;
                float c = (y % 2) == 0 ? 1f : 0f;
                float d = ((x + y) % 3) == 0 ? 1f : 0f;
                cells[k] = TestCellFactory.Cell(x, y, a, b, c, d);
            }

            var corner = new NativeArray<byte>(nodesY * nodesX, Allocator.TempJob);
            var mhTop = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var mvTop = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);
            var mhWall = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var mvWall = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);
            var tris = new NativeArray<int>(cellsX * cellsY, Allocator.TempJob);

            var job = new MarkNeededJob
            {
                nodesX = nodesX, nodesY = nodesY,
                isoThreshold = iso, cells = cells,
                cornerFlags = corner, edgeHTopFlags = mhTop, edgeVTopFlags = mvTop,
                hSideFlags = mhWall, vSideFlags = mvWall,
                trisPerCell = tris
            };

            var h = job.Schedule(cellsX * cellsY, 64);
            h.Complete();

            // Просто убеждаемся, что всё в пределах и есть хоть какие-то флаги/треугольники
            int sumCorners = 0, sumMH = 0, sumMV = 0, sumTris = 0;
            for (int i = 0; i < corner.Length; i++) sumCorners += corner[i];
            for (int i = 0; i < mhTop.Length; i++) sumMH += mhTop[i];
            for (int i = 0; i < mvTop.Length; i++) sumMV += mvTop[i];
            for (int i = 0; i < tris.Length; i++) sumTris += tris[i];

            Assert.GreaterOrEqual(sumCorners, 0);
            Assert.Greater(sumMH + sumMV, 0, "Edge flags are all zero — looks suspicious");
            Assert.Greater(sumTris, 0, "No triangles counted — looks suspicious");

            cells.Dispose();
            corner.Dispose();
            mhTop.Dispose();
            mvTop.Dispose();
            mhWall.Dispose();
            mvWall.Dispose();
            tris.Dispose();
        }

        // ====== ТЕСТ 3: Небольшая сетка — сверяем сумму треугольников с теоретической ======
        [Test]
        public void SmallGrid_TotalTriangles_MatchesSumOfLUTs()
        {
            const int cellsX = 8, cellsY = 5;
            const int nodesX = cellsX + 1, nodesY = cellsY + 1;
            const float iso = 0.5f;

            var cells = new NativeArray<GridCell>(cellsX * cellsY, Allocator.TempJob);
            // Заполняем шахматно, чтобы гарантировать разное поведение
            int idx = 0;
            for (int y = 0; y < cellsY; y++)
            for (int x = 0; x < cellsX; x++, idx++)
            {
                float v00 = ((x + y) & 1) == 0 ? 1f : 0f;
                float v10 = (x & 1) == 0 ? 1f : 0f;
                float v01 = (y & 1) == 0 ? 1f : 0f;
                float v11 = ((x * y) & 1) == 0 ? 1f : 0f;
                cells[idx] = TestCellFactory.Cell(x, y, v00, v10, v01, v11);
            }

            var corner = new NativeArray<byte>(nodesY * nodesX, Allocator.TempJob);
            var mhTop = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var mvTop = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);
            var mhWall = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var mvWall = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);
            var tris = new NativeArray<int>(cellsX * cellsY, Allocator.TempJob);

            var job = new MarkNeededJob
            {
                nodesX = nodesX, nodesY = nodesY,
                isoThreshold = iso, cells = cells,
                cornerFlags = corner, edgeHTopFlags = mhTop, edgeVTopFlags = mvTop,
                hSideFlags = mhWall, vSideFlags = mvWall,
                trisPerCell = tris
            };

            var h = job.Schedule(cellsX * cellsY, 128);
            h.Complete();

            // Суммируем теоретически и фактически
            int sumExpected = 0;
            idx = 0;
            for (int y = 0; y < cellsY; y++)
            for (int x = 0; x < cellsX; x++, idx++)
            {
                var c = cells[idx];
                int m = GridCell.GetCellType(c.v00, c.v10, c.v01, c.v11, iso);
                int topTris = MarchingSquaresLookUpTables.TriangleCount[m];
                int wallTris = Popcount4(MarchingSquaresLookUpTables.EdgeMask[m]);
                sumExpected += topTris + wallTris;
            }

            int sumActual = 0;
            for (int i = 0; i < tris.Length; i++) sumActual += tris[i];

            Assert.AreEqual(sumExpected, sumActual, "Total triangle count mismatch");

            cells.Dispose();
            corner.Dispose();
            mhTop.Dispose();
            mvTop.Dispose();
            mhWall.Dispose();
            mvWall.Dispose();
            tris.Dispose();
        }

        static int Popcount4(int m) => ((m >> 0) & 1) + ((m >> 1) & 1) + ((m >> 2) & 1) + ((m >> 3) & 1);
    }

    struct TestCellFactory
    {
        internal static GridCell Cell(int x, int y, float v00, float v10, float v01, float v11)
            => new GridCell
            {
                coordinates = new int2(x, y),
                v00 = v00, v10 = v10, v01 = v01, v11 = v11
            };

        internal static GridCell MakeCellForMask(int x, int y, int mask, float iso = 0.5f)
        {
            // Битовое соответствие: v00=bit0, v10=bit1, v01=bit2, v11=bit3
            float v00 = ((mask & 1) != 0) ? 1f : 0f;
            float v10 = ((mask & 2) != 0) ? 1f : 0f;
            float v01 = ((mask & 4) != 0) ? 1f : 0f;
            float v11 = ((mask & 8) != 0) ? 1f : 0f;
            return new GridCell
            {
                coordinates = new int2(x, y),
                v00 = v00, v10 = v10, v01 = v01, v11 = v11
            };
        }
    }
}
#endif