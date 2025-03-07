﻿namespace FFMediaToolkit.Common.Internal
{
    using System.Drawing;
    using FFMediaToolkit.Graphics;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a cache object for FFMpeg <see cref="SwsContext"/>. Useful when converting many bitmaps to the same format.
    /// </summary>
    internal unsafe class Scaler : Wrapper<SwsContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Scaler"/> class.
        /// </summary>
        public Scaler()
            : base(null)
        {
        }

        /// <summary>
        /// Overrides the <paramref name="destinationFrame"/> image buffer with rescaled specified bitmap. Used in encoding.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        /// <param name="destinationFrame">The <see cref="AVFrame"/> to override.</param>
        internal void FillAVFrame(ImageData bitmap, VideoFrame destinationFrame)
        {
            var context = GetCachedContext(bitmap.ImageSize, (AVPixelFormat)bitmap.PixelFormat, destinationFrame.Layout, destinationFrame.PixelFormat);
            fixed (byte* ptr = bitmap.Data)
            {
                var data = new byte*[4] { ptr, null, null, null };
                var linesize = new int[4] { bitmap.Stride, 0, 0, 0 };
                ffmpeg.sws_scale(context, data, linesize, 0, bitmap.ImageSize.Height, destinationFrame.Pointer->data, destinationFrame.Pointer->linesize);
            }
        }

        /// <summary>
        /// Converts a video <see cref="AVFrame"/> to bitmap data with a specified layout and writes its to the specified <see cref="ImageData"/>. Used in decoding.
        /// </summary>
        /// <param name="videoFrame">The video frame to convert.</param>
        /// <param name="destination">The destination <see cref="ImageData"/>.</param>
        internal void AVFrameToBitmap(VideoFrame videoFrame, ImageData destination)
        {
            var context = GetCachedContext(videoFrame.Layout, videoFrame.PixelFormat, destination.ImageSize, (AVPixelFormat)destination.PixelFormat);
            fixed (byte* ptr = destination.Data)
            {
                var data = new byte*[4] { ptr, null, null, null };
                var linesize = new int[4] { destination.Stride, 0, 0, 0 };
                ffmpeg.sws_scale(context, videoFrame.Pointer->data, videoFrame.Pointer->linesize, 0, videoFrame.Layout.Height, data, linesize);
            }
        }

        /// <inheritdoc/>
        protected override void OnDisposing() => ffmpeg.sws_freeContext(Pointer);

        private SwsContext* GetCachedContext(Size sourceSize, AVPixelFormat sourceFormat, Size destinationSize, AVPixelFormat destinationFormat)
        {
            // If don't change the dimensions of the image, there is no need to use the high quality bicubic method.
            var scaleMode = sourceSize == destinationSize ? ffmpeg.SWS_POINT : ffmpeg.SWS_BICUBIC;

            UpdatePointer(ffmpeg.sws_getCachedContext(Pointer, sourceSize.Width, sourceSize.Height, sourceFormat, destinationSize.Width, destinationSize.Height, destinationFormat, scaleMode, null, null, null));
            return Pointer;
        }
    }
}
