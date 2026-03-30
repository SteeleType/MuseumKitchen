using UnityEngine;

public class PotluckManager : MonoBehaviour
{
    [Header("VFX & Settings / 视觉特效与设置")]
    [Tooltip("The parent transform where the new dish will be spawned.\n收到菜品后，在哪个父级 Transform 下面生成。")]
    public Transform spawnContainer;
    
    [Tooltip("A generic display prefab serving as a visual drop effect container. You can replace this with actual 3D Dumpling models later.\n一个通用展示预制体（用作掉落动画容器），之后可换成真实 3D 模型。")]
    public GameObject dropEffectPrefab; 

    // Called on the Big Screen side when a LAN message is received
    // 大屏端调用：当收到局域网消息时
    public void OnPotluckDataReceived(PotluckData incomingDish)
    {
        Debug.Log($"🎉 A new dish has appeared on the Big Screen! / 大屏幕出现了一道新菜！\n" +
                  $"Source Device / 来源设备: {incomingDish.clientId}\n" +
                  $"Filling / 肉馅: {incomingDish.fillingName}\n" +
                  $"Wrapper / 面皮: {incomingDish.wrappingName}\n" +
                  $"Cooking Method / 煮法: {incomingDish.cookingMethodName}");
        
        // Simple Big Screen Demo: Instantiates a prefab at the script's location
        // 简单的大屏演示：在此脚本所处的世界坐标生成预制体（你可以增加更多的动画逻辑）
        if (dropEffectPrefab != null)
        {
            // Spawn at a random position dropping down from above
            // 随便找个顶部位置往下掉
            Vector3 randomSpawnPos = new Vector3(Random.Range(-5f, 5f), 10f, 0f);
            Instantiate(dropEffectPrefab, randomSpawnPos, Quaternion.identity, spawnContainer);
        }
    }
}
