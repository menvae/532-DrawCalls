using System.Collections.Generic;
using FFmpeg.AutoGen;

namespace osu.Framework.Graphics.Video
{
    // ReSharper disable once InconsistentNaming
    public class AVHWDeviceTypeEncoderPerformanceComparer : Comparer<AVHWDeviceType>
    {
        // higher = better
        private static readonly IReadOnlyDictionary<AVHWDeviceType, int> performance_scores = new Dictionary<AVHWDeviceType, int>
        {
            // Windows & Linux (NVIDIA NVENC)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA, 10 },
            // Windows & Linux (Intel Quick Sync)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_QSV, 9 },
            // Apple (VideoToolbox)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX, 10 },
            // Android (MediaCodec)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC, 10 },
            // Linux (Intel/AMD/NVIDIA)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI, 8 },
            // Windows (AMD AMF or Intel QSV via D3D11)
            { AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, 7 },
            // Modern cross-platform interop
            { AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN, 6 },
        };

        public override int Compare(AVHWDeviceType x, AVHWDeviceType y)
        {
            int xScore = performance_scores.GetValueOrDefault(x, int.MinValue);
            int yScore = performance_scores.GetValueOrDefault(y, int.MinValue);

            return -Comparer<int>.Default.Compare(xScore, yScore);
        }
    }
}
