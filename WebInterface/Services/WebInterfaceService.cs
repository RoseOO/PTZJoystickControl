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

        // POST /api/cameras/{index}/iris
        app.MapPost("/api/cameras/{index:int}/iris", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<IrisDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustIris(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/shutter
        app.MapPost("/api/cameras/{index:int}/shutter", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<ShutterDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustShutter(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/gain
        app.MapPost("/api/cameras/{index:int}/gain", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<GainDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustGain(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/aperture
        app.MapPost("/api/cameras/{index:int}/aperture", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<ApertureDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustAperture(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/backlight
        app.MapPost("/api/cameras/{index:int}/backlight", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var mode = Enum.Parse<BacklightCompensation>(body.GetProperty("mode").GetString()!);
                camera.SetBacklightCompensation(mode);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/redgain
        app.MapPost("/api/cameras/{index:int}/redgain", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<GainDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustRedGain(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/bluegain
        app.MapPost("/api/cameras/{index:int}/bluegain", async (int index, HttpContext ctx) =>
            await CameraOperation(index, ctx, (camera, body) =>
            {
                var dir = Enum.Parse<GainDir>(body.GetProperty("direction").GetString()!);
                camera.AdjustBlueGain(dir);
                return Results.Ok();
            }));

        // POST /api/cameras/{index}/wbtrigger
        app.MapPost("/api/cameras/{index:int}/wbtrigger", (int index) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            camera.TriggerWhiteBalance();
            return Results.Ok();
        });

        // POST /api/cameras/{index}/focusstop
        app.MapPost("/api/cameras/{index:int}/focusstop", (int index) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            camera.Focus(0, FocusDir.Stop);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/zoomstop
        app.MapPost("/api/cameras/{index:int}/zoomstop", (int index) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            camera.Zoom(0, ZoomDir.Stop);
            return Results.Ok();
        });

        // POST /api/cameras/{index}/movestop
        app.MapPost("/api/cameras/{index:int}/movestop", (int index) =>
        {
            var camera = GetCamera(index);
            if (camera == null) return Results.NotFound("Camera not found");
            camera.PanTilt(0, 0, PanDir.Stop, TiltDir.Stop);
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

        // === API Documentation ===

        app.MapGet("/api/docs", () =>
        {
            var docs = new[]
            {
                new { Method = "GET", Path = "/api/cameras", Description = "List all cameras with status", Body = (object?)null },
                new { Method = "POST", Path = "/api/cameras/{index}/pan", Description = "Pan camera", Body = (object?)new { speed = "byte (0-24)", direction = "Left|Right|Stop" } },
                new { Method = "POST", Path = "/api/cameras/{index}/tilt", Description = "Tilt camera", Body = (object?)new { speed = "byte (0-24)", direction = "Up|Down|Stop" } },
                new { Method = "POST", Path = "/api/cameras/{index}/pantilt", Description = "Pan and tilt simultaneously", Body = (object?)new { panSpeed = "byte (0-24)", tiltSpeed = "byte (0-24)", panDirection = "Left|Right|Stop", tiltDirection = "Up|Down|Stop" } },
                new { Method = "POST", Path = "/api/cameras/{index}/zoom", Description = "Zoom camera", Body = (object?)new { speed = "byte (0-7)", direction = "Tele|Wide|Stop" } },
                new { Method = "POST", Path = "/api/cameras/{index}/focus", Description = "Focus camera", Body = (object?)new { speed = "byte (0-7)", direction = "Far|Near|Stop" } },
                new { Method = "POST", Path = "/api/cameras/{index}/stop", Description = "Stop all pan/tilt/zoom movement", Body = (object?)null },
                new { Method = "POST", Path = "/api/cameras/{index}/preset", Description = "Recall or set a preset", Body = (object?)new { action = "Recall|Set", number = "byte (0-255)" } },
                new { Method = "POST", Path = "/api/cameras/{index}/power", Description = "Power camera on or off", Body = (object?)new { state = "On|Off" } },
                new { Method = "POST", Path = "/api/cameras/{index}/focusmode", Description = "Set focus mode", Body = (object?)new { mode = "Auto|Manual|Toggle" } },
                new { Method = "POST", Path = "/api/cameras/{index}/exposure", Description = "Set exposure mode", Body = (object?)new { mode = "Auto|Manual|ShutterPriority|IrisPriority|Bright" } },
                new { Method = "POST", Path = "/api/cameras/{index}/whitebalance", Description = "Set white balance mode", Body = (object?)new { mode = "Auto|Indoor|Outdoor|OnePush|Manual" } },
                new { Method = "POST", Path = "/api/cameras/{index}/iris", Description = "Adjust iris", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/shutter", Description = "Adjust shutter speed", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/gain", Description = "Adjust gain", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/aperture", Description = "Adjust aperture", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/backlight", Description = "Set backlight compensation", Body = (object?)new { mode = "On|Off" } },
                new { Method = "POST", Path = "/api/cameras/{index}/redgain", Description = "Adjust red gain", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/bluegain", Description = "Adjust blue gain", Body = (object?)new { direction = "Up|Down|Reset" } },
                new { Method = "POST", Path = "/api/cameras/{index}/wbtrigger", Description = "Trigger one-push white balance", Body = (object?)null },
                new { Method = "POST", Path = "/api/cameras/{index}/focusstop", Description = "Stop focus movement", Body = (object?)null },
                new { Method = "POST", Path = "/api/cameras/{index}/zoomstop", Description = "Stop zoom movement", Body = (object?)null },
                new { Method = "POST", Path = "/api/cameras/{index}/movestop", Description = "Stop pan/tilt movement", Body = (object?)null },
                new { Method = "GET", Path = "/api/gamepads", Description = "List all connected gamepads", Body = (object?)null },
                new { Method = "GET", Path = "/api/docs", Description = "This API documentation", Body = (object?)null },
            };
            return Results.Json(docs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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

    /// <summary>
    /// Wraps a camera API operation with standard error handling.
    /// Returns 404 if camera not found, 400 if request body is invalid.
    /// </summary>
    private async Task<IResult> CameraOperation(int index, HttpContext ctx, Func<ViscaDeviceBase, JsonElement, IResult> action)
    {
        var camera = GetCamera(index);
        if (camera == null) return Results.NotFound("Camera not found");
        try
        {
            var body = await ReadBody(ctx);
            return action(camera, body);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or JsonException or ArgumentException or FormatException)
        {
            return Results.BadRequest($"Invalid request: {ex.Message}");
        }
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
<html lang='en'><head><meta charset='utf-8'><title>PTZ Joystick Control</title>
<meta name='viewport' content='width=device-width, initial-scale=1, user-scalable=no'>
<style>
:root{
  --bg:#0f1117;--bg2:#1a1d2e;--bg3:#242840;--bg4:#2e3354;
  --text:#e0e4f0;--text2:#8890a8;--accent:#4a8fd4;--accent2:#6aafff;
  --success:#4caf50;--danger:#ef5350;--warn:#ffb74d;--border:#2e3354;--radius:8px;
}
*{box-sizing:border-box;margin:0;padding:0;-webkit-tap-highlight-color:transparent}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:var(--bg);color:var(--text);overflow-x:hidden}
header{background:var(--bg2);border-bottom:1px solid var(--border);padding:12px 16px;display:flex;align-items:center;justify-content:space-between;position:sticky;top:0;z-index:100}
header h1{font-size:1.1rem;color:var(--accent2);white-space:nowrap}
.cam-sel{display:flex;align-items:center;gap:8px}
.cam-sel select{background:var(--bg3);color:var(--text);border:1px solid var(--border);border-radius:var(--radius);padding:6px 12px;font-size:0.9rem}
.status-dot{width:10px;height:10px;border-radius:50%;display:inline-block}
.status-dot.on{background:var(--success)}.status-dot.off{background:var(--danger)}
main{padding:12px;max-width:900px;margin:0 auto}
.status-bar{background:var(--bg2);border:1px solid var(--border);border-radius:var(--radius);padding:10px 14px;margin-bottom:12px;display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));gap:6px 16px;font-size:0.82rem}
.status-bar .si{display:flex;justify-content:space-between}.status-bar .sl{color:var(--text2)}.status-bar .sv{color:var(--accent2);font-family:monospace}
.section{background:var(--bg2);border:1px solid var(--border);border-radius:var(--radius);margin-bottom:10px;overflow:hidden}
.section-hdr{display:flex;align-items:center;justify-content:space-between;padding:10px 14px;cursor:pointer;user-select:none;background:var(--bg3)}
.section-hdr h2{font-size:0.95rem;color:var(--accent2)}.section-hdr .arrow{transition:transform .2s;color:var(--text2)}
.section.collapsed .section-body{display:none}.section.collapsed .arrow{transform:rotate(-90deg)}
.section-body{padding:12px 14px}
.row{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:8px}
.row:last-child{margin-bottom:0}
.lbl{font-size:0.8rem;color:var(--text2);min-width:60px}
button{background:var(--bg4);color:var(--text);border:1px solid var(--border);border-radius:6px;padding:8px 12px;cursor:pointer;font-size:0.85rem;transition:background .15s;touch-action:manipulation}
button:hover{background:#3a4068}button:active,.btn-active{background:var(--accent)!important;color:#fff}
button.sm{padding:5px 8px;font-size:0.78rem;min-width:32px}
button.mode-btn{padding:6px 14px}
button.mode-btn.active{background:var(--accent);color:#fff;border-color:var(--accent)}
button.danger{border-color:var(--danger)}button.danger:hover{background:var(--danger);color:#fff}
button.success{border-color:var(--success)}button.success:hover{background:var(--success);color:#fff}
select.ctrl-sel{background:var(--bg4);color:var(--text);border:1px solid var(--border);border-radius:6px;padding:6px 10px;font-size:0.85rem}
input[type=range]{-webkit-appearance:none;appearance:none;background:var(--bg);border-radius:4px;height:6px;outline:none;flex:1;min-width:80px;max-width:200px}
input[type=range]::-webkit-slider-thumb{-webkit-appearance:none;width:18px;height:18px;border-radius:50%;background:var(--accent);cursor:pointer;border:2px solid var(--bg2)}
.range-val{font-family:monospace;font-size:0.8rem;color:var(--accent2);min-width:28px;text-align:center}
.dpad{display:grid;grid-template-columns:repeat(3,48px);grid-template-rows:repeat(3,48px);gap:3px}
.dpad button{width:48px;height:48px;font-size:1.1rem;padding:0;display:flex;align-items:center;justify-content:center}
.joy-wrap{position:relative;width:160px;height:160px;margin:0 auto}
.joy-base{width:160px;height:160px;border-radius:50%;background:var(--bg);border:2px solid var(--border);position:relative;touch-action:none}
.joy-knob{width:50px;height:50px;border-radius:50%;background:var(--accent);position:absolute;top:55px;left:55px;pointer-events:none;transition:none;opacity:0.85}
.presets-grid{display:grid;grid-template-columns:repeat(9,1fr);gap:4px}
.presets-grid button{padding:6px 0;text-align:center;font-size:0.82rem}
.presets-grid .set-btn{background:#2a2040;border-color:#5a4080}
.presets-grid .set-btn:hover{background:#4a3070}
.adj-group{display:flex;gap:4px;align-items:center}
.adj-group .adj-lbl{font-size:0.8rem;color:var(--text2);min-width:55px}
#no-cameras{text-align:center;padding:60px 20px;color:var(--text2)}
@media(max-width:600px){
  .dpad{grid-template-columns:repeat(3,44px);grid-template-rows:repeat(3,44px)}.dpad button{width:44px;height:44px;font-size:1rem}
  .presets-grid{grid-template-columns:repeat(5,1fr)}
  .status-bar{grid-template-columns:repeat(auto-fill,minmax(120px,1fr))}
  header{flex-direction:column;gap:8px}
}
</style></head><body>
<header>
  <h1>&#127909; PTZ Joystick Control</h1>
  <div class='cam-sel'>
    <span class='status-dot off' id='conn-dot'></span>
    <select id='cam-select'><option value='-1'>Loading...</option></select>
  </div>
</header>
<main id='app'><div id='no-cameras'><p>Loading cameras...</p></div></main>
<script>
'use strict';
const state={camIdx:0,cameras:[],panSpeed:12,tiltSpeed:10,zoomSpeed:4,focusSpeed:4,joyActive:false,sections:{}};
function ls(k,v){if(v!==undefined){localStorage.setItem('ptz_'+k,JSON.stringify(v));return v}try{return JSON.parse(localStorage.getItem('ptz_'+k))}catch{return null}}
async function api(path,body){
  const o={method:'POST',headers:{'Content-Type':'application/json'}};
  if(body)o.body=JSON.stringify(body);
  try{return await fetch(path,o)}catch(e){console.error(e)}
}
function $(s,p){return(p||document).querySelector(s)}
function $$(s,p){return(p||document).querySelectorAll(s)}

async function loadCameras(){
  try{
    const r=await fetch('/api/cameras');const data=await r.json();
    state.cameras=data;
    const sel=$('#cam-select');
    if(!data.length){$('#app').innerHTML='<div id=\""no-cameras\""><p>No cameras configured.</p><p style=\""margin-top:8px;font-size:0.85rem\"">Connect cameras in the desktop application.</p></div>';sel.innerHTML='<option value=\""-1\"">No cameras</option>';return}
    const prev=sel.value;
    sel.innerHTML=data.map((c,i)=>'<option value=\""'+i+'\"">'+(c.name||'Camera '+(i+1))+'</option>').join('');
    if(prev>=0&&prev<data.length)sel.value=prev;
    state.camIdx=parseInt(sel.value);
    updateStatus();
    if(!$('.section'))buildUI();
  }catch(e){$('#app').innerHTML='<div id=\""no-cameras\""><p>Error: '+e.message+'</p></div>'}
}

function updateStatus(){
  const c=state.cameras[state.camIdx];if(!c)return;
  const dot=$('#conn-dot');dot.className='status-dot '+(c.connected?'on':'off');
  const items=[['Connection',c.connected?'Connected':'Disconnected'],['Power',c.power||'—'],['Pan Pos',c.panPosition!=null?c.panPosition:'—'],['Tilt Pos',c.tiltPosition!=null?c.tiltPosition:'—'],['Zoom Pos',c.zoomPosition!=null?c.zoomPosition:'—'],['Focus Pos',c.focusPosition!=null?c.focusPosition:'—'],['Focus Mode',c.focusMode||'—'],['Exposure',c.exposureMode||'—'],['White Bal',c.whiteBalanceMode||'—']];
  const sb=$('#status-bar');
  if(sb)sb.innerHTML=items.map(x=>'<div class=\""si\""><span class=\""sl\"">'+x[0]+'</span><span class=\""sv\"">'+x[1]+'</span></div>').join('');
}

function buildUI(){
  const app=$('#app');
  app.innerHTML='<div class=\""status-bar\"" id=\""status-bar\""></div>'+
    buildSection('movement','Movement',buildMovement())+
    buildSection('zoom','Zoom',buildZoom())+
    buildSection('focus','Focus',buildFocus())+
    buildSection('presets','Presets',buildPresets())+
    buildSection('exposure','Exposure',buildExposure())+
    buildSection('whitebalance','White Balance',buildWhiteBalance())+
    buildSection('other','Other',buildOther());
  updateStatus();
  setupHandlers();
  restoreSections();
}

function buildSection(id,title,content){
  return '<div class=\""section\"" id=\""sec-'+id+'\""><div class=\""section-hdr\"" data-sec=\""'+id+'\""><h2>'+title+'</h2><span class=\""arrow\"">&#9660;</span></div><div class=\""section-body\"">'+content+'</div></div>';
}

function buildMovement(){
  return '<div class=\""row\"" style=\""gap:24px;align-items:flex-start;flex-wrap:wrap\"">'+
    '<div><div class=\""dpad\"">'+
    '<button data-mv=\""pantilt\"" data-pd=\""Left\"" data-td=\""Up\"">&#8598;</button>'+
    '<button data-mv=\""tilt\"" data-td=\""Up\"">&#8593;</button>'+
    '<button data-mv=\""pantilt\"" data-pd=\""Right\"" data-td=\""Up\"">&#8599;</button>'+
    '<button data-mv=\""pan\"" data-pd=\""Left\"">&#8592;</button>'+
    '<button data-mv=\""stop\"" style=\""background:var(--danger);color:#fff\"">&#9632;</button>'+
    '<button data-mv=\""pan\"" data-pd=\""Right\"">&#8594;</button>'+
    '<button data-mv=\""pantilt\"" data-pd=\""Left\"" data-td=\""Down\"">&#8601;</button>'+
    '<button data-mv=\""tilt\"" data-td=\""Down\"">&#8595;</button>'+
    '<button data-mv=\""pantilt\"" data-pd=\""Right\"" data-td=\""Down\"">&#8600;</button>'+
    '</div></div>'+
    '<div class=\""joy-wrap\""><div class=\""joy-base\"" id=\""joystick\""><div class=\""joy-knob\"" id=\""joy-knob\""></div></div></div>'+
    '</div>'+
    '<div class=\""row\""><span class=\""lbl\"">Pan Spd</span><input type=\""range\"" min=\""1\"" max=\""24\"" value=\""'+state.panSpeed+'\"" id=\""pan-speed\""><span class=\""range-val\"" id=\""pan-speed-val\"">'+state.panSpeed+'</span></div>'+
    '<div class=\""row\""><span class=\""lbl\"">Tilt Spd</span><input type=\""range\"" min=\""1\"" max=\""24\"" value=\""'+state.tiltSpeed+'\"" id=\""tilt-speed\""><span class=\""range-val\"" id=\""tilt-speed-val\"">'+state.tiltSpeed+'</span></div>';
}

function buildZoom(){
  return '<div class=\""row\""><button data-zm=\""Tele\"">&#128269;+ Zoom In</button><button data-zm=\""stop\"">&#9632; Stop</button><button data-zm=\""Wide\"">&#128269;- Zoom Out</button></div>'+
    '<div class=\""row\""><span class=\""lbl\"">Speed</span><input type=\""range\"" min=\""1\"" max=\""7\"" value=\""'+state.zoomSpeed+'\"" id=\""zoom-speed\""><span class=\""range-val\"" id=\""zoom-speed-val\"">'+state.zoomSpeed+'</span></div>';
}

function buildFocus(){
  return '<div class=\""row\""><button data-fc=\""Far\"">Far</button><button data-fc=\""stop\"">&#9632; Stop</button><button data-fc=\""Near\"">Near</button></div>'+
    '<div class=\""row\""><span class=\""lbl\"">Speed</span><input type=\""range\"" min=\""1\"" max=\""7\"" value=\""'+state.focusSpeed+'\"" id=\""focus-speed\""><span class=\""range-val\"" id=\""focus-speed-val\"">'+state.focusSpeed+'</span></div>'+
    '<div class=\""row\""><span class=\""lbl\"">Mode</span><button class=\""mode-btn\"" data-fm=\""Auto\"">Auto</button><button class=\""mode-btn\"" data-fm=\""Manual\"">Manual</button><button class=\""mode-btn\"" data-fm=\""Toggle\"">Toggle</button></div>';
}

function buildPresets(){
  let h='<div style=\""margin-bottom:6px;font-size:0.8rem;color:var(--text2)\"">Recall</div><div class=\""presets-grid\"">';
  for(let i=1;i<=9;i++)h+='<button data-pr=\""'+i+'\"">'+i+'</button>';
  h+='</div><div style=\""margin:8px 0 6px;font-size:0.8rem;color:var(--text2)\"">Set</div><div class=\""presets-grid\"">';
  for(let i=1;i<=9;i++)h+='<button class=\""set-btn\"" data-ps=\""'+i+'\"">S'+i+'</button>';
  h+='</div>';return h;
}

function buildExposure(){
  return '<div class=\""row\""><span class=\""lbl\"">Mode</span><select class=\""ctrl-sel\"" id=\""exp-mode\""><option>Auto</option><option>Manual</option><option>ShutterPriority</option><option>IrisPriority</option><option>Bright</option></select><button id=\""exp-mode-btn\"">Set</button></div>'+
    adjRow('Iris','iris')+adjRow('Shutter','shutter','Faster','Slower')+adjRow('Gain','gain');
}

function buildWhiteBalance(){
  return '<div class=\""row\""><span class=\""lbl\"">Mode</span><select class=\""ctrl-sel\"" id=\""wb-mode\""><option>Auto</option><option>Indoor</option><option>Outdoor</option><option>OnePush</option><option>Manual</option></select><button id=\""wb-mode-btn\"">Set</button></div>'+
    adjRow('Red Gain','redgain')+adjRow('Blue Gain','bluegain')+
    '<div class=\""row\""><button id=\""wb-trigger\"">&#128260; WB Trigger</button></div>';
}

function buildOther(){
  return '<div class=\""row\""><span class=\""lbl\"">Backlight</span><button class=\""success\"" data-bl=\""On\"">On</button><button class=\""danger\"" data-bl=\""Off\"">Off</button></div>'+
    adjRow('Aperture','aperture')+
    '<div class=\""row\"" style=\""margin-top:12px\""><span class=\""lbl\"">Power</span><button class=\""success\"" data-pw=\""On\"">&#9889; On</button><button class=\""danger\"" data-pw=\""Off\"">&#9724; Off</button></div>';
}

function adjRow(label,cmd,upLbl,downLbl){
  return '<div class=\""row\""><div class=\""adj-group\""><span class=\""adj-lbl\"">'+label+'</span><button class=\""sm\"" data-adj=\""'+cmd+'\"" data-dir=\""Up\"">'+(upLbl||'&#9650;')+'</button><button class=\""sm\"" data-adj=\""'+cmd+'\"" data-dir=\""Down\"">'+(downLbl||'&#9660;')+'</button><button class=\""sm\"" data-adj=\""'+cmd+'\"" data-dir=\""Reset\"">R</button></div></div>';
}

function base(){return '/api/cameras/'+state.camIdx}

function setupHandlers(){
  // Camera selector
  $('#cam-select').onchange=function(){state.camIdx=parseInt(this.value);updateStatus()};

  // Section collapse
  $$('.section-hdr').forEach(h=>h.onclick=function(){
    const sec=this.parentElement;sec.classList.toggle('collapsed');
    const id=this.dataset.sec;state.sections[id]=sec.classList.contains('collapsed');ls('sections',state.sections);
  });

  // Speed sliders
  setupSlider('pan-speed',v=>{state.panSpeed=v;ls('panSpeed',v)});
  setupSlider('tilt-speed',v=>{state.tiltSpeed=v;ls('tiltSpeed',v)});
  setupSlider('zoom-speed',v=>{state.zoomSpeed=v;ls('zoomSpeed',v)});
  setupSlider('focus-speed',v=>{state.focusSpeed=v;ls('focusSpeed',v)});

  // D-pad movement (press-and-hold)
  $$('[data-mv]').forEach(btn=>{
    const t=btn.dataset.mv;
    if(t==='stop'){btn.onclick=()=>api(base()+'/movestop');return}
    btn.addEventListener('pointerdown',e=>{e.preventDefault();btn.classList.add('btn-active');sendMove(btn)});
    btn.addEventListener('pointerup',e=>{btn.classList.remove('btn-active');api(base()+'/movestop')});
    btn.addEventListener('pointerleave',e=>{btn.classList.remove('btn-active');api(base()+'/movestop')});
  });

  // Zoom (press-and-hold)
  $$('[data-zm]').forEach(btn=>{
    const d=btn.dataset.zm;
    if(d==='stop'){btn.onclick=()=>api(base()+'/zoomstop');return}
    btn.addEventListener('pointerdown',e=>{e.preventDefault();btn.classList.add('btn-active');api(base()+'/zoom',{speed:state.zoomSpeed,direction:d})});
    btn.addEventListener('pointerup',()=>{btn.classList.remove('btn-active');api(base()+'/zoomstop')});
    btn.addEventListener('pointerleave',()=>{btn.classList.remove('btn-active');api(base()+'/zoomstop')});
  });

  // Focus (press-and-hold)
  $$('[data-fc]').forEach(btn=>{
    const d=btn.dataset.fc;
    if(d==='stop'){btn.onclick=()=>api(base()+'/focusstop');return}
    btn.addEventListener('pointerdown',e=>{e.preventDefault();btn.classList.add('btn-active');api(base()+'/focus',{speed:state.focusSpeed,direction:d})});
    btn.addEventListener('pointerup',()=>{btn.classList.remove('btn-active');api(base()+'/focusstop')});
    btn.addEventListener('pointerleave',()=>{btn.classList.remove('btn-active');api(base()+'/focusstop')});
  });

  // Focus mode
  $$('[data-fm]').forEach(btn=>btn.onclick=()=>api(base()+'/focusmode',{mode:btn.dataset.fm}));

  // Presets
  $$('[data-pr]').forEach(btn=>btn.onclick=()=>api(base()+'/preset',{action:'Recall',number:parseInt(btn.dataset.pr)}));
  $$('[data-ps]').forEach(btn=>btn.onclick=()=>{if(confirm('Set preset '+btn.dataset.ps+'?'))api(base()+'/preset',{action:'Set',number:parseInt(btn.dataset.ps)})});

  // Exposure
  $('#exp-mode-btn').onclick=()=>api(base()+'/exposure',{mode:$('#exp-mode').value});

  // Adjustments (iris, shutter, gain, aperture, redgain, bluegain)
  $$('[data-adj]').forEach(btn=>btn.onclick=()=>api(base()+'/'+btn.dataset.adj,{direction:btn.dataset.dir}));

  // White balance
  $('#wb-mode-btn').onclick=()=>api(base()+'/whitebalance',{mode:$('#wb-mode').value});
  $('#wb-trigger').onclick=()=>api(base()+'/wbtrigger');

  // Backlight
  $$('[data-bl]').forEach(btn=>btn.onclick=()=>api(base()+'/backlight',{mode:btn.dataset.bl}));

  // Power
  $$('[data-pw]').forEach(btn=>btn.onclick=()=>{if(btn.dataset.pw==='Off'&&!confirm('Power off camera?'))return;api(base()+'/power',{state:btn.dataset.pw})});

  // Virtual joystick
  setupJoystick();
}

function setupSlider(id,cb){
  const sl=$('#'+id);const vl=$('#'+id+'-val');
  if(!sl)return;
  const saved=ls(id.replace('-',''));
  if(saved!=null){sl.value=saved;vl.textContent=saved;cb(parseInt(saved))}
  sl.oninput=function(){vl.textContent=this.value;cb(parseInt(this.value))};
}

function sendMove(btn){
  const t=btn.dataset.mv;const b=base();
  if(t==='pan')api(b+'/pan',{speed:state.panSpeed,direction:btn.dataset.pd});
  else if(t==='tilt')api(b+'/tilt',{speed:state.tiltSpeed,direction:btn.dataset.td});
  else if(t==='pantilt')api(b+'/pantilt',{panSpeed:state.panSpeed,tiltSpeed:state.tiltSpeed,panDirection:btn.dataset.pd,tiltDirection:btn.dataset.td});
}

function setupJoystick(){
  const base_el=$('#joystick');const knob=$('#joy-knob');
  if(!base_el)return;
  let active=false,rect;
  const R=80,KR=25;

  function start(e){e.preventDefault();active=true;rect=base_el.getBoundingClientRect();move(e)}
  function move(e){
    if(!active)return;
    const cx=rect.left+R,cy=rect.top+R;
    const px=e.clientX-cx;
    const py=e.clientY-cy;
    const dist=Math.min(Math.sqrt(px*px+py*py),R-KR);
    const angle=Math.atan2(py,px);
    const nx=Math.cos(angle)*dist,ny=Math.sin(angle)*dist;
    knob.style.left=(R-KR+nx)+'px';knob.style.top=(R-KR+ny)+'px';
    const normX=nx/(R-KR),normY=ny/(R-KR);
    const deadzone=0.15;
    let pd='Stop',td='Stop',ps=0,ts=0;
    if(Math.abs(normX)>deadzone){pd=normX<0?'Left':'Right';ps=Math.round(Math.abs(normX)*state.panSpeed)}
    if(Math.abs(normY)>deadzone){td=normY<0?'Up':'Down';ts=Math.round(Math.abs(normY)*state.tiltSpeed)}
    if(pd==='Stop'&&td==='Stop')api('/api/cameras/'+state.camIdx+'/movestop');
    else api('/api/cameras/'+state.camIdx+'/pantilt',{panSpeed:ps,tiltSpeed:ts,panDirection:pd,tiltDirection:td});
  }
  function end(){
    if(!active)return;active=false;
    knob.style.left=(R-KR)+'px';knob.style.top=(R-KR)+'px';
    api('/api/cameras/'+state.camIdx+'/movestop');
  }
  base_el.addEventListener('pointerdown',start);
  document.addEventListener('pointermove',move);
  document.addEventListener('pointerup',end);
}

function restoreSections(){
  const saved=ls('sections');
  if(saved){state.sections=saved;Object.keys(saved).forEach(id=>{if(saved[id]){const s=$('#sec-'+id);if(s)s.classList.add('collapsed')}})}
}

// Initialize
loadCameras();
setInterval(loadCameras,3000);
</script></body></html>";
    }

    public void Dispose()
    {
        Stop();
    }
}
