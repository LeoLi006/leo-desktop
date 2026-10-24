using System.Text.RegularExpressions;
namespace LeoLauncher;

sealed record ResearchArea(string Id,string Name,string Arxiv,string Github,string News,string Pattern)
{
    public bool Matches(string text)=>Id=="all"||Regex.IsMatch(text,Pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
}
static class ResearchAreas
{
    // Queries are data, not model prompts. Choosing a field requests that field from each source.
    public static readonly ResearchArea[] All=[
        new("all","全部 AI 领域","cat:cs.AI OR cat:cs.LG OR cat:cs.CL OR cat:cs.CV OR cat:cs.RO OR cat:cs.SD OR cat:cs.NE OR cat:stat.ML","topic:artificial-intelligence","人工智能 OR 大模型 OR artificial intelligence",""),
        new("agent","Agent / 多智能体","all:agentic OR (all:agent AND all:\"language model\")","agent llm","AI agent OR 智能体 OR agentic","\\bagents?\\b|agentic|tool.use|multi.agent|智能体|工具调用"),
        new("duplex","全双工 / 实时语音","all:\"full-duplex\" OR all:\"full duplex\" OR all:\"speech-to-speech\" OR all:\"turn-taking\"","\"full-duplex\"","全双工 OR 实时语音 OR full-duplex OR speech-to-speech","full[ -]duplex|speech.to.speech|turn.taking|real.time (voice|speech|audio|dialog)|全双工|实时语音|语音交互"),
        new("llm","大语言模型 / 基础模型","all:\"large language model\" OR all:\"foundation model\"","large-language-model","大语言模型 OR 基础模型 OR LLM","language.model|foundation.model|\\bLLMs?\\b|大模型|语言模型|基础模型|GPT|Claude|Gemini|DeepSeek|Qwen|Llama"),
        new("reasoning","推理 / 数学 / 规划","all:\"chain of thought\" OR all:\"language model reasoning\" OR all:\"test-time scaling\"","llm reasoning","AI推理 OR 大模型数学 OR reasoning model","reasoning|chain.of.thought|test.time|planning|推理|数学|规划"),
        new("speech","语音 / 音频 / ASR / TTS","cat:cs.SD OR (cat:eess.AS AND (all:neural OR all:learning))","speech","语音识别 OR 语音合成 OR AI音频","\\bASR\\b|\\bTTS\\b|speech|audio|语音|音频|声音"),
        new("nlp","自然语言处理 / 文本","cat:cs.CL","natural-language-processing","自然语言处理 OR NLP OR 机器翻译","\\bNLP\\b|natural.language|translation|text|自然语言|文本|翻译"),
        new("multimodal","多模态 / 视觉语言","all:multimodal OR all:\"vision language\"","multimodal","多模态 OR 视觉语言 OR multimodal","multi.modal|vision.language|\\bVLM\\b|多模态|视觉语言"),
        new("vision","计算机视觉 / 3D","cat:cs.CV","computer-vision","计算机视觉 OR AI三维 OR computer vision","vision|3D|detection|segmentation|视觉|三维|检测|分割"),
        new("generative","图像 / 视频 / 生成模型","(cat:cs.CV OR cat:cs.LG) AND (all:diffusion OR all:\"video generation\" OR all:\"image generation\")","diffusion","AI视频 OR AI绘画 OR 文生视频 OR diffusion","diffusion|image.generation|video.generation|text.to.(image|video)|扩散|生成|绘画|视频"),
        new("robotics","机器人 / 具身智能 / 自动驾驶","cat:cs.RO OR all:\"embodied intelligence\" OR all:\"autonomous driving\"","robotics","具身智能 OR 机器人 OR 自动驾驶","robot|embodied|autonomous.driv|\\bVLA\\b|机器人|具身|自动驾驶"),
        new("rl","强化学习 / 决策","all:\"reinforcement learning\" OR all:\"reinforcement fine-tuning\"","reinforcement-learning","强化学习 OR reinforcement learning","reinforcement|\\bRLHF\\b|\\bGRPO\\b|\\bPPO\\b|强化学习|决策"),
        new("rag","RAG / 检索 / 知识图谱","all:\"retrieval augmented\" OR all:\"knowledge graph\" OR (cat:cs.IR AND all:neural)","rag","RAG OR 检索增强 OR 知识图谱","\\bRAG\\b|retrieval|knowledge.graph|检索|知识图谱|向量数据库"),
        new("alignment","对齐 / 安全 / 可解释性","all:\"AI safety\" OR all:\"model alignment\" OR all:mechanistic OR all:jailbreak","llm safety","AI安全 OR 模型对齐 OR 可解释性","alignment|safety|interpretability|jailbreak|对齐|安全|可解释|越狱"),
        new("evaluation","评测 / 基准 / 数据集","(cat:cs.AI OR cat:cs.CL OR cat:cs.LG) AND (ti:benchmark OR ti:evaluation OR ti:dataset)","llm evaluation","AI评测 OR 大模型基准 OR AI数据集","benchmark|evaluat|dataset|leaderboard|评测|基准|数据集|排行榜"),
        new("training","训练 / 微调 / 合成数据","all:\"fine tuning\" OR all:\"synthetic data\" OR all:\"model training\"","fine-tuning","大模型训练 OR 微调 OR 合成数据","train|fine.tun|synthetic.data|pretrain|训练|微调|合成数据|预训练"),
        new("efficiency","高效推理 / 压缩 / 端侧","all:quantization OR all:\"model compression\" OR all:\"speculative decoding\" OR all:\"edge AI\"","quantization","模型量化 OR 端侧AI OR 推理加速","quantiz|compress|speculative|inference|edge.AI|量化|压缩|端侧|加速|推理部署"),
        new("systems","AI 系统 / 算力 / 芯片","(cat:cs.DC OR cat:cs.AR OR cat:cs.LG) AND (all:GPU OR all:\"distributed training\" OR all:\"model serving\")","llm inference","AI芯片 OR GPU OR AI算力","\\bGPU\\b|\\bTPU\\b|CUDA|distributed|serving|芯片|算力|分布式|集群|英伟达|NVIDIA"),
        new("ai4science","AI4Science / 科学计算","all:\"AI for science\" OR all:\"scientific machine learning\" OR all:\"neural operator\"","ai4science","AI4Science OR AI科学 OR 科学智能","science|scientific|protein|molecule|physics|weather|科学|蛋白|分子|物理|气象"),
        new("medical","医疗 / 生物 / 药物发现","(all:medical OR all:clinical OR all:\"drug discovery\") AND (all:\"deep learning\" OR all:\"language model\" OR all:AI)","medical ai","AI医疗 OR AI制药 OR 生物人工智能","medical|clinical|drug|health|biolog|医疗|临床|药物|制药|生物|健康"),
        new("recommendation","推荐 / 搜索 / 广告","cat:cs.IR AND (all:recommendation OR all:ranking OR all:search)","recommendation","AI推荐 OR 智能搜索 OR AI广告","recommend|ranking|search|advertis|推荐|搜索|广告|排序"),
        new("graph","图学习 / 时序 / 表格","all:\"graph neural\" OR all:\"time series\" OR all:\"tabular learning\"","graph-neural-network","图神经网络 OR AI时序 OR 表格模型","graph.neural|time.series|tabular|图神经|图学习|时序|表格"),
        new("privacy","隐私 / 联邦学习 / 版权","all:\"federated learning\" OR all:\"differential privacy\" OR (all:copyright AND all:AI)","federated-learning","AI隐私 OR 联邦学习 OR AI版权","privacy|federated|copyright|watermark|隐私|联邦|版权|水印"),
        new("industry","产业 / 产品 / 政策","(cat:cs.CY OR cat:cs.AI) AND (all:policy OR all:deployment OR all:economic)","artificial-intelligence","AI产业 OR AI产品 OR 人工智能政策 OR AI融资","policy|regulat|product|launch|funding|产业|政策|监管|产品|发布|融资|开源")
    ];
    public static ResearchArea Get(string id)=>All.FirstOrDefault(a=>a.Id==id)??throw new ArgumentException("未知 AI 领域");
    public static string Label(string id)=>UiLanguage.T(id=="general"?"综合":All.FirstOrDefault(a=>a.Id==id)?.Name??"全部 AI 领域");
}
