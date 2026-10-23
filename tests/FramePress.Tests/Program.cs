using System.Text.Json;
using FramePress.Core;

var failures = new List<string>(); var passed = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed.Add(name); Console.WriteLine("PASS: " + name); }
    catch (Exception e) { failures.Add(name + ": " + e.Message); Console.WriteLine("FAIL: " + name + " · " + e); }
}
void Check(bool value, string message) { if (!value) throw new Exception(message); }
Task Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return Task.CompletedTask; }
    throw new Exception("Expected " + typeof(T).Name);
}
var root = Path.Combine(Path.GetTempPath(), "FramePress-Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root); var engine = new MediaEngine();
async Task<MediaInfo> Video(string name = "مقطع space.mov", int width = 320, int height = 180, bool sound = true, double seconds = 2)
{
    var path = Path.Combine(root, name);
    var args = new List<string> { "-v", "error", "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:rate=24" };
    if (sound) args.AddRange(["-f", "lavfi", "-i", "sine=frequency=440"]);
    args.AddRange(["-t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:v", "mpeg4", "-q:v", "1"]);
    if (sound) args.AddRange(["-c:a", "pcm_s16le"]);
    args.Add(path);
    var result = await ProcessRunner.RunAsync(engine.Ffmpeg, args, CancellationToken.None);
    if (result.ExitCode != 0) throw new Exception(result.Error);
    return await engine.ProbeAsync(path, CancellationToken.None);
}
try
{
    await Test("FFmpeg tools and H264 available", async () => await engine.VerifyToolsAsync(CancellationToken.None));
    await Test("portrait and landscape classify equally", () =>
    {
        Check(MediaEngine.Classify(1920,1080)=="1080p", "landscape"); Check(MediaEngine.Classify(1080,1920)=="1080p", "portrait");
        Check(MediaEngine.Classify(4096,2160)=="4K", "DCI"); Check(MediaEngine.Classify(7680,4320)=="8K+", "8K"); return Task.CompletedTask;
    });
    await Test("invalid CRF and preset rejected", async () =>
    {
        await Throws<ArgumentException>(() => new CompressionProfile { Crf = 99 }.Validate());
        await Throws<ArgumentException>(() => new CompressionProfile { Preset = "fake" }.Validate());
    });
    await Test("recursive scan ignores generated and unrelated files", () =>
    {
        var dir=Path.Combine(root,"scan"); Directory.CreateDirectory(Path.Combine(dir,"a","b"));
        foreach(var f in new[]{"a/b/input.mp4","a/b/out.framepress-123.mp4","a/b/old.mov.framepress-backup-123",".framepress-work-12.mp4","image.png","x.txt"}) File.WriteAllText(Path.Combine(dir,f),"");
        Check(MediaEngine.Discover(dir,true).Count()==2,"recursive"); Check(MediaEngine.Discover(dir,false).Count()==1,"flat"); return Task.CompletedTask;
    });
    var source = await Video();
    await Test("real H264 encode preserves source audio and dimensions", async () =>
    {
        var before=File.ReadAllBytes(source.Path); var result=await engine.ProcessAsync(source,new(),new(false,null,root),null,CancellationToken.None);
        Check(result.State=="Done","not compressed"); Check(File.ReadAllBytes(source.Path).SequenceEqual(before),"source changed");
        var output=await engine.ProbeAsync(result.Output,CancellationToken.None);
        Check(output.AudioStreams==1 && output.Width==320 && output.Height==180,"properties lost"); Check(Math.Abs(output.Duration-source.Duration)<.2,"duration");
    });
    await Test("video without audio works", async () =>
    {
        var m=await Video("silent.mov",sound:false);
        var r=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None); Check(r.State=="Done","silent output");
    });
    await Test("replacement preserves original recovery bytes", async () =>
    {
        var m=await Video("replace.mov"); var bytes=File.ReadAllBytes(m.Path);
        var r=await engine.ProcessAsync(m,new(),new(true,null,root),null,CancellationToken.None);
        Check(r.State=="Done" && File.Exists(r.Output),"output"); Check(File.ReadAllBytes(r.Backup).SequenceEqual(bytes),"backup"); Check(!File.Exists(m.Path),"old container path");
        Check(!Directory.EnumerateFiles(root,".framepress-transaction-*.json").Any(),"transaction not completed");
    });
    await Test("MP4 same-path replacement keeps recovery backup", async () =>
    {
        var m=await Video("first.mov"); var copy=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None);
        var high=await engine.ProbeAsync(copy.Output,CancellationToken.None);
        // Explicitly generate a padded high-bitrate MP4 so the replacement remains smaller.
        var path=Path.Combine(root,"same.mp4"); var encode=await ProcessRunner.RunAsync(engine.Ffmpeg,["-v","error","-i",m.Path,"-c:v","libx264","-crf","0","-c:a","aac",path],CancellationToken.None);
        Check(encode.ExitCode==0,"fixture"); var sourceMp4=await engine.ProbeAsync(path,CancellationToken.None); var bytes=File.ReadAllBytes(path);
        var r=await engine.ProcessAsync(sourceMp4,new(),new(true,null,root),null,CancellationToken.None);
        Check(r.State=="Done" && r.Output==path,"same path"); Check(File.ReadAllBytes(r.Backup).SequenceEqual(bytes),"mp4 recovery");
    });
    await Test("pre-cancel leaves master untouched", async () =>
    {
        using var c=new CancellationTokenSource(); c.Cancel();
        try { await engine.ProcessAsync(source,new(),new(true,null,root),null,c.Token); throw new Exception("not cancelled"); }
        catch(OperationCanceledException) { Check(File.Exists(source.Path),"lost master"); }
    });
    await Test("cancellation kills active encode and removes temporary output", async () =>
    {
        var m=await Video("cancel.mov",640,360,false,6); using var c=new CancellationTokenSource(); c.CancelAfter(100);
        try { await engine.ProcessAsync(m,new(){Preset="slower"},new(true,null,root),null,c.Token); throw new Exception("not cancelled"); }
        catch(OperationCanceledException) { Check(File.Exists(m.Path),"master removed"); Check(!Directory.EnumerateFiles(root,".framepress-work-*").Any(),"temporary output left"); }
    });
    await Test("HDR blocked before encoding", async () => await Throws<IOException>(() => engine.BuildCommand(source with { Transfer="smpte2084" },new(),new(false,null,root),"out.mp4")));
    await Test("10-bit safeguard and optional H265 output", async () =>
    {
        var high=source with { PixelFormat="yuv422p10le" };
        await Throws<IOException>(() => engine.BuildCommand(high,new(),new(false,null,root),"out.mp4"));
        var args=engine.BuildCommand(high,new(){Codec="H.265"},new(false,null,root,AllowHighBitDepth:true),"out.mp4");
        Check(args.Contains("yuv420p10le"),"10bit missing");
    });
    await Test("subtitles and multiple video streams blocked", async () =>
    {
        await Throws<IOException>(() => MediaEngine.ValidateMedia(source with { HasSubtitles=true },new(),new(false,null,root)));
        await Throws<IOException>(() => MediaEngine.ValidateMedia(source with { VideoStreams=2 },new(),new(false,null,root)));
    });
    await Test("data streams require copy mode", async () =>
    {
        await Throws<IOException>(() => MediaEngine.ValidateMedia(source with { HasData=true },new(),new(true,null,root)));
        MediaEngine.ValidateMedia(source with { HasData=true },new(),new(false,null,root));
    });
    await Test("source changed since scan is never replaced", async () =>
    {
        var m=await Video("changed.mov"); File.SetLastWriteTimeUtc(m.Path,m.LastWriteUtc.AddSeconds(5));
        try { await engine.ProcessAsync(m,new(),new(true,null,root),null,CancellationToken.None); throw new Exception("not rejected"); }
        catch(IOException e) { Check(e.Message.Contains("Source changed"),e.Message); Check(File.Exists(m.Path),"source missing"); }
    });
    await Test("destination preserves nested structure", () =>
    {
        var m=source with { Path=Path.Combine(root,"input","a","b","video.mov") };
        var o=new JobOptions(false,Path.Combine(root,"output"),Path.Combine(root,"input"));
        Check(MediaEngine.OutputDirectory(m,o)==Path.Combine(root,"output","a","b"),"structure"); return Task.CompletedTask;
    });
    await Test("destination traversal blocked", async () =>
    {
        await Throws<IOException>(() => MediaEngine.OutputDirectory(source,new(false,Path.Combine(root,"out"),Path.Combine(root,"unrelated"))));
    });
    await Test("sample estimate scales with actual sample duration", () =>
    {
        var e=MediaEngine.Estimate(10,20,1_000_000,3600);
        Check(e.HourMinutes==30 && e.FullBytes==180_000_000,"estimate math"); return Task.CompletedTask;
    });
    await Test("sample never replaces the original", async () =>
    {
        var r=await engine.ProcessAsync(source,new(),new(true,null,root,Sample:true),null,CancellationToken.None);
        Check(r.State=="Sample" && r.HourMinutes>0 && r.EstimatedFullBytes>0,"estimate"); Check(File.Exists(source.Path) && r.Backup=="","sample changed original");
    });
    await Test("PNG image to WebP", async () =>
    {
        var image=Path.Combine(root,"photo.png");
        var fixture=await ProcessRunner.RunAsync(engine.Ffmpeg,["-v","error","-f","lavfi","-i","testsrc2=size=512x512","-frames:v","1",image],CancellationToken.None); Check(fixture.ExitCode==0,fixture.Error);
        var m=await engine.ProbeAsync(image,CancellationToken.None); var r=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None);
        Check(r.State=="Done" && Path.GetExtension(r.Output)==".webp","WebP"); Check(File.Exists(image),"original image lost");
    });
    await Test("larger image output discarded", async () =>
    {
        var image=Path.Combine(root,"tiny.png");
        File.WriteAllBytes(image,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNgZGJmAAAAFQAH8/QyXgAAAABJRU5ErkJggg=="));
        var m=await engine.ProbeAsync(image,CancellationToken.None);
        var r=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None);
        Check(r.State=="LargerSkipped" && File.Exists(image),"larger guard");
    });
    await Test("animated PNG and WebP rejected without losing frames", async () =>
    {
        foreach(var (extension,data) in new[] {
            (".png","iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAAEAAAABAAAAAAAAAAAAAEACgAAV3ID4QAAABVJREFUeJxj/M/A8J8BCTAxoAHCAgCD0QIGApDvWAAAABpmY1RMAAAAAQAAAAQAAAAEAAAAAAAAAAAAAQAKAADMAek1AAAAGWZkQVQAAAACeJxjZPjP8J8BCTAxoAHCAgCC0gIGTHCdpgAAAABJRU5ErkJggg=="),
            (".webp","UklGRsoAAABXRUJQVlA4WAoAAAACAAAAAwAAAwAAQU5JTQYAAAAAAAAAAABBTk1GSgAAAAAAAAAAAAMAAAMAAGQAAAJWUDggMgAAADABAJ0BKgQABAABQCYloAADcAD+8ut///mwP/bz/wR6Af//0uD//pcH//S4P/SkAAAAQU5NRkwAAAAAAAAAAAADAAADAABkAAAAVlA4IDQAAAA0AQCdASoEAAQAAAAmJaAAA3AA/ukiH//3nz//ufP/+58/6M///yn7//I4//8jj/5QIAAA") })
        {
            var file=Path.Combine(root,"animation"+extension); File.WriteAllBytes(file,Convert.FromBase64String(data));
            await Throws<IOException>(()=>ImageSafety.EnsureStatic(file));
        }
    });
    await Test("portrait output keeps its original orientation and dimensions", async () =>
    {
        var m=await Video("portrait.mov",180,320,false);
        var r=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None);
        var check=await engine.ProbeAsync(r.Output,CancellationToken.None); Check(check.Width==180 && check.Height==320,"portrait resized");
    });
    await Test("rotation metadata is applied without changing visible orientation", async () =>
    {
        var path=Path.Combine(root,"rotated.mov");
        var fixture=await ProcessRunner.RunAsync(engine.Ffmpeg,["-v","error","-display_rotation:v:0","90","-i",source.Path,"-c","copy",path],CancellationToken.None);
        Check(fixture.ExitCode==0,fixture.Error); var m=await engine.ProbeAsync(path,CancellationToken.None);
        var r=await engine.ProcessAsync(m,new(),new(false,null,root),null,CancellationToken.None);
        var check=await engine.ProbeAsync(r.Output,CancellationToken.None); Check(check.Width==180 && check.Height==320,"rotation lost");
    });
    await Test("actual 10-bit SDR H265 output remains 10-bit", async () =>
    {
        var path=Path.Combine(root,"ten-bit.mkv");
        var fixture=await ProcessRunner.RunAsync(engine.Ffmpeg,["-v","error","-i",source.Path,"-c:v","ffv1","-pix_fmt","yuv420p10le","-c:a","pcm_s16le",path],CancellationToken.None);
        Check(fixture.ExitCode==0,fixture.Error); var m=await engine.ProbeAsync(path,CancellationToken.None); Check(m.IsHighBitDepth,"fixture bitdepth");
        var r=await engine.ProcessAsync(m,new(){Codec="H.265"},new(false,null,root,AllowHighBitDepth:true),null,CancellationToken.None);
        var check=await engine.ProbeAsync(r.Output,CancellationToken.None); Check(check.IsHighBitDepth,"output bitdepth lost");
    });
    await Test("streaming search encodes before discovery finishes and filters profiles", async () =>
    {
        var batch=Path.Combine(root,"stream"); Directory.CreateDirectory(Path.Combine(batch,"nested"));
        File.Copy(source.Path,Path.Combine(batch,"first.mov")); File.Copy(source.Path,Path.Combine(batch,"nested","third.mov"));
        var disabled=await Video("wide-disabled.mov",1280,32,false); File.Copy(disabled.Path,Path.Combine(batch,"nested","disabled.mov"));
        var profiles=CompressionProfile.Tiers.ToDictionary(t=>t,t=>new CompressionProfile { Tier=t, Enabled=t!="720p" });
        var events=new List<BatchUpdate>();
        await new BatchProcessor(engine).RunAsync(batch,true,false,profiles,new(false,null,batch,FullVerification:false),events.Add,CancellationToken.None);
        Check(events.Count(e=>e.Kind=="Result")==2,"eligible count"); Check(events.Count(e=>e.Kind=="Skipped")==1,"disabled profile");
        Check(events.FindIndex(e=>e.Kind=="Result")<events.FindLastIndex(e=>e.Kind=="Inspecting"),"waited for complete scan");
        Check(Directory.GetFiles(batch,"*.mov",SearchOption.AllDirectories).Length==3,"source lost");
    });
    await Test("fast-copy mode uses basic validation and retains original", async () =>
    {
        var r=await engine.ProcessAsync(source,new(),new(false,null,root,FullVerification:false),null,CancellationToken.None);
        Check(r.State=="Done" && r.Detail.Contains("probe only"),"validation mode"); Check(File.Exists(source.Path),"master lost");
    });
    await Test("replacement always forces full decode even in fast mode", async () =>
    {
        var m=await Video("fast-replace.mov");
        var r=await engine.ProcessAsync(m,new(),new(true,null,root,FullVerification:false),null,CancellationToken.None);
        Check(r.Detail.Contains("full decode") && File.Exists(r.Backup),"replacement safety");
    });
    await Test("automatic hardware selection produces valid output or CPU fallback", async () =>
    {
        var r=await engine.ProcessAsync(source,new(),new(false,null,root,Acceleration:"Auto",FullVerification:false),null,CancellationToken.None);
        Check(r.State=="Done" && r.Detail.Contains("Encoder:"),"auto encoder");
        var m=await engine.ProbeAsync(r.Output,CancellationToken.None); Check(m.AudioStreams==1 && m.Width==320,"auto output");
        Console.WriteLine("AUTO: "+r.Detail);
    });
    await Test("cancelling streaming batch does not start later files", async () =>
    {
        var batch=Path.Combine(root,"cancel-stream"); Directory.CreateDirectory(batch);
        for(var i=0;i<3;i++) File.Copy(source.Path,Path.Combine(batch,i+".mov"));
        var profiles=CompressionProfile.Tiers.ToDictionary(t=>t,t=>new CompressionProfile { Tier=t });
        using var cancel=new CancellationTokenSource(); var results=0; var inspected=0;
        try
        {
            await new BatchProcessor(engine).RunAsync(batch,true,false,profiles,new(false,null,batch,FullVerification:false),e=>
            { if(e.Kind=="Inspecting") inspected++; if(e.Kind=="Result") { results++; cancel.Cancel(); } },cancel.Token);
            throw new Exception("not cancelled");
        }
        catch(OperationCanceledException) { Check(results==1 && inspected==1,"continued after cancellation"); }
        Check(Directory.GetFiles(batch,"*.mov").Length==3,"batch sources lost");
    });
    await Test("automatic choice ranks valid measured speed rather than vendor order", () =>
    {
        var samples=new[] { new EncoderMeasurement("h264_nvenc",.2,48,true,"ok"),new EncoderMeasurement("h264_qsv",.1,48,true,"ok"),new EncoderMeasurement("h264_amf",.001,48,false,"failed"),new EncoderMeasurement("libx264",.4,48,true,"ok") };
        Check(EncoderPolicy.PickFastest(samples,"libx264")=="h264_qsv","fastest working encoder not chosen");
        Check(EncoderPolicy.PickFastest([new("h264_nvenc",1,0,false,"failed")],"libx264")=="libx264","no valid fallback"); return Task.CompletedTask;
    });
    await Test("benchmark cache separates quality speed bit depth and dimensions", () =>
    {
        var p=new CompressionProfile(); var key=EncoderPolicy.CacheKey("Auto",p,false,source);
        Check(key!=EncoderPolicy.CacheKey("Auto",new(){Crf=24},false,source),"quality cache");
        Check(key!=EncoderPolicy.CacheKey("Auto",new(){Preset="slow"},false,source),"speed cache");
        Check(key!=EncoderPolicy.CacheKey("Auto",p,true,source),"bit depth cache");
        Check(key!=EncoderPolicy.CacheKey("Auto",p,false,source with { Width=1920,Height=1080 }),"resolution cache"); return Task.CompletedTask;
    });
    await Test("actual auto benchmark includes CPU and reports selected output encoder", async () =>
    {
        var measured=new List<EncoderMeasurement>(); var fresh=new MediaEngine(); fresh.EncoderMeasured+=measured.Add;
        var r=await fresh.ProcessAsync(source,new(),new(false,null,root,Acceleration:"Auto",FullVerification:false),null,CancellationToken.None);
        Check(measured.Any(m=>m.Encoder=="libx264" && m.Success),"CPU baseline missing");
        var winner=EncoderPolicy.PickFastest(measured,"libx264"); Check(r.Detail.StartsWith("Encoder: "+winner+"."),"selection not used");
    });
    await Test("after-size and saving reflect real completed output, not a sample", () =>
    {
        var row=new QueueItem(source with { Bytes=1024*1024 });
        row.RecordOutput(new(source.Path,"sample.mp4","","Sample",1024*1024,1000,1,"sample"));
        Check(row.AfterSize=="—" && row.Reduction=="—","sample shown as full output");
        row.RecordOutput(new(source.Path,"output.mp4","","Done",1024*1024,256*1024,1,"done"));
        Check(row.AfterSize=="256 KB" && row.Reduction=="75%","size/reduction calculation"); return Task.CompletedTask;
    });
    await Test("hardware inventory degrades gracefully on unsupported platforms", async () =>
    {
        var hardware=await HardwareInfo.ReadAsync(CancellationToken.None);
        Check(!string.IsNullOrWhiteSpace(hardware.CpuName) && hardware.LogicalProcessors>0 && hardware.GpuNames is not null,"hardware result");
    });
    if(args.Contains("--batch")) await Test("100-video nested batch retains every original", async () =>
    {
        var seed=await Video("batch-seed.mov",160,90,false,.3); var batch=Path.Combine(root,"batch"); Directory.CreateDirectory(batch);
        for(var i=0;i<100;i++)
        {
            var dir=Path.Combine(batch,(i/10).ToString(),"nested"); Directory.CreateDirectory(dir); File.Copy(seed.Path,Path.Combine(dir,"مقطع "+i+".mov"));
        }
        var inputs=MediaEngine.Discover(batch,true).ToArray(); Check(inputs.Length==100,"inputs"); var timer=System.Diagnostics.Stopwatch.StartNew(); var done=0;
        foreach(var file in inputs)
        {
            var media=await engine.ProbeAsync(file,CancellationToken.None); var r=await engine.ProcessAsync(media,new(),new(false,null,batch),null,CancellationToken.None);
            Check(r.State=="Done","batch result: "+r.State); Check(File.Exists(file),"source missing");done++;
        }
        Check(done==100 && Directory.GetFiles(batch,"*.mov",SearchOption.AllDirectories).Length==100,"batch count");
        Console.WriteLine($"BATCH: {done} short synthetic videos; {timer.Elapsed.TotalSeconds:0.00} seconds; 100 originals retained.");
    });
    await Test("CSV fields quoted and spreadsheet formula escaped", () =>
    {
        var path=Path.Combine(root,"report.csv"); MediaEngine.ExportCsv([new("=HYPERLINK(\"x\")","a,b","","Error",1,1,0,"line\nbreak")],path);
        var text=File.ReadAllText(path); Check(text.Contains("'=HYPERLINK") && text.Contains("\"a,b\""),"unsafe CSV"); return Task.CompletedTask;
    });
    await Test("ten complete language dictionaries and well-formed XAML", () =>
    {
        var repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var folder=Path.Combine(repo,"src","FramePress.App","Assets","Languages");
        var languages=Directory.GetFiles(folder,"*.json"); Check(languages.Length==10,"language count");
        var reference=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"en.json")))!;
        foreach(var file in languages)
        {
            var values=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file))!;
            Check(values.Keys.Order().SequenceEqual(reference.Keys.Order()),"keys: "+file); Check(values.Values.All(v=>!string.IsNullOrWhiteSpace(v)),"empty translation");
        }
        foreach(var file in Directory.GetFiles(Path.Combine(repo,"src","FramePress.App"),"*.xaml")) System.Xml.Linq.XDocument.Load(file);
        return Task.CompletedTask;
    });
}
finally { try { Directory.Delete(root,true); } catch(IOException) { } }
Console.WriteLine($"\n{passed.Count} passed, {failures.Count} failed");
if (failures.Count>0) Environment.ExitCode=1;
