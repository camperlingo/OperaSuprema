using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OperaSuprema.Core.Infrastructure
{
    public record VideoFrameInfo(string FilePath, double TimestampSeconds, string FormattedTime);

    public class VideoExtractionResult
    {
        public string? ExtractedAudioPath { get; set; }
        public List<VideoFrameInfo> ExtractedFrames { get; set; } = new();
        public double DurationSeconds { get; set; }
    }

    public class VideoPipelineService
    {
        public static bool IsVideoFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm";
        }

        public async Task<VideoExtractionResult> ProcessVideoAsync(string videoPath, Action<string>? logCallback = null, int maxFrames = 8, CancellationToken ct = default)
        {
            var result = new VideoExtractionResult();
            
            // 1. Get duration with ffprobe
            result.DurationSeconds = await GetVideoDurationAsync(videoPath, ct);
            if (result.DurationSeconds <= 0)
            {
                result.DurationSeconds = 10.0;
                logCallback?.Invoke($"[VideoPipeline] Durata non rilevata. Fallback a {result.DurationSeconds} secondi.");
            }
            else
            {
                logCallback?.Invoke($"[VideoPipeline] Durata rilevata: {result.DurationSeconds} secondi.");
            }

            // Temp dir
            string tempDir = Path.Combine(Path.GetTempPath(), $"opera_video_{Guid.NewGuid().ToString().Substring(0, 8)}");
            Directory.CreateDirectory(tempDir);
            
            // 2. Extract audio
            string audioPath = Path.Combine(tempDir, "audio.wav");
            var audioPsi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{audioPath}\"",
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(audioPsi))
            {
                if (process != null)
                {
                    await process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);
                    if (File.Exists(audioPath))
                    {
                        result.ExtractedAudioPath = audioPath;
                        logCallback?.Invoke($"[VideoPipeline] Audio estratto in: {audioPath}");
                    }
                }
            }

            // 3. Extract frames
            if (result.DurationSeconds > 0)
            {
                double fps = Math.Max(0.1, (double)maxFrames / result.DurationSeconds);
                string framesPattern = Path.Combine(tempDir, "frame_%03d.jpg");
                var framesPsi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{videoPath}\" -vf \"fps={fps.ToString(System.Globalization.CultureInfo.InvariantCulture)},scale='if(gt(iw,ih),min(768,iw),-2)':'if(gt(iw,ih),-2,min(768,ih))'\" -vframes {maxFrames} -q:v 3 \"{framesPattern}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(framesPsi))
                {
                    if (process != null)
                    {
                        await process.StandardError.ReadToEndAsync(ct);
                        await process.WaitForExitAsync(ct);
                        var extractedFiles = Directory.GetFiles(tempDir, "frame_*.jpg");
                        Array.Sort(extractedFiles);
                        for (int i = 0; i < extractedFiles.Length; i++)
                        {
                            double timestamp = i * (result.DurationSeconds / Math.Max(1, extractedFiles.Length));
                            TimeSpan ts = TimeSpan.FromSeconds(timestamp);
                            string formattedTime = ts.ToString(@"hh\:mm\:ss\.ff");
                            result.ExtractedFrames.Add(new VideoFrameInfo(extractedFiles[i], timestamp, formattedTime));
                        }
                        logCallback?.Invoke($"[VideoPipeline] Estratti {extractedFiles.Length} fotogrammi con tagging temporale.");
                    }
                }
            }

            return result;
        }

        private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return 0;
            
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(ct);
            
            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
            return 0;
        }

        public async Task<string> SliceAudioAsync(string sourceWav, double startSec, double durationSec, CancellationToken ct = default)
        {
            string slicePath = Path.Combine(Path.GetDirectoryName(sourceWav) ?? "/tmp", $"slice_{Guid.NewGuid().ToString().Substring(0, 8)}.wav");
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -ss {startSec.ToString(System.Globalization.CultureInfo.InvariantCulture)} -t {durationSec.ToString(System.Globalization.CultureInfo.InvariantCulture)} -i \"{sourceWav}\" -c copy \"{slicePath}\"",
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
            }
            return slicePath;
        }
    }
}
