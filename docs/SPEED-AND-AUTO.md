# FramePress speed and automatic folder processing update

## Operation
1. Choose a folder.
2. Set profiles/save mode once, then click Search & compress.

Discovery, metadata probing and encoding now form one streaming operation. Each eligible file starts processing before the rest of the folder is fully inspected. Subfolders are supported. There is no required scan/select/start sequence. Scan only and Process selected remain optional for samples and manual workflows.

The total batch percentage is indeterminate while files are still being discovered; each file has its own real progress. Cancel stops the active external process and prevents starting later files. No parallel full-video encodes are launched, keeping memory and encoder-session use bounded.

## Speed
- New default CPU preset: veryfast, keeping the quality setting at 20. Existing medium presets migrate to veryfast once; custom fast/slow settings remain. Later edits are respected.
- Acceleration Auto benchmarks NVIDIA NVENC, Intel QSV, AMD AMF and a CPU baseline on a short source sample, then chooses the fastest successful measured encoder. Results cache per codec/bit depth/dimensions/quality/preset for this session. Missing drivers/hardware fall back to CPU.
- If a hardware encoder passes its small test but fails on real footage, the partial output is removed and that file retries on CPU. Cancel does not trigger a retry.
- CPU uses CRF; NVIDIA uses CQ; Intel uses global_quality; AMD uses constant QP. These scales are not perceptually equivalent. Compare a sample when changing engines. No fixed quality-loss percentage or speedup is promised.
- GPU encoding is supported; decoding/filtering can still use CPU. Disk speed, source codec and footage complexity can remain bottlenecks.
- Activity/report identifies the actual encoder: h264_nvenc / hevc_nvenc, *_qsv, *_amf, libx264 / libx265.

## Validation
Copy mode defaults to basic validation: FFmpeg success, output probe, size/duration/dimensions/audio-count checks, and source-change checks. This skips decoding the entire output again; the original is retained. Full decode remains an optional checkbox.

Replacement always forces full output decode and keeps recovery backups, regardless of the checkbox. HDR, high-bit-depth and camera data-stream safeguards remain.

## Verification
37 standard integration tests pass locally, including streaming discovery order, disabled profile filtering, cancellation preventing later starts, basic-copy verification, forced full replacement verification, and automatic selection falling back to CPU in this machine. Hardware acceleration itself requires testing on a supported Windows GPU; no GPU speed benchmark was performed here.

On Windows test:
- First choose copies, Auto, H.264, quality 20, veryfast, full verification off.
- Choose a folder with 3 short clips in nested folders and press Search & compress directly.
- Verify the first clip starts encoding before all files are inspected.
- Read Activity for the actual encoder, inspect outputs, then compare timing against CPU/medium using the same short source.
- If Auto uses libx264, the hardware tests did not find a working supported encoder. Check the GPU/driver before expecting GPU speed.
- Compare samples in your editing software before a large batch.

The updater also carries forward the Arabic, appearance, ComboBox name and explicit ConverterCulture fixes. Its transport is ASCII-only and compatible with Windows PowerShell 5.1 JSON array handling. It backs up edited source files and leaves vendor binaries, video files and preferences in place.
