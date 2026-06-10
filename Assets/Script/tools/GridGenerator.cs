using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
[Header("网格设置")]
    public GameObject cubePrefab; // 拖入你的方块预制体
    public int rows = 3;          // 行数 (例如Z轴方向)
    public int columns = 4;       // 列数 (例如X轴方向)
    public float spacing = 1.5f;  // 方块之间的间距
    private GameObject[,] gridArray;

    void Start()
    {
        gridArray = new GameObject[columns, rows];
        GenerateGrid();
    }

    void GenerateGrid()
    {
        // 检查是否赋值了预制体
        if (cubePrefab == null)
        {
            Debug.LogError("请在 Inspector 面板中分配 Cube Prefab！");
            return;
        }

        // (列数 - 1) * 间距 = 网格总宽度。除以 2 就是需要向左移动的偏移距离
        float offsetX = (columns - 1) * spacing / 2f; 
        float offsetZ = (rows - 1) * spacing / 2f;

        // 嵌套循环生成网格
        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                float posX = (x * spacing) - offsetX;
                float posZ = (z * spacing) - offsetZ;
                // 计算每个方块的位置 (以生成器本身的位置为基准)
                Vector3 spawnPosition = new Vector3(posX, 0, posZ) + transform.position;

                // 实例化预制体
                GameObject newCube = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);

                // (可选) 将生成的方块设为当前物体的子物体，保持层级面板整洁
                newCube.transform.parent = this.transform;
                
                // (可选) 给生成的方块命名，方便区分
                newCube.name = $"Cube_{x}_{z}";
                gridArray[x, z] = newCube;
            }
        }
    }

    // 依然保留了改变颜色的方法，供你后续操作使用
    public void ChangeCubeColor(int x, int z, Color newColor)
    {
        if (x >= 0 && x < columns && z >= 0 && z < rows)
        {
            GameObject targetCube = gridArray[x, z];
            Renderer cubeRenderer = targetCube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                cubeRenderer.material.color = newColor; 
            }
        }
    }
}
