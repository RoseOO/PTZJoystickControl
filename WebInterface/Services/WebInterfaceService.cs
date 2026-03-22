using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PtzJoystickControl.WebInterface.Services;

public class WebInterfaceService : IDisposable
{
    private readonly ICamerasService _camerasService;
    private readonly IGamepadsService _gamepadsService;
    private WebApplication? _app;
    private CancellationTokenSource? _cts;

    public int Port { get; }
    public bool IsRunning { get; private set; }

    public WebInterfaceService(ICamerasService camerasService, IGamepadsService gamepadsService, int port = 5000)
    {
        _camerasService = camerasService;
        _gamepadsService = gamepadsService;
        Port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls($"http://*:{Port}");
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Any, Port);
                });

                // Suppress excessive ASP.NET Core logging
                builder.Logging.SetMinimumLevel(LogLevel.Warning);

                _app = builder.Build();

                MapRoutes(_app);

                Debug.WriteLine($"[WebInterface] Starting web server on port {Port}");
                IsRunning = true;

                await _app.RunAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebInterface] Error: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
            }
        });
    }

    public void Stop()
    {
        try
        {
            _app?.StopAsync().GetAwaiter().GetResult();
        }
        catch { }
        _cts?.Cancel();
        IsRunning = false;
    }

    private void MapRoutes(WebApplication app)
    {
        // Serve embedded HTML at root
        app.MapGet("/", (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            var html = GetEmbeddedResource("wwwroot.index.html");
            return ctx.Response.WriteAsync(html ?? GetFallbackHtml());
        });

        // === Camera API ===

        // GET /api/cameras - list all cameras
        app.MapGet("/api/cameras", () =>
        {
            var cameras = _camerasService.Cameras.Select((c, i) => new
            {
                Index = i,
                c.Name,
                c.Connected,
                c.PollingEnabled,
                ZoomPosition = c.ZoomPosition,
                PanPosition = c.PanPosition,
                TiltPosition = c.TiltPosition,
                FocusPosition = c.FocusPosition,
                FocusMode = c.FocusModeState?.ToString(),
                ExposureMode = c.ExposureModeState?.ToString(),
                WhiteBalanceMode = c.WhiteBalanceModeState?.ToString(),
                Power = c.PowerState?.ToString(),
            });
            return Results.Json(cameras, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        });

        // POST /api/cameras/{index}/pan
        app.MapPost("/api/cameras/{index:int}/pan", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var speed = body.GetProperty("speed").GetByte();
            var dir = Enum.Parse<PanDir>(body.GetProperty("direction").GetString()!);
            camera.Pan(speed, dir);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/tilt
        app.MapPost("/api/cameras/{index:int}/tilt", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var speed = body.GetProperty("speed").GetByte();
            var dir = Enum.Parse<TiltDir>(body.GetProperty("direction").GetString()!);
            camera.Tilt(speed, dir);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/pantilt
        app.MapPost("/api/cameras/{index:int}/pantilt", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var panSpeed = body.GetProperty("panSpeed").GetByte();
            var tiltSpeed = body.GetProperty("tiltSpeed").GetByte();
            var panDir = Enum.Parse<PanDir>(body.GetProperty("panDirection").GetString()!);
            var tiltDir = Enum.Parse<TiltDir>(body.GetProperty("tiltDirection").GetString()!);
            camera.PanTilt(panSpeed, tiltSpeed, panDir, tiltDir);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/zoom
        app.MapPost("/api/cameras/{index:int}/zoom", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var speed = body.GetProperty("speed").GetByte();
            var dir = Enum.Parse<ZoomDir>(body.GetProperty("direction").GetString()!);
            camera.Zoom(speed, dir);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/focus
        app.MapPost("/api/cameras/{index:int}/focus", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var speed = body.GetProperty("speed").GetByte();
            var dir = Enum.Parse<FocusDir>(body.GetProperty("direction").GetString()!);
            camera.Focus(speed, dir);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/stop
        app.MapPost("/api/cameras/{index:int}/stop", (int index) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            camera.PanTilt(0, 0, PanDir.Stop, TiltDir.Stop);
            camera.Zoom(0, ZoomDir.Stop);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/preset
        app.MapPost("/api/cameras/{index:int}/preset", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var action = Enum.Parse<Preset>(body.GetProperty("action").GetString()!);
            var number = body.GetProperty("number").GetByte();
            camera.Preset(action, number);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/power
        app.MapPost("/api/cameras/{index:int}/power", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var state = Enum.Parse<Power>(body.GetProperty("state").GetString()!);
            camera.Power(state);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/focusmode
        app.MapPost("/api/cameras/{index:int}/focusmode", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var mode = Enum.Parse<FocusMode>(body.GetProperty("mode").GetString()!);
            camera.FocusMode(mode);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/exposure
        app.MapPost("/api/cameras/{index:int}/exposure", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var mode = Enum.Parse<ExposureMode>(body.GetProperty("mode").GetString()!);
            camera.SetExposureMode(mode);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/whitebalance
        app.MapPost("/api/cameras/{index:int}/whitebalance", async (int index, HttpContext ctx) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            var body = await ReadBody(ctx);
            var mode = Enum.Parse<WhiteBalanceMode>(body.GetProperty("mode").GetString()!);
            camera.SetWhiteBalanceMode(mode);
            return Results.Ok();
        });

        // === Gamepads API ===

        app.MapGet("/api/gamepads", () =>
        {
            var gamepads = _gamepadsService.Gamepads.Select(g => new
            {
                g.Id,
                g.Name,
                g.IsConnected,
                g.IsActivated,
            });
            return Results.Json(gamepads, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        });
    }

    private ViscaDeviceBase? GetCamera(int index)
    {
        if (index < 0 || index >= _camerasService.Cameras.Count) return null;
        return _camerasService.Cameras[index];
    }

    private static async Task<JsonElement> ReadBody(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string? GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName));
        if (fullName == null) return null;

        using var stream = assembly.GetManifestResourceStream(fullName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GetFallbackHtml()
    {
        return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>PTZ Joystick Control - Web Interface</title>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#1e1e1e;color:#e0e0e0;padding:16px}
h1{font-size:1.2rem;margin-bottom:12px;color:#4488cc}
h2{font-size:1rem;margin:12px 0 8px;color:#88bbee}
.cameras{display:flex;flex-wrap:wrap;gap:12px}
.camera-card{background:#2a2a2a;border:1px solid #444;border-radius:8px;padding:12px;min-width:280px;flex:1}
.camera-card h3{margin-bottom:8px;color:#66aadd}
.status{font-size:0.85rem;color:#888;margin-bottom:8px}
.btn-grid{display:grid;grid-template-columns:repeat(3,40px);gap:2px;margin:8px 0}
.btn-grid button,.ctrl button{width:40px;height:34px;background:#3a3a3a;color:#e0e0e0;border:1px solid #555;border-radius:4px;cursor:pointer;font-size:14px}
.btn-grid button:hover,.ctrl button:hover{background:#4a4a4a}
.btn-grid button:active,.ctrl button:active{background:#5588bb}
.ctrl{display:flex;gap:4px;margin:4px 0;align-items:center}
.ctrl label{font-size:0.8rem;min-width:50px}
.presets{display:flex;flex-wrap:wrap;gap:2px;margin:4px 0}
.presets button{width:32px;height:28px;font-size:0.8rem}
select{background:#333;color:#e0e0e0;border:1px solid #555;border-radius:4px;padding:4px 8px}
.zoom-ctrl{display:flex;gap:4px;margin:4px 0}
.connected{color:#4caf50}.disconnected{color:#f44336}
</style>
</head><body>
<h1>PTZ Joystick Control</h1>
<div id='app'><p>Loading cameras...</p></div>
<script>
const API='';
async function api(path,body){
  const opts={method:'POST',headers:{'Content-Type':'application/json'}};
  if(body)opts.body=JSON.stringify(body);
  try{return await fetch(API+path,opts)}catch(e){console.error(e)}
}
async function loadCameras(){
  try{
    const r=await fetch(API+'/api/cameras');
    const cameras=await r.json();
    const app=document.getElementById('app');
    if(!cameras.length){app.innerHTML='<p>No cameras configured</p>';return}
    app.innerHTML='<div class=""cameras"">'+cameras.map((c,i)=>cameraCard(c,i)).join('')+'</div>';
    setupHandlers();
  }catch(e){document.getElementById('app').innerHTML='<p>Error loading cameras: '+e.message+'</p>'}
}
function cameraCard(c,i){
  return`<div class=""camera-card"" data-idx=""${i}"">
    <h3>${c.name||'Camera '+(i+1)}</h3>
    <div class=""status""><span class=""${c.connected?'connected':'disconnected'}"">${c.connected?'Connected':'Disconnected'}</span></div>
    <h2>Movement</h2>
    <div class=""btn-grid"">
      <button data-cmd=""pantilt"" data-pd=""Left"" data-td=""Up"">↖</button>
      <button data-cmd=""tilt"" data-d=""Up"">↑</button>
      <button data-cmd=""pantilt"" data-pd=""Right"" data-td=""Up"">↗</button>
      <button data-cmd=""pan"" data-d=""Left"">←</button>
      <button data-cmd=""stop"">■</button>
      <button data-cmd=""pan"" data-d=""Right"">→</button>
      <button data-cmd=""pantilt"" data-pd=""Left"" data-td=""Down"">↙</button>
      <button data-cmd=""tilt"" data-d=""Down"">↓</button>
      <button data-cmd=""pantilt"" data-pd=""Right"" data-td=""Down"">↘</button>
    </div>
    <h2>Zoom</h2>
    <div class=""zoom-ctrl"">
      <button data-cmd=""zoom"" data-d=""Tele"">Z+</button>
      <button data-cmd=""zstop"">Z■</button>
      <button data-cmd=""zoom"" data-d=""Wide"">Z-</button>
    </div>
    <h2>Focus</h2>
    <div class=""ctrl"">
      <button data-cmd=""focus"" data-d=""Far"">Far</button>
      <button data-cmd=""fstop"">Stop</button>
      <button data-cmd=""focus"" data-d=""Near"">Near</button>
      <button data-cmd=""focusmode"" data-m=""Auto"">Auto</button>
      <button data-cmd=""focusmode"" data-m=""Manual"">Man</button>
    </div>
    <h2>Presets</h2>
    <div class=""presets"">${[1,2,3,4,5,6,7,8].map(n=>`<button data-cmd=""preset"" data-n=""${n}"">${n}</button>`).join('')}</div>
    <h2>Power</h2>
    <div class=""ctrl"">
      <button data-cmd=""power"" data-s=""On"">On</button>
      <button data-cmd=""power"" data-s=""Off"">Off</button>
    </div>
  </div>`;
}
function setupHandlers(){
  document.querySelectorAll('.camera-card').forEach(card=>{
    const idx=parseInt(card.dataset.idx);
    card.querySelectorAll('button').forEach(btn=>{
      const cmd=btn.dataset.cmd;
      // For movement buttons, send on press and stop on release
      if(['pan','tilt','pantilt'].includes(cmd)){
        btn.addEventListener('pointerdown',()=>sendCmd(idx,btn));
        btn.addEventListener('pointerup',()=>api(`/api/cameras/${idx}/stop`));
        btn.addEventListener('pointerleave',()=>api(`/api/cameras/${idx}/stop`));
      }else if(cmd==='zoom'){
        btn.addEventListener('pointerdown',()=>sendCmd(idx,btn));
        btn.addEventListener('pointerup',()=>api(`/api/cameras/${idx}/zoom`,{speed:0,direction:'Stop'}));
        btn.addEventListener('pointerleave',()=>api(`/api/cameras/${idx}/zoom`,{speed:0,direction:'Stop'}));
      }else if(cmd==='focus'){
        btn.addEventListener('pointerdown',()=>sendCmd(idx,btn));
        btn.addEventListener('pointerup',()=>api(`/api/cameras/${idx}/focus`,{speed:0,direction:'Stop'}));
        btn.addEventListener('pointerleave',()=>api(`/api/cameras/${idx}/focus`,{speed:0,direction:'Stop'}));
      }else{
        btn.addEventListener('click',()=>sendCmd(idx,btn));
      }
    });
  });
}
function sendCmd(idx,btn){
  const cmd=btn.dataset.cmd;
  const base=`/api/cameras/${idx}`;
  switch(cmd){
    case'pan':api(`${base}/pan`,{speed:12,direction:btn.dataset.d});break;
    case'tilt':api(`${base}/tilt`,{speed:10,direction:btn.dataset.d});break;
    case'pantilt':api(`${base}/pantilt`,{panSpeed:12,tiltSpeed:10,panDirection:btn.dataset.pd,tiltDirection:btn.dataset.td});break;
    case'stop':api(`${base}/stop`);break;
    case'zoom':api(`${base}/zoom`,{speed:4,direction:btn.dataset.d});break;
    case'zstop':api(`${base}/zoom`,{speed:0,direction:'Stop'});break;
    case'focus':api(`${base}/focus`,{speed:4,direction:btn.dataset.d});break;
    case'fstop':api(`${base}/focus`,{speed:0,direction:'Stop'});break;
    case'focusmode':api(`${base}/focusmode`,{mode:btn.dataset.m});break;
    case'preset':api(`${base}/preset`,{action:'Recall',number:parseInt(btn.dataset.n)});break;
    case'power':api(`${base}/power`,{state:btn.dataset.s});break;
  }
}
loadCameras();
setInterval(loadCameras,10000);
</script>
</body></html>";
    }

    public void Dispose()
    {
        Stop();
    }
}
