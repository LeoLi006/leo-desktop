using NAudio.Wave;
using SherpaOnnx;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
namespace LeoLauncher;

sealed class LocalAsr:IDisposable
{
    readonly object gate=new();OfflineRecognizer? engine;bool disposed;
    public static string ModelDirectory=>Path.Combine(Store.Root,"models","sensevoice");
    public static bool Installed=>File.Exists(Path.Combine(ModelDirectory,"model.int8.onnx"))&&File.Exists(Path.Combine(ModelDirectory,"tokens.txt"));
    public string Decode(float[] samples,CancellationToken token)
    {
        lock(gate){
            token.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException(nameof(LocalAsr));
            if(!Installed)throw new InvalidOperationException("本地 SenseVoice 模型不完整，请保留 models 文件夹");
            if(engine==null){var config=new OfflineRecognizerConfig();config.FeatConfig.SampleRate=16000;config.FeatConfig.FeatureDim=80;config.ModelConfig.NumThreads=2;config.ModelConfig.Provider="cpu";config.ModelConfig.Tokens=Path.Combine(ModelDirectory,"tokens.txt");config.ModelConfig.SenseVoice.Model=Path.Combine(ModelDirectory,"model.int8.onnx");config.ModelConfig.SenseVoice.Language="zh";config.ModelConfig.SenseVoice.UseInverseTextNormalization=1;engine=new OfflineRecognizer(config);}
            using var stream=engine.CreateStream();stream.AcceptWaveform(16000,samples);engine.Decode(stream);token.ThrowIfCancellationRequested();return Regex.Replace(stream.Result.Text,@"<\|[^|]*\|>","").Trim();
        }
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;engine?.Dispose();engine=null;}}
}
sealed class SpeechIO:IDisposable
{
    readonly LocalAsr asr=new();readonly object audioGate=new();readonly List<float> audio=new();
    WaveInEvent? capture;CancellationTokenSource? decoding;
    SpeechSynthesizer? speaker;Prompt? activePrompt;
    bool disposed;long session;long lastMeter;int silenceSamples,voiceSamples;bool heardVoice;double peakRms;
    public int InputDevice=-1;
    public static (int Id,string Name)[] InputDevices()=>Enumerable.Range(0,WaveInEvent.DeviceCount).Select(i=>(i,WaveInEvent.GetCapabilities(i).ProductName)).ToArray();
    public bool Listening{get;private set;}
    public bool Finishing{get;private set;}
    public bool Speaking{get;private set;}
    public event Action<string,bool>? Recognized;
    public event Action<string>? Status;
    public event Action? Changed;
    public void Start()
    {
        if(disposed||Listening||Finishing)return;
        if(!LocalAsr.Installed)throw new InvalidOperationException("缺少 SenseVoice 本地模型");
        StopSpeaking();decoding?.Cancel();decoding?.Dispose();decoding=new CancellationTokenSource();var token=decoding.Token;long id=Interlocked.Increment(ref session);
        lock(audioGate){audio.Clear();silenceSamples=voiceSamples=0;heardVoice=false;peakRms=0;}
        var device=new WaveInEvent{DeviceNumber=InputDevice,WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=100,NumberOfBuffers=3};capture=device;
        device.DataAvailable+=(_,e)=>{
            if(disposed||id!=session||!Listening||Finishing)return;
            double energy=0;bool finish=false;
            lock(audioGate){for(int i=0;i+1<e.BytesRecorded;i+=2){float sample=BitConverter.ToInt16(e.Buffer,i)/32768f;audio.Add(sample);energy+=sample*sample;}
                int count=e.BytesRecorded/2;double rms=Math.Sqrt(energy/Math.Max(1,count));
                peakRms=Math.Max(peakRms,rms);
                if(rms>.006){heardVoice=true;voiceSamples+=count;silenceSamples=0;}else silenceSamples+=count;
                finish=audio.Count>=16000*60||(heardVoice&&voiceSamples>=3200&&silenceSamples>=16000);
                if(Environment.TickCount64-lastMeter>1500){lastMeter=Environment.TickCount64;Status?.Invoke(rms>.003?"正在录音 · 停顿后自动转写":"正在听 · 请确认默认麦克风有声音");}
            }
            if(finish)_=Task.Run(()=>{if(id==session)FinishListening();});
        };
        device.RecordingStopped+=(_,e)=>{
            device.Dispose();if(ReferenceEquals(capture,device))capture=null;
            if(disposed||id!=session||token.IsCancellationRequested)return;
            Listening=false;Finishing=true;Changed?.Invoke();
            if(e.Exception!=null){Finishing=false;Changed?.Invoke();Status?.Invoke("录音失败："+e.Exception.Message);return;}
            float[] samples;lock(audioGate){samples=audio.ToArray();audio.Clear();}
            if(samples.Length<1600||peakRms<.001){Finishing=false;Changed?.Invoke();Status?.Invoke("没有录到有效语音，请检查默认麦克风");return;}
            Status?.Invoke("SenseVoice 本地转写中…");
            _=Task.Run(()=>{
                try{string text=asr.Decode(samples,token);if(!disposed&&id==session&&!token.IsCancellationRequested){if(text.Length>0)Recognized?.Invoke(text,true);else Status?.Invoke("没有识别到文字，请再试一次");}}
                catch(OperationCanceledException){}
                catch(Exception error){if(!disposed&&id==session)Status?.Invoke("本地转写失败："+error.Message);}
                finally{if(!disposed&&id==session){Finishing=false;Changed?.Invoke();}}
            });
        };
        Listening=true;try{device.StartRecording();}catch{Listening=false;capture=null;device.Dispose();throw;}
        Changed?.Invoke();Status?.Invoke("SenseVoice 正在录音 · 停顿1秒或再次点击后转写");
    }
    public void FinishListening(){if(!Listening||Finishing)return;Finishing=true;Changed?.Invoke();Status?.Invoke("结束录音，准备本地转写…");try{capture?.StopRecording();}catch{StopListening();}}
    public void StopListening(){Interlocked.Increment(ref session);decoding?.Cancel();Listening=false;Finishing=false;var current=capture;capture=null;try{current?.StopRecording();}catch{current?.Dispose();}lock(audioGate)audio.Clear();Changed?.Invoke();}
    public static string PlainSpeech(string text){string plain=Regex.Replace(text,@"```[\s\S]*?```"," 代码片段略。 ");plain=Regex.Replace(plain,@"\[([^\]]+)\]\([^)]+\)","$1");plain=Regex.Replace(plain,@"[*#`_]+","");return plain.Length>12000?plain[..12000]:plain;}
    public void Speak(string text)
    {
        if(disposed||string.IsNullOrWhiteSpace(text))return;StopListening();StopSpeaking();
        if(speaker==null){speaker=new SpeechSynthesizer();var voice=speaker.GetInstalledVoices().FirstOrDefault(x=>x.Enabled&&x.VoiceInfo.Culture.Name=="zh-CN");if(voice!=null)speaker.SelectVoice(voice.VoiceInfo.Name);speaker.SpeakCompleted+=(_,e)=>{if(!disposed&&ReferenceEquals(e.Prompt,activePrompt)){activePrompt=null;Speaking=false;Changed?.Invoke();}};}
        speaker.SetOutputToDefaultAudioDevice();Speaking=true;try{activePrompt=speaker.SpeakAsync(PlainSpeech(text));}catch{Speaking=false;throw;}Changed?.Invoke();
    }
    public void StopSpeaking(){activePrompt=null;try{speaker?.SpeakAsyncCancelAll();}catch{}Speaking=false;Changed?.Invoke();}
    public void Stop(){StopListening();StopSpeaking();}
    public void Dispose(){if(disposed)return;disposed=true;Stop();decoding?.Dispose();speaker?.Dispose();_=Task.Run(asr.Dispose);}
    public static object VerifyToFile(string file)
    {
        using var synth=new SpeechSynthesizer();var voice=synth.GetInstalledVoices().FirstOrDefault(x=>x.Enabled&&x.VoiceInfo.Culture.Name=="zh-CN");if(voice==null)return new{ok=false,reason="缺少测试合成声音"};
        synth.SelectVoice(voice.VoiceInfo.Name);synth.SetOutputToWaveFile(file,new System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,System.Speech.AudioFormat.AudioBitsPerSample.Sixteen,System.Speech.AudioFormat.AudioChannel.Mono));synth.Speak("你好，请打开记事本。");synth.SetOutputToNull();
        using var wav=new WaveFileReader(file);var provider=wav.ToSampleProvider();var samples=new List<float>();float[] buffer=new float[16000];int n;while((n=provider.Read(buffer,0,buffer.Length))>0)samples.AddRange(buffer.Take(n));
        using var engine=new LocalAsr();var watch=System.Diagnostics.Stopwatch.StartNew();string text=engine.Decode(samples.ToArray(),CancellationToken.None);watch.Stop();double cold=watch.Elapsed.TotalSeconds;
        watch.Restart();string warm=engine.Decode(samples.ToArray(),CancellationToken.None);watch.Stop();
        double warmSeconds=watch.Elapsed.TotalSeconds;
        string referenceText="";string reference=Path.Combine(Store.Root,"checks","reference-zh.wav");
        if(File.Exists(reference)){using var reader=new WaveFileReader(reference);var source=reader.ToSampleProvider();var human=new List<float>();while((n=source.Read(buffer,0,buffer.Length))>0)human.AddRange(buffer.Take(n));referenceText=engine.Decode(human.ToArray(),CancellationToken.None);}
        var process=System.Diagnostics.Process.GetCurrentProcess();return new{ok=text.Contains("记事本")&&warm.Contains("记事本")&&(!File.Exists(reference)||Regex.IsMatch(referenceText,"[\\u4e00-\\u9fff]")),engine="SenseVoice INT8 / sherpa-onnx CPU 2 threads",text,cold_seconds=cold,warm_seconds=warmSeconds,audio_seconds=samples.Count/16000.0,reference_text=referenceText,process_working_set_mb=process.WorkingSet64/1048576.0,model_mb=new FileInfo(Path.Combine(LocalAsr.ModelDirectory,"model.int8.onnx")).Length/1048576.0,microphone_used=false,external_audio_requests=0};
    }
}
