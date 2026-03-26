using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ChillAIMod
{
    /// <summary>
    /// 分层记忆管理系统 - 递归摘要架构
    /// Layer 0: 原始对话 | Layer 1+: 压缩摘要 | 自动递归压缩保证token可控
    /// </summary>
    public class HierarchicalMemory
    {
        // ================= 配置参数 =================
        private readonly List<List<string>> _layers;
        private readonly int _maxItemsLayer0;         // Layer 0 的最大容量
        private readonly int _maxItemsOtherLayers;    // Layer 1+ 的最大容量
        private readonly int _summarizeBatchSize;     // 每次压缩的条目数
        
        // LLM 服务接口（用于调用摘要 API）
        private readonly Func<string, Task<string>> _llmSummarizer;
        
        // 持久化相关
        private readonly string _saveFilePath;
        
        // 并发控制：防止同时触发多次压缩
        private readonly object _processLock = new object();
        private bool _isProcessing = false;
        
        // ================= 构造函数 =================
        public HierarchicalMemory(
            Func<string, Task<string>> llmSummarizer,
            int totalLayers = 3,
            int maxItemsLayer0 = 20,
            int maxItemsOtherLayers = 10,
            int summarizeBatchSize = 5,
            string saveFilePath = null)
        {
            if (llmSummarizer == null) throw new ArgumentNullException(nameof(llmSummarizer));
            if (totalLayers < 1) throw new ArgumentException("层数至少为1");
            if (maxItemsLayer0 < summarizeBatchSize || maxItemsOtherLayers < summarizeBatchSize)
                throw new ArgumentException("容量必须≥批处理大小");

            _llmSummarizer = llmSummarizer;
            _maxItemsLayer0 = maxItemsLayer0;
            _maxItemsOtherLayers = maxItemsOtherLayers;
            _summarizeBatchSize = summarizeBatchSize;
            _saveFilePath = saveFilePath;

            _layers = new List<List<string>>();
            for (int i = 0; i < totalLayers; i++) _layers.Add(new List<string>());

            if (!string.IsNullOrEmpty(_saveFilePath)) LoadFromFile();

            Debug.Log($"[HierarchicalMemory] 初始化: {totalLayers}层 | L0:{maxItemsLayer0} L1+:{maxItemsOtherLayers} | 压缩:{summarizeBatchSize}");
        }

        // ================= 公共方法 =================
        
        public void AddMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            _layers[0].Add(message);  // 同步添加到Layer 0

            // 后台异步处理压缩，不阻塞调用者
            Task.Run(async () =>
            {
                lock (_processLock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }

                try
                {
                    await ProcessLayerAsync(0);
                    SaveToFile();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HierarchicalMemory] 后台失败: {ex.Message}");
                }
                finally
                {
                    lock (_processLock) { _isProcessing = false; }
                }
            });
        }

        public string GetContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("【对话记忆（仅供参考，回复时必须使用 [标签] ||| 日文 ||| 中文 格式）】");

            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                if (_layers[i].Count == 0) continue;
                sb.AppendLine($"\n{GetLayerName(i)}:");
                foreach (var item in _layers[i]) sb.AppendLine(item);
            }
            return sb.ToString();
        }

        public void ClearAllMemory()
        {
            foreach (var layer in _layers) layer.Clear();
            try
            {
                if (!string.IsNullOrEmpty(_saveFilePath) && File.Exists(_saveFilePath))
                    File.Delete(_saveFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchicalMemory] 清除文件失败: {ex.Message}");
            }
        }
        public string GetMemoryStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 记忆系统状态 ===");
            for (int i = 0; i < _layers.Count; i++)
                sb.AppendLine($"{GetLayerName(i)}: {_layers[i].Count}/{GetMaxItemsForLayer(i)} 条");
            return sb.ToString();
        }

        // ================= 私有方法 =================

        private int GetMaxItemsForLayer(int layerIndex)
        {
            return layerIndex == 0 ? _maxItemsLayer0 : _maxItemsOtherLayers;
        }

        private async Task ProcessLayerAsync(int layerIndex)
        {
            int maxItems = GetMaxItemsForLayer(layerIndex);
            if (_layers[layerIndex].Count < maxItems) return;  // 未满则无需压缩

            try
            {
                // 1. 提取最老的N条进行压缩
                var itemsToSummarize = _layers[layerIndex].Take(_summarizeBatchSize).ToList();
                string summary = await CallLlmToSummarizeAsync(itemsToSummarize, layerIndex);
                
                if (string.IsNullOrWhiteSpace(summary)) return;

                bool isTopLayer = (layerIndex >= _layers.Count - 1);

                if (isTopLayer)
                {
                    // 2a. 顶层：压缩后放回自己这层
                    _layers[layerIndex].RemoveRange(0, _summarizeBatchSize);
                    _layers[layerIndex].Add(summary);
                }
                else
                {
                    // 2b. 非顶层：上传到上一层，递归检查
                    _layers[layerIndex + 1].Add(summary);
                    _layers[layerIndex].RemoveRange(0, _summarizeBatchSize);
                    await ProcessLayerAsync(layerIndex + 1);  // 递归处理上层
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchicalMemory] Layer {layerIndex} 错误: {ex.Message}");
            }
        }

        private async Task<string> CallLlmToSummarizeAsync(List<string> items, int layerIndex)
        {
            // 根据层级调整prompt策略：使用中文避免干扰微调模型格式
            string strategyHint = layerIndex == 0
                ? "请用一句中文总结以下对话要点，保留关键信息（人名、事件、情绪）。只输出纯文本总结，不要使用任何标签或特殊格式。"
                : "请用一句中文（不超过50字）概括核心主题。只输出纯文本，不要使用任何标签或特殊格式。";

            string prompt = $"{strategyHint}\n\n内容:\n{string.Join("\n", items.Select((item, idx) => $"{idx + 1}. {item}"))}\n\n总结:";

            try
            {
                return await _llmSummarizer(prompt);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchicalMemory] LLM失败: {ex.Message}");
                return $"[Auto] {string.Join("; ", items.Take(2))}...";
            }
        }

        private string GetLayerName(int i)
        {
            return i == 0 ? "[Recent Conversations]" : i == 1 ? "[Mid-term Summary]" : "[Long-term Memory]";
        }

        // ================= 持久化 =================

        public void SaveToFile()
        {
            if (string.IsNullOrEmpty(_saveFilePath)) return;

            try
            {
                string directory = Path.GetDirectoryName(_saveFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var sb = new StringBuilder();
                sb.AppendLine("[METADATA]");
                sb.AppendLine($"SaveTime={System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"TotalLayers={_layers.Count}");
                sb.AppendLine($"MaxItemsLayer0={_maxItemsLayer0}");
                sb.AppendLine($"MaxItemsOtherLayers={_maxItemsOtherLayers}");
                sb.AppendLine($"SummarizeBatchSize={_summarizeBatchSize}");
                sb.AppendLine();

                // 从高层到低层保存（便于阅读时从宏观到微观）
                for (int i = _layers.Count - 1; i >= 0; i--)
                {
                    sb.AppendLine($"[LAYER_{i}]");
                    sb.AppendLine($"; {GetLayerName(i)}");
                    
                    if (_layers[i].Count == 0)
                    {
                        sb.AppendLine("; (empty)");
                    }
                    else
                    {
                        foreach (var item in _layers[i])
                        {
                            // 转义换行符，确保每条记录占一行
                            string escaped = item.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
                            sb.AppendLine(escaped);
                        }
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(_saveFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchicalMemory] 保存失败: {ex.Message}");
            }
        }

        public void LoadFromFile()
        {
            if (string.IsNullOrEmpty(_saveFilePath) || !File.Exists(_saveFilePath)) return;

            try
            {
                string[] lines = File.ReadAllLines(_saveFilePath, Encoding.UTF8);
                foreach (var layer in _layers) layer.Clear();

                int currentLayer = -1;
                bool inMetadata = false;
                
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";")) continue;  // 跳过空行和注释

                    // 解析INI节标记
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        string sectionName = trimmed.Substring(1, trimmed.Length - 2);
                        if (sectionName == "METADATA")
                        {
                            inMetadata = true;
                            currentLayer = -1;
                        }
                        else if (sectionName.StartsWith("LAYER_"))
                        {
                            inMetadata = false;
                            if (int.TryParse(sectionName.Substring(6), out int layerNum))
                                currentLayer = layerNum;
                        }
                        continue;
                    }

                    if (inMetadata) continue;  // 跳过元数据内容

                    // 还原换行符并添加到对应层
                    if (currentLayer >= 0 && currentLayer < _layers.Count)
                    {
                        string unescaped = trimmed.Replace("\\n", "\n");
                        _layers[currentLayer].Add(unescaped);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchicalMemory] 加载失败: {ex.Message}");
            }
        }
    }
}
