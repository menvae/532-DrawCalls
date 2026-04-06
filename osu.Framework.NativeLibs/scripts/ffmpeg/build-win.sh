#!/bin/bash
set -eu
pushd "$(dirname "$0")" > /dev/null
SCRIPT_PATH=$(pwd)
popd > /dev/null
source "$SCRIPT_PATH/common.sh"

# Detect whether we're running natively on Windows (MSYS2/MinGW) or cross-compiling from Linux/macOS
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "mingw"* || "$OSTYPE" == "cygwin" ]]; then
    NATIVE_WIN=true
else
    NATIVE_WIN=false
fi

if [ -z "${arch-}" ]; then
    if $NATIVE_WIN; then
        PS3='Build for which arch? '
        select arch in "x86" "x64" "arm64"; do
            if [ -z "$arch" ]; then
                echo "invalid option"
            else
                break
            fi
        done
    else
        PS3='Build for which arch? '
        select arch in "x86" "x64" "arm64"; do
            if [ -z "$arch" ]; then
                echo "invalid option"
            else
                break
            fi
        done
    fi
fi

FFMPEG_FLAGS+=(
    --enable-w32threads
    --enable-dxva2
    --enable-d3d11va
    # Decode hwaccels
    --enable-hwaccel='h264_dxva2,h264_d3d11va,h264_d3d11va2'
    --enable-hwaccel='hevc_dxva2,hevc_d3d11va,hevc_d3d11va2'
    --enable-hwaccel='vp9_dxva2,vp9_d3d11va,vp9_d3d11va2'
    # NVIDIA NVENC encode hwaccels
    --enable-nvenc
    --enable-encoder='h264_nvenc,hevc_nvenc'
    # AMD AMF encode hwaccels
    --enable-amf
    --enable-encoder='h264_amf,hevc_amf'
    # Windows Media Foundation encode hwaccels (built-in, no extra SDK needed)
    --enable-mediafoundation
    --enable-encoder='h264_mf,hevc_mf'
)

if $NATIVE_WIN; then
    # Native build — no cross-compilation flags needed; arch is controlled by the
    # compiler that MSYS2/MinGW puts on PATH (e.g. launch from the correct MSYS2 shell)
    echo "-> Native Windows build detected, skipping cross-compile flags."
    case $arch in
        x86)   FFMPEG_FLAGS+=(--arch=x86)    ;;
        x64)   FFMPEG_FLAGS+=(--arch=x86_64) ;;
        arm64) FFMPEG_FLAGS+=(--arch=aarch64) ;;
    esac
    FFMPEG_FLAGS+=(--target-os=mingw32)
else
    # Cross-compilation from Linux/macOS
    cross_arch=''
    cross_prefix=''
    case $arch in
        x86)
            cross_arch='x86'
            cross_prefix='i686-w64-mingw32-'
            ;;
        x64)
            cross_arch='x86_64'
            cross_prefix='x86_64-w64-mingw32-'
            ;;
        arm64)
            cross_arch='aarch64'
            cross_prefix='aarch64-w64-mingw32-'
            ;;
    esac
    FFMPEG_FLAGS+=(
        --extra-libs='-lkernel32'
        --enable-cross-compile
        --target-os=mingw32
        --arch=$cross_arch
        --cross-prefix=$cross_prefix
    )
fi

pushd . > /dev/null
prep_ffmpeg "win-$arch"
build_ffmpeg
popd > /dev/null

find "win-$arch" -not -name "win-$arch" -not -name '*.dll' -delete
