# MuseumKitchen (AMNH Interactive Experience)

## 项目背景 (Project Context)
这个项目是为美国自然历史博物馆 (American Museum of Natural History, AMNH) 定制的交互式体验装置。

## 场景体验 (Experience Flow)
体验分为两个联动的环节：
1. **小屏幕交互 (Visitor Terminal)**
   - 参观者在各自的独立屏幕上进行互动，背景设定为**丝绸之路 (Silk Road)**。
   - 玩家通过选择在横跨亚欧大陆历史长河中出现的各种食材 (Ingredients)、香料 (Spices) 以及 烹饪方法 (Cooking Methods) 进行组合。
   - 所有元素结合后，将完成并“制作”出一道特定的菜肴或食物（例如各种饺子、包子类面点 —— Dumplings 等）。

2. **大屏幕展示 (Potluck Display)**
   - 这是一个所有玩家共同参与的虚拟“百乐宴” (Potluck)。
   - 几位玩家在小屏幕上制作完成的食物，最终会汇集并同步展示在这个中央大屏幕上。
   - 强调文化交流与分享的概念，展现丝绸之路上食材流通与碰撞产生的美丽化学反应。

## 技术架构决定 (Technical Architecture)
**当前选定方案：纯本地局域网方案 (LAN tablets + PC Provider)**
- **客户端 (Visitor Tablets)**: 部署为多台提供触控输入的平板电脑或触摸屏。运行 Unity 客户端程序，主要负责呈现 UI (选择配料)、用户输入，并发送煮好的食材数据。
- **服务端/展示端 (Central Big Screen PC)**: 一台本地高性能 PC 运行同一个 Unity 项目的另一场景（或不同模式），作为独立接收平台，负责接收所有平板的数据并统一渲染华丽的动画（The Potluck）。
- **通信协议选型 (待定)**: 鉴于展览通常需要极强的稳定性与极小的包体延迟，推荐无需连接外网的方案（如 UDP/TCP Socket 广播或使用轻量级的网络框架如 NGO/Mirror）。

## 其他开发注意事项 (Tech Notes)
