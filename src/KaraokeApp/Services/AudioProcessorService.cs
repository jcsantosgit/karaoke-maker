using Whisper.net;
using Whisper.net.Ggml;
using Xabe.FFmpeg;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System;

public class AudioProcessorService
{
    private readonly string _modelPath;
    private readonly int _videoWidth;
    private readonly int _videoHeight;
    private readonly string? _backgroundImagePath;
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly string _vocalRemover;
    private readonly int _karaokeFontSize;
    private readonly int _previewFontSize;
    private readonly int _titleFontSize;
    private const string FONT_NAME = "Liberation Sans"; // Using a more common font in Linux environments

    public AudioProcessorService(IConfiguration configuration, ILogger<AudioProcessorService> logger)
    {
        _modelPath = configuration["Whisper:ModelPath"] ?? "WhisperModels";
        _videoWidth = int.Parse(configuration["Video:Width"] ?? "1280");
        _videoHeight = int.Parse(configuration["Video:Height"] ?? "720");
        _backgroundImagePath = configuration["Video:BackgroundImage"];
        _logger = logger;
        _vocalRemover = configuration["Audio:VocalRemover"] ?? "ffmpeg";
        _karaokeFontSize = int.Parse(configuration["Subtitles:KaraokeFontSize"] ?? "52");
        _previewFontSize = int.Parse(configuration["Subtitles:PreviewFontSize"] ?? "28");
        _titleFontSize = int.Parse(configuration["Subtitles:TitleFontSize"] ?? "36");
    }

    public async Task InitializeAsync()
    {
        // FFmpeg availability will be checked when used
        await Task.Delay(1);
    }

    public async Task<string> GenerateAssFromAudioAsync(string audioPath, string language)
    {
        _logger.LogInformation("Step 2: Generating subtitles for {audioPath} in {language}", audioPath, language);

        string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        var conversionToWav = await FFmpeg.Conversions.New()
            .AddParameter($"-i \"{audioPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1")
            .SetOutput(wavPath)
            .Start();
        _logger.LogInformation("Converted audio to 16kHz mono WAV for Whisper: {wavPath}", wavPath);

        string modelName = "ggml-medium.bin";
        string modelFilePath = Path.Combine(_modelPath, modelName);

        if (!File.Exists(modelFilePath))
        {
            _logger.LogWarning("Medium model not found at {path}. Falling back to Base model.", modelFilePath);
            modelFilePath = Path.Combine(_modelPath, "ggml-base.bin");
            if (!File.Exists(modelFilePath))
            {
                throw new FileNotFoundException($"No Whisper model found in {_modelPath}. Please download ggml-medium.bin or ggml-base.bin.");
            }
        }

        var segments = new List<Whisper.net.SegmentData>();
        using var whisperFactory = WhisperFactory.FromPath(modelFilePath);

        string prompt = language switch
        {
            "pt" => "Letra de uma música, transcrição fiel.",
            "es" => "Letra de una canción, transcripción fiel.",
            _ => "Lyrics of a song, accurate transcription.",
        };

        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .WithPrompt(prompt)
            .Build();

        using var fileStream = File.OpenRead(wavPath);
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            segments.Add(segment);
        }
        File.Delete(wavPath);

        if (segments.Count == 0)
        {
            _logger.LogWarning("Whisper.net did not produce any segments for {audioPath}. The subtitle file will be empty.", audioPath);
        }
        else
        {
            _logger.LogInformation("Whisper.net generated {count} segments.", segments.Count);
        }

        string assPath = Path.ChangeExtension(audioPath, ".ass");
        await CreateAssFileFromSegments(assPath, segments);

