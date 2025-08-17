using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGeneration.Generation
{
    public class MapGeneratorSync : IMapGenerator
    {
        private readonly MapConfiguration _config;
        private readonly Transform _root;

        private Vector3[] _vertices;
        private int[] _triangles;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private readonly Vector3[] _cellVertices = new Vector3[4];

        private int _vertexCount;
        private int _triangleCount;
        private float _layerHeightStep;
        
        private readonly Dictionary<Vector3Int, int> _vertexIndexMapInt = new(); // Хранит уникальные вершины и их индексы


        public MapGeneratorSync(MapConfiguration config, Transform root)
        {
            _config = config;
            _root = root;
        }
        
        public void GenerateMap()
        {
            float[,] map = _config.Heightmap.GetHeightMapMatrix();
            int mapWidth = _config.MapWidth;
            int mapHeight = _config.MapHeight;
            int heightLevels = _config.Heightmap.HeightLevels;
            _layerHeightStep = 1f / heightLevels;

            // Предварительное выделение памяти
            int maxVertices = mapWidth * mapHeight * 4; // Максимум 4 вершины на ячейку
            int maxTriangles = mapWidth * mapHeight * 6; // Максимум 6 индексов на ячейку (2 треугольника)

            _vertices = new Vector3[maxVertices];
            _triangles = new int[maxTriangles];
            _uvs = new Vector2[maxVertices];
            _normals =  new Vector3[maxVertices];

            for (int layer = 0; layer < heightLevels; layer++)
            {
                GenerateLayerMesh(map, mapWidth, mapHeight, layer, _config.LayerHeight);
            }
        }
        
        private void GenerateLayerMesh(float[,] map, int width, int height, int layer, float layerHeight)
        {
            GameObject layerObject = new GameObject($"Layer_{layer}");
            layerObject.transform.parent = _root;

            MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();

            // Устанавливаем базовый материал
            meshRenderer.sharedMaterial = _config.LayerMaterial;
            meshFilter.mesh = GenerateLayerMeshData(map, width, height, layer, layerHeight);
        }

        private Mesh GenerateLayerMeshData(float[,] map, int width, int height, int layer, float layerHeight)
        {
            ClearMeshData();
            Mesh mesh = new Mesh();

            // Мировые высоты для слоя
            float baseHeight = layer * layerHeight; // Нижняя граница слоя
            float topHeight = (layer + 1) * layerHeight; // Верхняя граница слоя

            // Нормализованный порог для текущего слоя
            float normalizedThreshold = layer * _layerHeightStep;

            // Проходим по всей сетке
            for (int z = 0; z < height - 1; z++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    // Получаем значения для текущей ячейки
                    float v00 = map[x, z];
                    float v10 = map[x + 1, z];
                    float v01 = map[x, z + 1];
                    float v11 = map[x + 1, z + 1];

                    // Определяем тип ячейки (0–15)
                    int cellType = 0;
                    if (v00 >= normalizedThreshold) cellType |= 1;
                    if (v10 >= normalizedThreshold) cellType |= 2;
                    if (v01 >= normalizedThreshold) cellType |= 4;
                    if (v11 >= normalizedThreshold) cellType |= 8;

                    if (cellType == 0) continue;
 
                    // Добавляем вершины и треугольники верхней поверхности на основе типа ячейки
                    AddMarchingSquare(cellType, x, z, topHeight, baseHeight, normalizedThreshold);
                }
            }
            
            mesh.SetVertices(_vertices, 0, _vertexCount);
            mesh.triangles = _triangles.Take(_triangleCount).ToArray();
            // Вычисляем нормали вручную
            //_normals = CalculateNormals(_vertices, mesh.triangles);
            //mesh.SetNormals(_normals, 0, _vertexCount); // передаём нормали
            mesh.RecalculateNormals();
            mesh.SetUVs(0, _uvs, 0, _vertexCount); // Передаём UV-координаты
            mesh.RecalculateBounds();

            return mesh;
        }

        private void ClearMeshData()
        {
            // Сбрасываем счётчики
            _vertexCount = 0;
            _triangleCount = 0;
            _vertexIndexMapInt.Clear();
        }

        private void AddMarchingSquare(int cellType, int x, int z, float topHeight, float baseHeight, float normalizedThreshold)
        {
            var heightMap = _config.Heightmap.GetHeightMapMatrix();
            var cellSize = _config.CellSize;

            // Вершины клетки
            _cellVertices[0] = new Vector3(x * cellSize, topHeight, z * cellSize);
            _cellVertices[1] = new Vector3((x + 1) * cellSize, topHeight, z * cellSize);
            _cellVertices[2] = new Vector3(x * cellSize, topHeight, (z + 1) * cellSize);
            _cellVertices[3] = new Vector3((x + 1) * cellSize, topHeight, (z + 1) * cellSize);

            // Фактические высоты вершин
            float v00Height = heightMap[x, z];
            float v10Height = heightMap[x + 1, z];
            float v01Height = heightMap[x, z + 1];
            float v11Height = heightMap[x + 1, z + 1];

            // Координаты серединных вершин на рёбрах
            var edgeVertices = new EdgeVertices(
                _cellVertices[0], _cellVertices[1], _cellVertices[2], _cellVertices[3],
                v00Height, v10Height, v01Height, v11Height,
                normalizedThreshold
            );

            // Обработка всех 16 случаев
            switch (cellType)
            {
                case 0: // Пустая ячейка
                    break;

                case 1: // Только v00 внутри
                    AddTriangle(GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold));
                    AddSideFace(edgeVertices.Bottom, edgeVertices.Left, baseHeight, normalizedThreshold);
                    break;

                case 2: // Только v10 внутри
                    AddTriangle(GetOrAddVertex(_cellVertices[1], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold));
                    AddSideFace(edgeVertices.Right, edgeVertices.Bottom, baseHeight, normalizedThreshold);
                    break;

                case 3: // v00 и v10 внутри
                    AddQuad(
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold)
                    );
                    AddSideFace(edgeVertices.Right, edgeVertices.Left, baseHeight, normalizedThreshold);
                    break;

                case 4: // Только v01 внутри
                    AddTriangle(GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold));
                    AddSideFace(edgeVertices.Left, edgeVertices.Top, baseHeight, normalizedThreshold);
                    break;

                case 5: // v00 и v01 внутри
                    AddQuad(
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold)
                    );
                    AddSideFace(edgeVertices.Bottom, edgeVertices.Top, baseHeight, normalizedThreshold);
                    break;

                case 6:
                    AddTriangle(GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold));
                    AddTriangle(GetOrAddVertex(_cellVertices[1], normalizedThreshold), GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold));
                    break;

                case 7: // v00, v10 и v01 внутри
                    AddPentagon(
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold)
                    );
                    AddSideFace( edgeVertices.Right, edgeVertices.Top, baseHeight, normalizedThreshold);
                    break;

                case 8: // Только v11 внутри
                    AddTriangle(GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold));
                    AddSideFace( edgeVertices.Top, edgeVertices.Right, baseHeight, normalizedThreshold);
                    break;

                case 9: // v00 и v11 внутри
                    AddTriangle(GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold));
                    AddTriangle(GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold));
                    break;

                case 10: // v10 и v11 внутри
                    AddQuad(
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold)
                    );
                    AddSideFace(edgeVertices.Top, edgeVertices.Bottom, baseHeight, normalizedThreshold);
                    break;

                case 11: // v00, v10 и v11 внутри
                    AddPentagon(
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold)
                    );
                    AddSideFace( edgeVertices.Top, edgeVertices.Left, baseHeight, normalizedThreshold);
                    break;

                case 12: // v01 и v11 внутри
                    AddQuad(
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold)
                    );
                    AddSideFace(edgeVertices.Left, edgeVertices.Right, baseHeight, normalizedThreshold);
                    break;

                case 13: // v00, v01 и v11 внутри
                    AddPentagon(
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold)
                    );
                    AddSideFace( edgeVertices.Bottom, edgeVertices.Right, baseHeight, normalizedThreshold);
                    break;

                case 14: // v10, v01 и v11 внутри
                    AddPentagon(
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold)
                    );
                    AddSideFace( edgeVertices.Left, edgeVertices.Bottom, baseHeight, normalizedThreshold);
                    break;

                case 15: // Все вершины внутри
                    AddQuad(
                        GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(_cellVertices[1], normalizedThreshold)
                    );
                    break;
            }
        }
        
        // Добавление боковой грани
        private void AddSideFace(Vector3 topEdgeStart, Vector3 topEdgeEnd, float bottomHeight, float normalizedLayer)
        {
            // Нижние вершины боковой грани
            Vector3 bottomEdgeStart = new Vector3(topEdgeStart.x, bottomHeight, topEdgeStart.z);
            Vector3 bottomEdgeEnd = new Vector3(topEdgeEnd.x, bottomHeight, topEdgeEnd.z);

            // Добавляем боковую грань
            AddQuad(
                GetOrAddVertex(bottomEdgeStart, normalizedLayer),
                GetOrAddVertex(topEdgeStart, normalizedLayer),
                GetOrAddVertex(topEdgeEnd, normalizedLayer),
                GetOrAddVertex(bottomEdgeEnd, normalizedLayer)
            );
        }

        #region AddPoligons
        
        private void AddTriangle(int v0, int v1, int v2)
        {
            _triangles[_triangleCount++] = v0;
            _triangles[_triangleCount++] = v1;
            _triangles[_triangleCount++] = v2;
        }

        private void AddQuad(int v0, int v1, int v2, int v3)
        {
            AddTriangle(v0, v1, v2);
            AddTriangle(v0, v2, v3);
        }

        private void AddPentagon(int v0, int v1, int v2, int v3, int v4)
        {
            AddTriangle(v0, v1, v2);
            AddTriangle(v0, v2, v3);
            AddTriangle(v0, v3, v4);
        }
        
        #endregion
        
        private int GetOrAddVertex(Vector3 vertex, float normalizedLayer)
        {
            var scaleFactor = 100f;
            var vScaled =  new Vector3Int(Mathf.CeilToInt(vertex.x * scaleFactor),Mathf.CeilToInt(vertex.y * scaleFactor), Mathf.CeilToInt(vertex.z * scaleFactor));
            if (_vertexIndexMapInt.TryGetValue(vScaled, out var index))
            {
                return index;
            }
            
            index = _vertexCount++;
            _vertices[index] = vertex;
            _uvs[index] = new Vector2(normalizedLayer, 0);
            _vertexIndexMapInt[vScaled] = index;
            
            return index;
        }
        
        /*private Vector3[] CalculateNormals(Vector3[] vertices, int[] triangles)
        {
            Vector3[] normals = new Vector3[vertices.Length];
            int triangleCount = triangles.Length / 3;

            // Рассчитываем нормали для каждой грани
            for (int i = 0; i < triangleCount; i++)
            {
                int index0 = triangles[i * 3];
                int index1 = triangles[i * 3 + 1];
                int index2 = triangles[i * 3 + 2];

                Vector3 v0 = vertices[index0];
                Vector3 v1 = vertices[index1];
                Vector3 v2 = vertices[index2];
                
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                
                normals[index0] += normal;
                normals[index1] += normal;
                normals[index2] += normal;
            }
            
            // Нормализуем нормали для каждой вершины
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normals[i].normalized;
            }
            
            AdjustBoundaryNormals(_vertices, normals);

            return normals;
        }
        
        private void AdjustBoundaryNormals(Vector3[] vertices, Vector3[] normals)
        {
            for (int i = 0; i < _vertexCount; i++)
            {
                Vector3 vertex = vertices[i];
                
                // Ищем соответствующую нижнюю вершину
                Vector3 vertexB = new Vector3(vertex.x, vertex.y - _layerHeightStep, vertex.z);

                // Проверяем, является ли вершина граничной (по X или Z)
                bool isBoundary = _vertexIndexMap.ContainsKey(vertexB);

                if (isBoundary)
                {
                    // Устанавливаем нормаль перпендикулярно верхней поверхности
                    normals[i] = Vector3.up;
                }
            }
        }*/
    }
}