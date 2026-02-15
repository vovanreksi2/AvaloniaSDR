using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;
using System;

namespace AvaloniaSDR.UI.Views;

/// <summary>
/// GPU-accelerated waterfall renderer using Avalonia's composition thread.
/// Uses an SKSL runtime shader for color mapping and ring-buffer scrolling on the GPU.
/// </summary>
public class WaterfallVisualHandler : CompositionCustomVisualHandler
{
    private const string ShaderSource = @"
uniform shader dataTexture;
uniform float scrollOffset;
uniform float2 resolution;
uniform float2 textureSize;

half4 main(float2 fragCoord) {
    float2 uv = fragCoord / resolution;

    // Ring buffer scroll: newest row at top, older rows scroll down
    float y = fract(scrollOffset - uv.y);

    // Sample the data texture alpha channel (Alpha8 format) using physical pixel dimensions
    float power = dataTexture.eval(float2(uv.x * textureSize.x, y * textureSize.y)).a;

    // GPU color mapping: blue -> cyan -> green -> yellow -> red
    float t = clamp(power, 0.0, 1.0);

    half3 c;
    if (t < 0.25) {
        float s = t / 0.25;
        c = half3(0.0, s, 1.0);
    } else if (t < 0.5) {
        float s = (t - 0.25) / 0.25;
        c = half3(0.0, 1.0, 1.0 - s);
    } else if (t < 0.75) {
        float s = (t - 0.5) / 0.25;
        c = half3(s, 1.0, 0.0);
    } else {
        float s = (t - 0.75) / 0.25;
        c = half3(1.0, 1.0 - s, 0.0);
    }

    return half4(c, 1.0);
}
";

    private SKRuntimeEffect? _effect;
    private string? _shaderError;
    private readonly object _frameLock = new();
    private WaterfallFrameData? _pendingFrame;
    private WaterfallFrameData? _currentFrame;

    public override void OnRender(ImmediateDrawingContext context)
    {
        lock (_frameLock)
        {
            if (_pendingFrame.HasValue)
            {
                _currentFrame?.Return();
                _currentFrame = _pendingFrame.Value;
                _pendingFrame = null;
            }
        }

        var bounds = GetRenderBounds();

        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        if (_currentFrame == null)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        if (_effect == null && _shaderError == null)
        {
            _effect = SKRuntimeEffect.CreateShader(ShaderSource, out var errors);
            if (_effect == null)
            {
                _shaderError = errors;
            }
        }

        if (_effect == null) return;

        var data = _currentFrame.Value;
        RenderWithShader(canvas, data);
    }

    private void RenderWithShader(SKCanvas canvas, WaterfallFrameData data)
    {
        var imageInfo = new SKImageInfo(data.Width, data.Height, SKColorType.Alpha8, SKAlphaType.Premul);

        using var skData = SKData.CreateCopy(data.PixelData.AsSpan(0, data.ActualSize));
        using var image = SKImage.FromPixels(imageInfo, skData, data.Width);

        if (image == null) return;

        using var imageShader = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

        var bounds = GetRenderBounds();
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;

        if (width <= 0 || height <= 0) return;

        var uniforms = new SKRuntimeEffectUniforms(_effect!)
        {
            ["scrollOffset"] = data.ScrollOffset,
            ["resolution"] = new[] { width, height },
            ["textureSize"] = new[] { (float)data.Width, (float)data.Height }
        };

        var children = new SKRuntimeEffectChildren(_effect!)
        {
            ["dataTexture"] = imageShader
        };

        using var shader = _effect!.ToShader(uniforms, children);
        using var paint = new SKPaint();
        paint.Shader = shader;

        canvas.DrawRect(0, 0, width, height, paint);
    }

    public override void OnMessage(object message)
    {
        if (message is WaterfallFrameData data)
        {
            lock (_frameLock)
            {
                _pendingFrame?.Return();
                _pendingFrame = data;
            }
            RegisterForNextAnimationFrameUpdate();
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        Invalidate();
    }
}