        return assPath;
    }

    private async Task CreateAssFileFromSegments(string assPath, List<Whisper.net.SegmentData> segments)
    {
        var assBuilder = new StringBuilder();
        assBuilder.AppendLine("[Script Info]");
        assBuilder.AppendLine("Title: Karaoke Video");
        assBuilder.AppendLine("ScriptType: v4.00+");
        assBuilder.AppendLine("WrapStyle: 0");
        assBuilder.AppendLine("ScaledBorderAndShadow: yes");
        assBuilder.AppendLine();
        assBuilder.AppendLine("[V4+ Styles]");
        assBuilder.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        assBuilder.AppendLine($"Style: Karaoke,{FONT_NAME},{_karaokeFontSize},&H00FFFFFF,&H0000FFFF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3,0,2,30,30,50,1");
        assBuilder.AppendLine($"Style: Preview,{FONT_NAME},{_previewFontSize},&H00808080,&H00808080,&H00000000,&H80000000,0,0,0,0,100,100,0,0,1,2,0,2,30,30,100,1");
        assBuilder.AppendLine();
        assBuilder.AppendLine("[Events]");
        assBuilder.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        for (int i = 0; i < segments.Count; i++)
        {
            var currentSegment = segments[i];
            var nextSegment = (i + 1 < segments.Count) ? segments[i + 1] : null;

            string dialogue = $"Dialogue: 0,{FormatAssTime(currentSegment.Start)},{FormatAssTime(currentSegment.End)},Karaoke,,0,0,0,,";
            string[] words = currentSegment.Text.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            double totalDuration = (currentSegment.End - currentSegment.Start).TotalSeconds;

            if (words.Length > 0)
            {
                double timePerWord = totalDuration / words.Length;
                foreach (var word in words)
                {
                    int durationMs = (int)(timePerWord * 100); // Duration in centiseconds for \k
                    dialogue += $"{{\\k{durationMs}}}{word} ";
                }
            }
            else
            {
                dialogue += currentSegment.Text;
            }
            assBuilder.AppendLine(dialogue.TrimEnd());

            if (nextSegment != null)
            {
                string preview = $"Dialogue: 0,{FormatAssTime(currentSegment.Start)},{FormatAssTime(currentSegment.End)},Preview,,0,0,0,,{nextSegment.Text.Trim()}";
                assBuilder.AppendLine(preview);
            }
        }

        await File.WriteAllTextAsync(assPath, assBuilder.ToString());
        _logger.LogInformation("ASS subtitle file created at {assPath}", assPath);
    }

    public async Task<(string instrumental, string vocals)> RemoveVocalsAsync(string inputAudioPath)
    {
        _logger.LogInformation("Step 1: Removing vocals from {inputAudioPath} using {_vocalRemover}", inputAudioPath, _vocalRemover);

        if (_vocalRemover == "demucs")
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "demucs",
                    Arguments = $"--two-stems=vocals -o \"{Path.GetDirectoryName(inputAudioPath)}\" \"{inputAudioPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Demucs failed with exit code {exitCode}. Output: {output}. Error: {error}", process.ExitCode, output, error);
                throw new Exception($"Demucs failed: {error}");
            }
            _logger.LogInformation("Demucs separation successful.");

            string separatedDir = Path.Combine(Path.GetDirectoryName(inputAudioPath) ?? "", "htdemucs", Path.GetFileNameWithoutExtension(inputAudioPath));
            var noVocalsPath = Path.Combine(separatedDir, "no_vocals.wav");
            var vocalsPath = Path.Combine(separatedDir, "vocals.wav");

            if (!File.Exists(noVocalsPath) || !File.Exists(vocalsPath))
            {
                throw new Exception($"Demucs output missing. Expected to find files in {separatedDir}");
            }
            
            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.aac");
            await FFmpeg.Conversions.New()
                .AddParameter($"-i \"{noVocalsPath}\" -vn -c:a aac -ar 44100")
                .SetOutput(instrumentalPath)
                .Start();

            string cleanVocalsPath = Path.ChangeExtension(inputAudioPath, "_vocals_clean.wav");
            File.Move(vocalsPath, cleanVocalsPath);
            
            Directory.Delete(separatedDir, true);
            
            return (instrumentalPath, cleanVocalsPath);
        }
        else
        {
            // Fallback FFmpeg for vocal removal
            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.aac");
            await FFmpeg.Conversions.New()
                .AddParameter($"-i \"{inputAudioPath}\" -vn -af pan=stereo|c0=FL-0.5*FC|c1=FR-0.5*FC -c:a aac -ar 44100")
                .SetOutput(instrumentalPath)
                .Start();
            
            return (instrumentalPath, inputAudioPath);
        }
    }

    public async Task<string> GenerateVideoWithAudioAndSubtitlesAsync(string instrumentalAudioPath, string assPath, string? musicTitle = "", string? artistName = "")
    {
        _logger.LogInformation("Step 3: Generating final video.");
        string outputPath = Path.ChangeExtension(instrumentalAudioPath, ".mp4").Replace("_instrumental", "_karaoke");
        string tempVideoPath = Path.ChangeExtension(outputPath, ".temp.mp4");

        var mediaInfo = await FFmpeg.GetMediaInfo(instrumentalAudioPath);
        var duration = mediaInfo.Duration;

        // Step 3a: Create base video with background and instrumental audio
        _logger.LogInformation("Creating base video with audio at {tempVideoPath}", tempVideoPath);
        var conversion = FFmpeg.Conversions.New();
        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            conversion.AddParameter($"-loop 1 -i \"{_backgroundImagePath}\" -i \"{instrumentalAudioPath}\" -c:v libx264 -tune stillimage -c:a copy -pix_fmt yuv420p -shortest");
        }
        else
        {
            conversion.AddParameter($"-f lavfi -i color=c=black:s={_videoWidth}x{_videoHeight}:d={duration.TotalSeconds} -i \"{instrumentalAudioPath}\" -c:v libx264 -c:a copy -pix_fmt yuv420p");
        }
        await conversion.SetOutput(tempVideoPath).Start();

        // Step 3b: Burn subtitles onto the base video
        _logger.LogInformation("Burning subtitles from {assPath} into final video at {outputPath}", assPath, outputPath);
        
        // Correctly escape the path for the FFmpeg 'ass' filter
        string escapedAssPath = assPath.Replace("\\", "\\\\").Replace(":", "\\:");
        
        await FFmpeg.Conversions.New()
            .AddParameter($"-i \"{tempVideoPath}\" -vf \"ass='{escapedAssPath}'\" -c:a copy")
            .SetOutput(outputPath)
            .Start();

        // Clean up temporary video file
        if (File.Exists(tempVideoPath))
        {
            File.Delete(tempVideoPath);
        }
        
        _logger.LogInformation("Successfully created final karaoke video at {outputPath}", outputPath);
        return outputPath;
    }

    private string FormatAssTime(TimeSpan ts)
    {
        return $"{ts.Hours:D1}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
    }
}