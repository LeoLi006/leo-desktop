using System.Text.Json;
namespace LeoLauncher;
static class ResearchTitleTranslationTests
{
    internal static string Reply(string[] titles)=>JsonSerializer.Serialize(new{translations=titles.Select((title,id)=>new{id,title="中文新闻标题 "+id}).Reverse()});
    public static async Task Check(string folder,Dictionary<string,bool> c)
    {
        int calls=0;var batches=new List<int>();string cache=Path.Combine(folder,Guid.NewGuid().ToString("N")+"-titles.json");
        var translator=new ResearchTitleTranslator(cache,(titles,token)=>{token.ThrowIfCancellationRequested();calls++;batches.Add(titles.Length);return Task.FromResult(Reply(titles));});
        var titles=Enumerable.Range(0,21).Select(i=>"Public AI headline "+i).ToArray();var result=await translator.Translate(titles.Concat(new[]{titles[0],"已有中文标题"}),default);
        c["translation_batched_deduplicated"]=calls==2&&batches.SequenceEqual(new[]{20,1})&&result.Translated==21;
        c["translation_maps_reordered_ids"]=translator.Display(titles[3])=="中文新闻标题 3";
        await translator.Translate(titles,default);c["cached_titles_no_model_requests"]=calls==2;
        var reopened=new ResearchTitleTranslator(cache,(_,_)=>throw new Exception("must use cache"));c["translated_titles_persist"]=(await reopened.Translate(titles,default)).Translated==0&&reopened.Display(titles[0])=="中文新闻标题 0";
        var messages=ResearchTitleTranslator.Messages(["Public title"]);using(var json=JsonDocument.Parse(messages[1].content))c["model_receives_only_title_and_local_id"]=messages.Length==2&&json.RootElement[0].EnumerateObject().Select(p=>p.Name).SequenceEqual(new[]{"id","title"})&&json.RootElement[0].GetProperty("title").GetString()=="Public title";
        int boundedCalls=0;var bounded=new ResearchTitleTranslator(null,(batch,_)=>{boundedCalls++;return Task.FromResult(Reply(batch));});
        c["translation_request_cap"]=(await bounded.Translate(Enumerable.Range(0,100).Select(i=>"Bounded headline "+i),default)).Translated==60&&boundedCalls==3;
        c["changed_headline_invalidates_old_translation"]=translator.Missing("Public AI headline 0 revised");
        bool bad=false;try{ResearchTitleTranslator.Parse("{\"translations\":[{\"id\":7,\"title\":\"错误标题\"}]}",["Original"]);}catch(IOException){bad=true;}c["unknown_translation_id_rejected"]=bad;
        bad=false;try{ResearchTitleTranslator.Parse("{\"translations\":[{\"id\":0,\"title\":\"Not translated\"}]}",["Original"]);}catch(IOException){bad=true;}c["non_chinese_result_rejected"]=bad;
        c["fenced_translation_supported"]=ResearchTitleTranslator.Parse("```json\n"+Reply(["Original"])+"\n```",["Original"]).Single().Chinese=="中文新闻标题 0";
        var malformed=new ResearchTitleTranslator(null,(_,_)=>Task.FromResult("invalid json"));try{await malformed.Translate(["Keep original"],default);}catch(JsonException){}c["malformed_response_keeps_original"]=malformed.Display("Keep original")=="Keep original";
        using(var cancel=new CancellationTokenSource()){
            var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var blocked=new ResearchTitleTranslator(null,async(_,token)=>{started.SetResult();await Task.Delay(Timeout.Infinite,token);return "";});
            var pending=blocked.Translate(["Cancel me"],cancel.Token);await started.Task;cancel.Cancel();bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}c["translation_cancel_keeps_original"]=cancelled&&blocked.Display("Cancel me")=="Cancel me";
        }
        string before=File.ReadAllText(cache);var hook=AtomicJson.BeforeReplace;try{
            AtomicJson.BeforeReplace=path=>{if(path==cache)throw new IOException("fixture write failure");};var unsaved=await translator.Translate(["Another public headline"],default);
            c["translation_save_failure_preserves_disk_and_memory"]=File.ReadAllText(cache)==before&&translator.Display("Another public headline").StartsWith("中文")&&unsaved.Note.Contains("缓存保存失败");
        }finally{AtomicJson.BeforeReplace=hook;}
    }
}
