using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Threading;

namespace Nexus.Services.Meeting
{
    public class VideoFrameDecoder : IDisposable
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;
        
        private readonly int _width;
        private readonly int _height;
        private WriteableBitmap? _bitmap;
        private bool _disposed;
        private int _frameCount;
        private readonly object _lock = new();

        public int Width => _width;
        public int Height => _height;

        public VideoFrameDecoder(int width = DefaultWidth, int height = DefaultHeight)
        {
            _width = width;
            _height = height;
        }

        public (WriteableBitmap? bitmap, bool isNew) DecodeFrame(byte[] frameData)
        {
            if (_disposed)
                return (null, false);

            lock (_lock)
            {
                try
                {
                    var rowSize = ((_width * 3 + 3) & ~3);
                    var expectedSize = rowSize * _height;

                    if (frameData.Length < expectedSize)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoFrameDecoder] 帧数据太小: 期望 {expectedSize}, 实际 {frameData.Length}");
                        return (null, false);
                    }

                    bool isNew = _bitmap == null;
                    
                    _bitmap ??= new WriteableBitmap(
                        new Avalonia.PixelSize(_width, _height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        AlphaFormat.Premul
                    );

                    using (var locked = _bitmap.Lock())
                    {
                        ConvertRgb24ToBgra32(frameData, locked.Address, _width, _height, locked.RowBytes);
                    }

                    _frameCount++;
                    return (_bitmap, isNew);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoFrameDecoder] 解码帧失败: {ex.Message}");
                    return (null, false);
                }
            }
        }

        private unsafe void ConvertRgb24ToBgra32(byte[] rgbData, IntPtr destPtr, int width, int height, int destRowBytes)
        {
            var rowSize = ((width * 3 + 3) & ~3);
            
            fixed (byte* srcPtr = rgbData)
            {
                byte* src = srcPtr;
                byte* dest = (byte*)destPtr;

                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src + y * rowSize;
                    byte* destRow = dest + y * destRowBytes;

                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = x * 3;
                        int destIdx = x * 4;

                        destRow[destIdx + 0] = srcRow[srcIdx + 2];
                        destRow[destIdx + 1] = srcRow[srcIdx + 1];
                        destRow[destIdx + 2] = srcRow[srcIdx + 0];
                        destRow[destIdx + 3] = 255;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lock)
                {
                    _bitmap?.Dispose();
                    _bitmap = null;
                }
                _disposed = true;
            }
        }
    }
}
