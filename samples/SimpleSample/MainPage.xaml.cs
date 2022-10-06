// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComputeSharp;
using ComputeSharp.D2D1;
using ComputeSharp.D2D1.Interop;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Win32;
using Win32.Graphics.Direct2D;
using HRESULT = Win32.HResult;
using D2D1_PROPERTY_TYPE = Win32.Graphics.Direct2D.PropertyType;

#pragma warning disable CS0649

namespace SimpleSample
{
    /// <summary>
    /// Draws some graphics using Win2D
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void canvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            ReadOnlyMemory<byte> bytecode = D2D1PixelShader.LoadBytecode<HelloWorld>();

            PixelShaderEffect pixelShaderEffect = new(bytecode.ToArray());

            Test(sender, args.DrawingSession, pixelShaderEffect);
        }

        static unsafe void Test(CanvasControl sender, CanvasDrawingSession session, PixelShaderEffect pixelShaderEffect)
        {
            using ComPtr<IUnknown> canvasDevice = default;

            // Get the underlying IUnknown* object for the canvas device
            canvasDevice.Attach((IUnknown*)Marshal.GetIUnknownForObject(session.Device));

            using ComPtr<ID2D1Effect> d2D1Effect = default;
            using ComPtr<IUnknown> d2D1EffectUnknown = default;

            // Get the underlying IUnknown* object for the effect
            d2D1EffectUnknown.Attach((IUnknown*)Marshal.GetIUnknownForObject(pixelShaderEffect));

            using ComPtr<ICanvasResourceWrapperNative> canvasResourceWrapperNative = default;

            // Query for ICanvasResourceWrapperNative to get the underlying ID2D1Effect object
            d2D1EffectUnknown.CopyTo(canvasResourceWrapperNative.GetAddressOf()).Assert();

            // Get the underlying ID2D1Effect* object from the wrapper
            canvasResourceWrapperNative.Get()->GetNativeResource(
                device: canvasDevice.Get(),
                dpi: 0,
                iid: Win32.Apis.__uuidof<ID2D1Effect>(),
                resource: d2D1Effect.GetVoidAddressOf()).Assert();

            HelloWorld helloWorld = new(time: 0, width: (int)sender.Size.Width, height: (int)sender.Size.Height);

            // Get the shader constant buffer
            ReadOnlyMemory<byte> constantBuffer = D2D1PixelShader.GetConstantBuffer(in helloWorld);

            fixed (byte* p = constantBuffer.Span)
            {
                d2D1Effect.Get()->SetValue(
                    index: 1,
                    type: D2D1_PROPERTY_TYPE.Blob,
                    data: p,
                    dataSize: (uint)constantBuffer.Length).Assert();
            }

            session.DrawImage(pixelShaderEffect);
        }
    }

    public static class Ex
    {
        public static void Assert(this int hresult)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }

        public static void Assert(this HRESULT hresult)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }

    /// <summary>
    /// An interop wrapper type for Win2D objects (see <see href="https://microsoft.github.io/Win2D/WinUI3/html/Interop.htm"/>).
    /// </summary>
    [Guid("5F10688D-EA55-4D55-A3B0-4DDB55C0C20A")]
    internal unsafe struct ICanvasResourceWrapperNative
    {
        public void** lpVtbl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HRESULT QueryInterface(Guid* riid, void** ppvObject)
        {
            return ((delegate* unmanaged[Stdcall]<ICanvasResourceWrapperNative*, Guid*, void**, int>)(lpVtbl[0]))((ICanvasResourceWrapperNative*)Unsafe.AsPointer(ref this), riid, ppvObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint AddRef()
        {
            return ((delegate* unmanaged[Stdcall]<ICanvasResourceWrapperNative*, uint>)(lpVtbl[1]))((ICanvasResourceWrapperNative*)Unsafe.AsPointer(ref this));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Release()
        {
            return ((delegate* unmanaged[Stdcall]<ICanvasResourceWrapperNative*, uint>)(lpVtbl[2]))((ICanvasResourceWrapperNative*)Unsafe.AsPointer(ref this));
        }

        /// <summary>
        /// Interface provided by various Canvas objects that is able to retrieve the wrapped resource.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HRESULT GetNativeResource(void* device, float dpi, Guid* iid, void** resource)
        {
            return ((delegate* unmanaged[Stdcall]<ICanvasResourceWrapperNative*, void*, float, Guid*, void**, int>)(lpVtbl[3]))((ICanvasResourceWrapperNative*)Unsafe.AsPointer(ref this), device, dpi, iid, resource);
        }
    }

    /// <summary>
    /// A simple shader to get started with based on shadertoy new shader template.
    /// Ported from <see href="https://www.shadertoy.com/new"/>.
    /// </summary>
    [D2DRequiresScenePosition]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader41)]
    [AutoConstructor]
    internal readonly partial struct HelloWorld : ID2D1PixelShader
    {
        /// <summary>
        /// The current time since the start of the application.
        /// </summary>
        public readonly float time;
        public readonly int width;
        public readonly int height;

        /// <inheritdoc/>
        public float4 Execute()
        {
            // Normalized screen space UV coordinates from 0.0 to 1.0
            int2 xy = (int2)D2D.GetScenePosition().XY;
            float2 uv = (float2)xy / new float2(this.width, this.height);

            // Time varying pixel color
            float3 col = 0.5f + 0.5f * Hlsl.Cos(this.time + new float3(uv, uv.X) + new float3(0, 2, 4));

            // Output to screen
            return new(col, 1f);
        }
    }
}
