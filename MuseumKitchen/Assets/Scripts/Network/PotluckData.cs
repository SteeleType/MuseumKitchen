using System;

[Serializable]
public class PotluckData
{
    // The identifier of the sender device / 发送者设备的标识符
    public string clientId;           
    
    // Name of the filling ingredient / 馅料名称
    public string fillingName;        
    
    // Name of the wrapper ingredient / 面皮名称
    public string wrappingName;       
    
    // Name of the cooking method / 烹饪方式名称
    public string cookingMethodName;  

    // Future expansion: dish name, model name, creation time, etc. 
    // 后期扩展：菜品名称、模型名称、制作时间等
}
