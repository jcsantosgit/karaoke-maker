using Whisper.net;
using Whisper.net.Ggml;
using Xabe.FFmpeg;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class AudioProcessorService
{
    private readonly string _modelPath;
    private readonly string _language;
    private readonly int _videoWidth;
    private readonly int _videoHeight;

    public AudioProcessorService(IConfiguration configuration)
    {
        _modelPath = configuration["Whisper:ModelPath"] ?? "Models/ggml-base.bin";
        _language = configuration["Whisper:Language"] ?? "pt";
        _videoWidth = int.Parse(configuration["Video:Width"] ?? "1280");
        _videoHeight = int.Parse(configuration["Video:Height"] ?? "720");
    }

    public async Task InitializeAsync()
    {
        // FFmpeg should already be available on the system
        // The Xabe.FFmpeg library will use the system's FFmpeg installation
        await Task.Delay(1); // Make method genuinely async to avoid compiler warning
    }

    public async Task<string> GenerateSrtFromAudioAsync(string audioPath)
    {
        // Convert audio to a temporary WAV file for Whisper.net processing
        string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        var wavConversion = await FFmpeg.Conversions.New()
            .AddParameter($"-i \"{audioPath}\" -acodec pcm_s16le -ar 16000 -ac 1 \"{wavPath}\"")
            .Start();

        // Transcription with Whisper.NET
        var srtBuilder = new StringBuilder();
        int subtitleIndex = 1;

        using var whisperFactory = WhisperFactory.FromPath(_modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(_language)
            .Build();

        using var fileStream = File.OpenRead(wavPath);
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            var start = segment.Start;
            var end = segment.End;

            srtBuilder.AppendLine(subtitleIndex.ToString());
            srtBuilder.AppendLine($"{FormatTime(start)} --> {FormatTime(end)}");
            srtBuilder.AppendLine(segment.Text.Trim());
            srtBuilder.AppendLine();
            subtitleIndex++;
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
        var conversion = await FFmpeg.Conversions.New()
            .AddParameter($"-i \"{inputAudioPath}\" -af pan=stereo|c0=FL-0.5*FC|c1=FR-0.5*FC -c:a aac -ar 44100 \"{instrumentalPath}\"")
            .Start();

        if (!File.Exists(instrumentalPath) || new FileInfo(instrumentalPath).Length == 0)
        {
            var altConversion = await FFmpeg.Conversions.New()
                .AddParameter($"-i \"{inputAudioPath}\" -af pan=stereo|c0=0.5*FL+0.5*BL+0.3*FC|c1=0.5*FR+0.5*BR+0.3*FC -c:a aac -ar 44100 \"{instrumentalPath}\"")
                .Start();
        }

        return instrumentalPath;
    }

    public async Task<string> GenerateBlackVideoWithAudioAndSubtitlesAsync(string instrumentalAudioPath, string srtPath)
    {
        string outputPath = Path.ChangeExtension(instrumentalAudioPath, ".mp4").Replace("_instrumental", "_karaoke");

        var mediaInfo = await FFmpeg.GetMediaInfo(instrumentalAudioPath);
        var duration = mediaInfo.Duration;

        var conversion = await FFmpeg.Conversions.New()
            .AddParameter($"-f lavfi -i color=c=black:s={_videoWidth}x{_videoHeight}:d={duration.TotalSeconds} -i \"{instrumentalAudioPath}\" -vf \"subtitles='{srtPath.Replace("'", "'\\''")}'\" -c:v libx264 -c:a copy -b:v 2M -preset fast -shortest \"{outputPath}\"")
            .Start();

        return outputPath;
    }

    private string FormatTime(TimeSpan ts)
    {
        return ts.ToString(@"hh\:mm\:ss\,fff");
    }
}