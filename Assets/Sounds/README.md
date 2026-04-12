# 声音资源文件说明

## 📁 文件存放位置

所有声音文件必须放在以下目录：
```
Nexus/Assets/Sounds/
```

## 🎵 需要的声音文件清单

### 1. 火灾警报声 ✅
**文件名**: `fire_alarm.mp3`
**用途**: 火灾警报通知时循环播放
**特征**: 
- 急促、高音调的警报声
- 建议时长：3-5秒
- 具有明显的火灾警报特征
- **会循环播放直到用户确认**

### 2. 防空警报声 ✅
**文件名**: `air_raid_alert.mp3`
**用途**: 防空警报通知时循环播放
**特征**:
- 上升和下降音调的警报声
- 建议时长：3-5秒
- 具有明显的防空警报特征
- **会循环播放直到用户确认**

### 3. 地震预警声 ✅
**文件名**: `earthquake_warning.mp3`
**用途**: 地震预警通知时循环播放
**特征**:
- 低沉、持续的警报声
- 建议时长：3-5秒
- 具有明显的地震预警特征
- **会循环播放直到用户确认**

### ❌ 不需要的声音文件
以下通知类型**不需要**声音文件：
- **紧急通知** (emergency.mp3) - 不播放声音
- **警告提示** (alert.mp3) - 不播放声音
- **横幅通知** (notification.mp3) - 不播放声音

## 📋 声音文件格式要求

- **格式**: MP3
- **采样率**: 44100 Hz
- **比特率**: 128 kbps 或更高
- **声道**: 立体声或单声道
- **文件大小**: 建议每个文件不超过 500KB

## 🔊 播放规则

| 通知类型 | 声音文件 | 是否循环 | 音量 | 说明 |
|---------|---------|---------|------|------|
| 🔥 火灾警报 | fire_alarm.mp3 | ✅ 循环 | 100% | 必须提供 |
| ⚠️ 防空警报 | air_raid_alert.mp3 | ✅ 循环 | 100% | 必须提供 |
| 🌍 地震预警 | earthquake_warning.mp3 | ✅ 循环 | 100% | 必须提供 |
| 🚨 紧急通知 | - | - | - | 不播放声音 |
| ⚡ 弹窗警告 | - | - | - | 不播放声音 |
| 📢 横幅通知 | - | - | - | 不播放声音 |

## 🌐 声音文件来源建议

### 免费音效库
1. **Freesound** - https://freesound.org/
   - 搜索关键词：fire alarm, air raid siren, earthquake alert
   - 需要注册账号，大部分免费

2. **Zapsplat** - https://www.zapsplat.com/
   - 搜索关键词：alarm, siren, warning
   - 需要注册账号，有免费额度

3. **BBC Sound Effects** - https://sound-effects.bbcrewind.co.uk/
   - BBC官方音效库
   - 可免费用于个人和教育用途

4. **Mixkit** - https://mixkit.co/free-sound-effects/
   - 完全免费，无需注册
   - 搜索：alarm, siren, alert

### 推荐搜索关键词
- 火灾警报：`fire alarm`, `fire siren`, `fire evacuation`
- 防空警报：`air raid siren`, `civil defense siren`, `war siren`
- 地震预警：`earthquake alarm`, `seismic alert`, `earthquake warning`

## 📝 使用步骤

1. **下载声音文件**
   - 从上述网站下载合适的声音文件
   - 确保文件格式为 MP3

2. **重命名文件**
   - 按照上述文件名规范重命名
   - 例如：`fire_alarm.mp3`, `air_raid_alert.mp3`

3. **放置文件**
   - 将文件复制到 `Nexus/Assets/Sounds/` 目录
   - 如果目录不存在，请手动创建

4. **测试播放**
   - 运行 Nexus 客户端
   - 发送不同类型的灾害预警通知进行测试
   - 确认声音能正常播放并循环

## ⚠️ 注意事项

1. **版权问题**
   - 确保使用的声音文件具有合法授权
   - 商业用途请购买商业授权

2. **音量控制**
   - 灾害预警声音会循环播放直到用户确认
   - 确保声音不会过于刺耳
   - 建议在设置中提供音量控制选项

3. **文件大小**
   - 控制文件大小以提高加载速度
   - 建议使用音频编辑软件压缩文件

4. **测试建议**
   - 在不同设备上测试声音效果
   - 测试循环播放是否流畅
   - 测试音量是否合适

## 🛠️ 音频编辑工具推荐

- **Audacity** (免费开源) - https://www.audacityteam.org/
- **Online Audio Converter** - https://online-audio-converter.com/
- **MP3Cut** - https://mp3cut.net/

## 📞 技术支持

如果在声音播放过程中遇到问题，请检查：
1. 文件是否存在于正确目录
2. 文件格式是否为 MP3
3. 文件是否损坏
4. 系统音量是否开启
5. 查看调试日志中的错误信息

## 📌 总结

只需要准备 **3个声音文件**：
1. `fire_alarm.mp3` - 火灾警报声
2. `air_raid_alert.mp3` - 防空警报声
3. `earthquake_warning.mp3` - 地震预警声

其他通知类型（紧急通知、警告提示、横幅通知）不会播放声音。
