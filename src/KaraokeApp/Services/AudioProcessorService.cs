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
    private readonly IConfiguration _configuration;
    private readonly string _vocalRemover;
    private readonly int _karaokeFontSize;
    private readonly int _previewFontSize;
    private readonly int _titleFontSize;

    public AudioProcessorService(IConfiguration configuration, ILogger<AudioProcessorService> logger)
    {
        _modelPath = configuration["Whisper:ModelPath"] ?? "WhisperModels";
        _videoWidth = int.Parse(configuration["Video:Width"] ?? "1280");
        _videoHeight = int.Parse(configuration["Video:Height"] ?? "720");
        _backgroundImagePath = configuration["Video:BackgroundImage"];
        _logger = logger;
        _configuration = configuration;
        _vocalRemover = configuration["Audio:VocalRemover"] ?? "ffmpeg";
        _karaokeFontSize = int.Parse(configuration["Subtitles:KaraokeFontSize"] ?? "52");
        _previewFontSize = int.Parse(configuration["Subtitles:PreviewFontSize"] ?? "28");
        _titleFontSize = int.Parse(configuration["Subtitles:TitleFontSize"] ?? "36");        
    }

    public async Task InitializeAsync()
    {
        await Task.Delay(1);
    }

    public async Task<string> GenerateAssFromAudioAsync(string audioPath, string language)
    {
        // Converte áudio para WAV temporário para o Whisper (16kHz mono)
        string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        var command = $"-i \"{audioPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavPath}\"";
        _logger.LogInformation("FFmpeg command for Whisper prep: {command}", command);
        await FFmpeg.Conversions.New().AddParameter(command).Start();

        // CORREÇÃO: Usar sempre o modelo Multilíngue (ggml-medium.bin)
        // Isso evita erros ao buscar modelos '-en' que podem não existir e unifica a qualidade.
        string modelName = "ggml-medium.bin";
        string modelFilePath = Path.Combine(_modelPath, modelName);

        if (!File.Exists(modelFilePath))
        {
            _logger.LogWarning("Modelo Medium não encontrado em {path}. Usando fallback para Base.", modelFilePath);
            modelFilePath = Path.Combine(_modelPath, "ggml-base.bin");
        }

        if (!File.Exists(modelFilePath))
        {
            throw new FileNotFoundException($"Nenhum modelo Whisper encontrado em {_modelPath}. Baixe o ggml-medium.bin.");
        }

        var segments = new List<Whisper.net.SegmentData>();
        using var whisperFactory = WhisperFactory.FromPath(modelFilePath);

        // CORREÇÃO: Prompt dinâmico baseado no idioma.
        // Isso "força" o Whisper a manter o idioma correto e evita traduções acidentais.
        string prompt = "Lyrics of a song";
        switch (language)
        {
            case "pt": prompt = "Letra de uma música, transcrição fiel."; break;
            case "es": prompt = "Letra de una canción, transcripción fiel."; break;
            case "en": prompt = "Lyrics of a song, accurate transcription."; break;
        }

        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .WithPrompt(prompt)
            .WithProbabilities()
            .Build();

        using var fileStream = File.OpenRead(wavPath);
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            segments.Add(segment);
        }

        File.Delete(wavPath);

        // Criar arquivo ASS ao invés de SRT
        string assPath = Path.ChangeExtension(audioPath, ".ass");
        await CreateAssFileFromSegments(assPath, segments);

        return assPath;
    }

    private async Task CreateAssFileFromSegments(string assPath, List<Whisper.net.SegmentData> segments)
    {
        var assBuilder = new StringBuilder();

        // Cabeçalho ASS
        assBuilder.AppendLine("[Script Info]");
        assBuilder.AppendLine("Title: Karaoke Video");
        assBuilder.AppendLine("ScriptType: v4.00+");
        assBuilder.AppendLine("WrapStyle: 0");
        assBuilder.AppendLine("ScaledBorderAndShadow: yes");
        assBuilder.AppendLine("YCbCr Matrix: None");
        assBuilder.AppendLine();

        // Estilos
        assBuilder.AppendLine("[V4+ Styles]");
        assBuilder.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        
        // Estilo principal do karaoke
        assBuilder.AppendLine($"Style: Karaoke,Arial,{_karaokeFontSize},&H00FFFFFF,&H0000FFFF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3,0,2,30,30,50,1");
        
        // Estilo para próxima linha (preview)
        assBuilder.AppendLine($"Style: Preview,Arial,{_previewFontSize},&H00808080,&H00808080,&H00000000,&H80000000,0,0,0,0,100,100,0,0,1,2,0,2,30,30,100,1");
        assBuilder.AppendLine();

        // Eventos
        assBuilder.AppendLine("[Events]");
        assBuilder.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        for (int i = 0; i < segments.Count; i++)
        {
            var currentSegment = segments[i];
            var nextSegment = (i + 1 < segments.Count) ? segments[i + 1] : null;

            // Linha principal com efeito karaoke
            string dialogue = $"Dialogue: 0,{FormatAssTime(currentSegment.Start)},{FormatAssTime(currentSegment.End)},Karaoke,,0,0,0,,";
            
            string[] words = currentSegment.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double totalDuration = (currentSegment.End - currentSegment.Start).TotalSeconds;
            
            if (words.Length > 0)
            {
                double timePerWord = totalDuration / words.Length;
                foreach (var word in words)
                {
                    int duration = (int)(timePerWord * 100);
                    dialogue += $"{{\\k{duration}}}{word} ";
                }
            }
            else
            {
                 dialogue += currentSegment.Text;
            }

            assBuilder.AppendLine(dialogue.TrimEnd());

            // Mostrar preview da próxima linha
            if (nextSegment != null)
            {
                string preview = $"Dialogue: 0,{FormatAssTime(currentSegment.Start)},{FormatAssTime(currentSegment.End)},Preview,,0,0,0,,{nextSegment.Text.Trim()}";
                assBuilder.AppendLine(preview);
            }
        }

        await File.WriteAllTextAsync(assPath, assBuilder.ToString());
    }

    public async Task<(string instrumental, string vocals)> RemoveVocalsAsync(string inputAudioPath)
    {
        if (_vocalRemover == "demucs")
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "demucs",
                    Arguments = $"--two-stems=vocals \"{inputAudioPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception($"Demucs failed: {error}");

            var separatedPath = Path.Combine("separated", "htdemucs", Path.GetFileNameWithoutExtension(inputAudioPath));
            var noVocalsPath = Path.Combine(separatedPath, "no_vocals.wav");
            var vocalsPath = Path.Combine(separatedPath, "vocals.wav");

            if (!File.Exists(noVocalsPath)) throw new Exception("Demucs output missing (no_vocals).");
            
            // Caminhos finais de saída
            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.mp4");
            string cleanVocalsPath = Path.ChangeExtension(inputAudioPath, "_vocals_clean.wav");

            // 1. Converter Instrumental
            var commandInst = $"-i \"{noVocalsPath}\" -vn -c:a aac -ar 44100 \"{instrumentalPath}\"";
            await FFmpeg.Conversions.New().AddParameter(commandInst).Start();

            // 2. Salvar Vocais Limpos (para usar no Whisper)
            if (File.Exists(vocalsPath))
            {
                var commandVocals = $"-i \"{vocalsPath}\" -vn -acodec pcm_s16le \"{cleanVocalsPath}\"";
                await FFmpeg.Conversions.New().AddParameter(commandVocals).Start();
            }
            else
            {
                File.Copy(inputAudioPath, cleanVocalsPath, true);
            }

            // Limpar pasta do Demucs
            Directory.Delete(separatedPath, true);
            
            return (instrumentalPath, cleanVocalsPath);
        }
        else
        {
            // Fallback FFmpeg
            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.mp4");
            var command1 = $"-i \"{inputAudioPath}\" -vn -af pan=stereo|c0=FL-0.5*FC|c1=FR-0.5*FC -c:a aac -ar 44100 \"{instrumentalPath}\"";
            await FFmpeg.Conversions.New().AddParameter(command1).Start();
            
            // No modo FFmpeg simples, retornamos o áudio original como "vocals"
            return (instrumentalPath, inputAudioPath);
        }
    }

    public async Task<string> GenerateVideoWithAudioAndSubtitlesAsync(string instrumentalAudioPath, string assPath, string? musicTitle = "", string? artistName = "")
    {
        string outputPath = Path.ChangeExtension(instrumentalAudioPath, ".mp4").Replace("_instrumental", "_karaoke");

        var assPathForFilter = assPath.Replace(@"\", @"\\").Replace(":", @"\:");

        var title = string.IsNullOrEmpty(musicTitle?.Trim()) ? "Música" : musicTitle?.Trim();
        var artist = string.IsNullOrEmpty(artistName?.Trim()) ? "Artista desconhecido" : artistName?.Trim();
        string songInfo = $"{title} - {artist}";

        string titleAssPath = Path.GetTempFileName() + ".ass";
        await CreateTitleAssFile(titleAssPath, songInfo, _videoWidth, _videoHeight);

        var titleAssPathForFilter = titleAssPath.Replace(@"\", @"\\").Replace(":", @"\:");

        var mediaInfo = await FFmpeg.GetMediaInfo(instrumentalAudioPath);
        var duration = mediaInfo.Duration;

        string command;
        string filterComplex;

        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            filterComplex = $"[0:v]scale={_videoWidth}:{_videoHeight}[bg];[bg]ass='{titleAssPathForFilter}'[v_title];[v_title]ass='{assPathForFilter}'[outv]";
            command = $"-loop 1 -i \"{_backgroundImagePath}\" -i \"{instrumentalAudioPath}\" -filter_complex \"{filterComplex}\" -map \"[outv]\" -map 1:a -c:v libx264 -pix_fmt yuv420p -b:v 2M -preset fast -shortest \"{outputPath}\"";
        }
        else
        {
            filterComplex = $"[0:v]ass='{titleAssPathForFilter}'[v_title];[v_title]ass='{assPathForFilter}'[outv]";
            command = $"-f lavfi -i color=c=black:s={_videoWidth}x{_videoHeight}:d={duration.TotalSeconds} -i \"{instrumentalAudioPath}\" -filter_complex \"{filterComplex}\" -map \"[outv]\" -map 1:a -c:v libx264 -pix_fmt yuv420p -b:v 2M -preset fast -shortest \"{outputPath}\"";
        }

        _logger.LogInformation("FFmpeg command (Visual Effects): {command}", command);

        await FFmpeg.Conversions.New()
            .AddParameter(command)
            .Start();

        if (File.Exists(titleAssPath))
            File.Delete(titleAssPath);

        return outputPath;
    }

    private async Task CreateTitleAssFile(string assPath, string titleText, int videoWidth, int videoHeight)
    {
        var assBuilder = new StringBuilder();

        assBuilder.AppendLine("[Script Info]");
        assBuilder.AppendLine("Title: Song Title");
        assBuilder.AppendLine("ScriptType: v4.00+");
        assBuilder.AppendLine();

        assBuilder.AppendLine("[V4+ Styles]");
        assBuilder.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        assBuilder.AppendLine($"Style: Title,Arial,{_titleFontSize},&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3,0,7,50,10,50,1");
        assBuilder.AppendLine();

        assBuilder.AppendLine("[Events]");
        assBuilder.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        assBuilder.AppendLine($"Dialogue: 0,0:00:00.00,0:00:05.00,Title,,0,0,0,,{titleText}");

        await File.WriteAllTextAsync(assPath, assBuilder.ToString());
    }

    private string FormatAssTime(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
    }
}