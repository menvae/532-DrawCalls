using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace osu.Framework.Graphics.Video
{
    /// <summary>
    /// Represents a list of usable hardware video encoders.
    /// </summary>
    /// <remarks>
    /// Contains encoders for ALL platforms.
    /// </remarks>
    [Flags]
    // todo: revisit when we have a way to exclude enum members from naming rules
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum HardwareVideoEncoderType
    {
        [Description("None")]
        None = 0,

        /// <remarks>
        /// Windows and Linux only (Nvidia).
        /// </remarks>
        [Description("Nvidia NVENC")]
        NVENC = 1,

        /// <remarks>
        /// Windows and Linux only (Intel).
        /// </remarks>
        [Description("Intel Quick Sync Video")]
        QuickSyncVideo = 1 << 2,

        /// <remarks>
        /// Windows and Linux (AMD).
        /// </remarks>
        [Description("AMD AMF")]
        AMF = 1 << 3,

        /// <remarks>
        /// Linux only (Intel/AMD).
        /// </remarks>
        [Description("VA-API")]
        VAAPI = 1 << 4,

        /// <remarks>
        /// Android only.
        /// </remarks>
        [Description("Android MediaCodec")]
        MediaCodec = 1 << 5,

        /// <remarks>
        /// Apple devices only.
        /// </remarks>
        [Description("Apple VideoToolbox")]
        VideoToolbox = 1 << 6,

        [Description("Any")]
        Any = int.MaxValue,
    }
}
