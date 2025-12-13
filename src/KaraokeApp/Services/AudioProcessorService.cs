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

public class AudioProcessorService
{
    private readonly string _modelPath;
    private readonly int _videoWidth;
    private readonly int _videoHeight;
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly IConfiguration _configuration;

    public AudioProcessorService(IConfiguration configuration, ILogger<AudioProcessorService> logger)
    {
        _modelPath = configuration["Whisper:ModelPath"] ?? "WhisperModels";
        _videoWidth = int.Parse(configuration["Video:Width"] ?? "1280");
        _videoHeight = int.Parse(configuration["Video:Height"] ?? "720");
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        // FFmpeg should already be available on the system
        // The Xabe.FFmpeg library will use the system's FFmpeg installation
        await Task.Delay(1); // Make method genuinely async to avoid compiler warning
    }

    public async Task<string> GenerateSrtFromAudioAsync(string audioPath, string language)
    {
        // Convert audio to a temporary WAV file for Whisper.net processing
        string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        var command = $"-i \"{audioPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavPath}\"";
        _logger.LogInformation("FFmpeg command: {command}", command);
        var wavConversion = await FFmpeg.Conversions.New()
            .AddParameter(command)
            .Start();

        // Select model based on language
        string modelName = $"ggml-base.bin"; // Default model
        if (language == "pt")
        {
            modelName = "ggml-base-pt.bin";
        }
        else if (language == "es")
        {
            modelName = "ggml-base-es.bin";
        }
        string modelFilePath = Path.Combine(_modelPath, modelName);

        if (!File.Exists(modelFilePath))
        {
            _logger.LogError("Model file not found: {modelFilePath}", modelFilePath);
            // Fallback to default model
            modelFilePath = Path.Combine(_modelPath, "ggml-base.bin");
        }


        // Transcription with Whisper.NET
        var segments = new List<Whisper.net.SegmentData>();
        using var whisperFactory = WhisperFactory.FromPath(modelFilePath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        using var fileStream = File.OpenRead(wavPath);
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            segments.Add(segment);
        }

        var srtBuilder = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            var currentSegment = segments[i];
            var nextSegment = (i + 1 < segments.Count) ? segments[i + 1] : null;

            srtBuilder.AppendLine((i + 1).ToString());
            srtBuilder.AppendLine($"{FormatTime(currentSegment.Start)} --> {FormatTime(currentSegment.End)}");
            srtBuilder.AppendLine(currentSegment.Text.Trim());
            if (nextSegment != null)
            {
                srtBuilder.AppendLine(@"[" + @"{\fs18}" + nextSegment.Text.Trim() + @"]");
            }
            srtBuilder.AppendLine();
        }

        // Clean up temporary WAV file
        File.Delete(wavPath);

        string srtPath = Path.ChangeExtension(audioPath, ".srt");
        await File.WriteAllTextAsync(srtPath, srtBuilder.ToString());

        return srtPath;
    }

    public async Task<string> RemoveVocalsAsync(string inputAudioPath)
    {
        string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.mp4");

        // Use FFmpeg to remove vocals and encode as AAC in an MP4 container
        var command1 = $"-i \"{inputAudioPath}\" -vn -af pan=stereo|c0=FL-0.5*FC|c1=FR-0.5*FC -c:a aac -ar 44100 \"{instrumentalPath}\"";
        _logger.LogInformation("FFmpeg command: {command}", command1);
        var conversion = await FFmpeg.Conversions.New()
            .AddParameter(command1)
            .Start();

        if (!File.Exists(instrumentalPath) || new FileInfo(instrumentalPath).Length == 0)
        {
            var command2 = $"-i \"{inputAudioPath}\" -vn -af pan=stereo|c0=0.5*FL+0.5*BL+0.3*FC|c1=0.5*FR+0.5*BR+0.3*FC -c:a aac -ar 44100 \"{instrumentalPath}\"";
            _logger.LogInformation("FFmpeg command: {command}", command2);
            var altConversion = await FFmpeg.Conversions.New()
                .AddParameter(command2)
                .Start();
        }

        return instrumentalPath;
    }

    public async Task<string> GenerateBlackVideoWithAudioAndSubtitlesAsync(string instrumentalAudioPath, string srtPath)
    {
        string outputPath = Path.ChangeExtension(instrumentalAudioPath, ".mp4").Replace("_instrumental", "_karaoke");

        var mediaInfo = await FFmpeg.GetMediaInfo(instrumentalAudioPath);
        var duration = mediaInfo.Duration;

        var srtPathForFilter = srtPath.Replace(@"\", @"\\").Replace(":", @"\:");
        var vf = $"subtitles=filename='{srtPathForFilter}':force_style='Alignment=5,Fontsize=24,PrimaryColour=&H00FFFF'";
        var command = $"-f lavfi -i color=c=black:s={_videoWidth}x{_videoHeight}:d={duration.TotalSeconds} -i \"{instrumentalAudioPath}\" -vf \"{vf}\" -c:v libx264 -c:a copy -b:v 2M -preset fast -shortest \"{outputPath}\"";
        _logger.LogInformation("FFmpeg command: {command}", command);
        var conversion = await FFmpeg.Conversions.New()
            .AddParameter(command)
            .Start();

        return outputPath;
    }

    private string FormatTime(TimeSpan ts)
    {
        return ts.ToString(@"hh\:mm\:ss\,fff");
    }
}