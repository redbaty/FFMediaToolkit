﻿namespace FFMediaToolkit.Decoding.Internal
{
    using System;
    using System.IO;
    using FFMediaToolkit.Common;
    using FFMediaToolkit.Common.Internal;
    using FFMediaToolkit.Helpers;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a input multimedia stream.
    /// </summary>
    /// <typeparam name="TFrame">The type of frames in the stream.</typeparam>
    internal unsafe class InputStream<TFrame> : Wrapper<AVStream>
        where TFrame : MediaFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputStream{TFrame}"/> class.
        /// </summary>
        /// <param name="stream">The multimedia stream.</param>
        /// <param name="owner">The container that owns the stream.</param>
        public InputStream(AVStream* stream, InputContainer owner)
            : base(stream)
        {
            OwnerFile = owner;
            PacketQueue = new ObservableQueue<MediaPacket>();

            Type = typeof(TFrame) == typeof(VideoFrame) ? MediaType.Video : MediaType.None;
            Info = new StreamInfo(stream, owner);
        }

        /// <summary>
        /// Gets the media container that owns this stream.
        /// </summary>
        public InputContainer OwnerFile { get; }

        /// <summary>
        /// Gets a pointer to <see cref="AVCodecContext"/> for this stream.
        /// </summary>
        public AVCodecContext* CodecPointer => Pointer->codec;

        /// <summary>
        /// Gets the type of this stream.
        /// </summary>
        public MediaType Type { get; }

        /// <summary>
        /// Gets informations about the stream.
        /// </summary>
        public StreamInfo Info { get; }

        /// <summary>
        /// Gets the packet queue.
        /// </summary>
        public ObservableQueue<MediaPacket> PacketQueue { get; }

        /// <summary>
        /// Reads the next frame from the stream and writes its data to the specified <see cref="MediaFrame"/> object.
        /// </summary>
        /// <param name="frame">A media frame to override with the new decoded frame.</param>
        public void Read(TFrame frame)
        {
            int error;

            do
            {
                SendPacket();
                error = ffmpeg.avcodec_receive_frame(CodecPointer, frame.Pointer);
            }
            while (error == -ffmpeg.EAGAIN);

            error.ThrowIfError("An error ocurred while decoding the frame.");
        }

        /// <summary>
        /// Flushes the codec buffers.
        /// </summary>
        public void FlushBuffers() => ffmpeg.avcodec_flush_buffers(CodecPointer);

        /// <inheritdoc/>
        protected override void OnDisposing()
        {
            FlushBuffers();

            var ptr = CodecPointer;
            ffmpeg.avcodec_close(ptr);
            ffmpeg.avcodec_free_context(&ptr);
        }

        private void SendPacket()
        {
            if (!PacketQueue.TryPeek(out var pkt))
            {
                if (OwnerFile.IsAtEndOfFile)
                {
                    throw new EndOfStreamException("End of the media strem.");
                }
                else
                {
                    throw new Exception("No packets in queue.");
                }
            }

            var result = ffmpeg.avcodec_send_packet(CodecPointer, pkt);

            if (result == -ffmpeg.EAGAIN)
            {
                return;
            }
            else
            {
                result.ThrowIfError("Cannot send a packet to the decoder.");
                PacketQueue.TryDequeue(out var _);
            }
        }
    }
}
