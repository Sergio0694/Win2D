// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using System;
using System.Linq;
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

            using ComPtr<IUnknown> resourceTextureManager = default;

            // Create an ID2D1ResourceTextureManager instance
            D2D1ResourceTextureManager.Create((void**)resourceTextureManager.GetAddressOf());

            // Initialize it with some data (this will go into a staging buffer for now)
            D2D1ResourceTextureManager.Initialize(
                resourceTextureManager.Get(),
                Guid.NewGuid(),
                extents: stackalloc uint[] { 4096 },
                bufferPrecision: D2D1BufferPrecision.Float32,
                channelDepth: D2D1ChannelDepth.One,
                filter: D2D1Filter.MinMagMipLinear,
                extendModes: stackalloc D2D1ExtendMode[] { D2D1ExtendMode.Clamp },
                data: MemoryMarshal.AsBytes(Enumerable.Range(0, 4096).Select(i => i / 4096.0f).ToArray().AsSpan()),
                strides: ReadOnlySpan<uint>.Empty);

            // Assign the resource texture manager to the effect
            d2D1Effect.Get()->SetValue(
                index: 4,
                type: D2D1_PROPERTY_TYPE.Unknown,
                data: (byte*)resourceTextureManager.GetAddressOf(),
                dataSize: (uint)sizeof(void*)).Assert();

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
    [D2DInputCount(0)]
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

        [D2DResourceTextureIndex(0)]
        public readonly D2D1ResourceTexture1D<float> buffer;

        /// <inheritdoc/>
        public float4 Execute()
        {
            int2 xy = (int2)D2D.GetScenePosition().XY;

            uint x = (uint)xy.X % 64;
            uint y = (uint)xy.Y % 64;

            float value = buffer[(int)(x * 64 + y)];

            return new(value, 0, 0, 1);
        }
    }
}
