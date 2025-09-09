using UnityEngine;

namespace WorldGeneration.Generation
{
    public class MarchingSquaresVisualizer : MonoBehaviour
    {
        public float cellSize = 1f;
        public Vector2 gridSize = new Vector2(4, 4);
        public float vertexSize = 0.15f;
        public float edgeWidth = 0.05f;
        public Color[] vertexColors = new Color[4]
        {
            Color.red,    // v00 (bottom-left)
            Color.green,  // v10 (bottom-right)
            Color.blue,   // v01 (top-left)
            Color.yellow  // v11 (top-right)
        };
        public Color edgeColor = Color.cyan;
        public Color activeEdgeColor = Color.magenta;
        
        // Lookup tables from your algorithm
        private readonly int[] TriangleCountLUT = { 0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 2 };
        private readonly int[] CornerMaskLUT = {
            0b0000, 0b0001, 0b0010, 0b0011,
            0b0100, 0b0101, 0b0110, 0b0111,
            0b1000, 0b1001, 0b1010, 0b1011,
            0b1100, 0b1101, 0b1110, 0b1111
        };
        private readonly int[] EdgeMaskLUT = {
            0b0000, 0b1001, 0b1010, 0b0011,
            0b0101, 0b1100, 0b0110, 0b0110,
            0b0110, 0b0101, 0b1100, 0b1001,
            0b0011, 0b1010, 0b1001, 0b0000
        };
        
        private void OnDrawGizmos()
    {
        Vector3 startPos = transform.position;
        
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                int mask = y * (int)gridSize.x + x;
                if (mask > 15) continue;

                Vector3 cellCenter = startPos + new Vector3(
                    x * cellSize * 1.5f,
                    0,
                    y * cellSize * 1.5f
                );

                DrawCell(cellCenter, mask);
            }
        }
    }

    private void DrawCell(Vector3 center, int mask)
    {
        // Calculate corner positions
        Vector3 v00 = center + new Vector3(-cellSize/2, 0, -cellSize/2);
        Vector3 v10 = center + new Vector3(cellSize/2, 0, -cellSize/2);
        Vector3 v01 = center + new Vector3(-cellSize/2, 0, cellSize/2);
        Vector3 v11 = center + new Vector3(cellSize/2, 0, cellSize/2);

        // Draw cell boundary
        Gizmos.color = Color.white;
        Gizmos.DrawLine(v00, v10);
        Gizmos.DrawLine(v10, v11);
        Gizmos.DrawLine(v11, v01);
        Gizmos.DrawLine(v01, v00);

        // Draw mask info
        #if UNITY_EDITOR
        string info = $"M: {mask}\nT: {TriangleCountLUT[mask]}";
        UnityEditor.Handles.Label(center + Vector3.up * 0.1f, info, 
            new GUIStyle { normal = { textColor = Color.white }, fontSize = 16 });
        #endif

        // Draw active corners (from CornerMaskLUT)
        int cornerMask = CornerMaskLUT[mask];
        if ((cornerMask & 1) != 0) DrawVertex(v00, vertexColors[0]); // v00
        if ((cornerMask & 2) != 0) DrawVertex(v10, vertexColors[1]); // v10
        if ((cornerMask & 4) != 0) DrawVertex(v01, vertexColors[2]); // v01
        if ((cornerMask & 8) != 0) DrawVertex(v11, vertexColors[3]); // v11

        // Draw active edges (from EdgeMaskLUT)
        int edgeMask = EdgeMaskLUT[mask];
        if ((edgeMask & 1) != 0) DrawThickEdge(v00, v01, activeEdgeColor); // Left edge
        else DrawThickEdge(v00, v01, edgeColor);
        
        if ((edgeMask & 2) != 0) DrawThickEdge(v10, v11, activeEdgeColor); // Right edge
        else DrawThickEdge(v10, v11, edgeColor);
        
        if ((edgeMask & 4) != 0) DrawThickEdge(v01, v11, activeEdgeColor); // Top edge
        else DrawThickEdge(v01, v11, edgeColor);
        
        if ((edgeMask & 8) != 0) DrawThickEdge(v00, v10, activeEdgeColor); // Bottom edge
        else DrawThickEdge(v00, v10, edgeColor);
    }

    private void DrawVertex(Vector3 position, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(position, vertexSize);
    }

    private void DrawThickEdge(Vector3 from, Vector3 to, Color color)
    {
        Gizmos.color = color;
        
        // Draw multiple lines to simulate thickness
        Vector3 dir = (to - from).normalized;
        Vector3 perpendicular = Vector3.Cross(dir, Vector3.up).normalized * edgeWidth;
        
        Gizmos.DrawLine(from - perpendicular, to - perpendicular);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawLine(from + perpendicular, to + perpendicular);
    }
    }
}