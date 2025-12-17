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
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _vocalRemover;

    public AudioProcessorService(IConfiguration configuration, ILogger<AudioProcessorService> logger)
    {
        _modelPath = configuration["Whisper:ModelPath"] ?? "WhisperModels";
        _videoWidth = int.Parse(configuration["Video:Width"] ?? "1280");
        _videoHeight = int.Parse(configuration["Video:Height"] ?? "720");
        _logger = logger;
        _configuration = configuration;
        _vocalRemover = configuration["Audio:VocalRemover"] ?? "ffmpeg";
    }

    public async Task InitializeAsync()
    {
        await Task.Delay(1);
    }

    public async Task<string> GenerateSrtFromAudioAsync(string audioPath, string language)
    {
        // Converte áudio para WAV temporário para o Whisper
        string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        var command = $"-i \"{audioPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavPath}\"";
        _logger.LogInformation("FFmpeg command: {command}", command);
        await FFmpeg.Conversions.New().AddParameter(command).Start();

        // ALTERAÇÃO 1: Melhoria da Precisão
        // Tenta usar o modelo Medium (melhor ortografia) antes do Base.
        // O modelo Medium requer mais RAM, mas erra muito menos.
        string modelName = "ggml-medium.bin";

        // Se quiser especificidade por lingua, pode manter a lógica abaixo,
        // mas o medium multilingue geral já é excelente.
        if (language == "en") modelName = "ggml-medium-en.bin";

        string modelFilePath = Path.Combine(_modelPath, modelName);

        // Fallback para o base se o medium não existir
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

        // Configurações para melhorar a pontuação e contexto
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .WithProbabilities() // Ajuda na precisão interna
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

            // Lógica de mostrar a próxima frase (karaoke style)
            if (nextSegment != null)
            {
                srtBuilder.AppendLine(@"..." + @"{\fs14\c&H808080&}" + nextSegment.Text.Trim() + @"..."); // Diminuí a fonte da próxima frase e mudei pra cinza
            }
            srtBuilder.AppendLine();
        }

        File.Delete(wavPath);
        string srtPath = Path.ChangeExtension(audioPath, ".srt");
        await File.WriteAllTextAsync(srtPath, srtBuilder.ToString());

        return srtPath;
    }

    public async Task<string> RemoveVocalsAsync(string inputAudioPath)
    {
        // (Mantive a lógica original do Demucs/FFmpeg pois estava correta)
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

            if (!File.Exists(noVocalsPath)) throw new Exception("Demucs output missing.");

            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.mp4");
            var command = $"-i \"{noVocalsPath}\" -vn -c:a aac -ar 44100 \"{instrumentalPath}\"";
            await FFmpeg.Conversions.New().AddParameter(command).Start();

            Directory.Delete(separatedPath, true);
            return instrumentalPath;
        }
        else
        {
            string instrumentalPath = Path.ChangeExtension(inputAudioPath, "_instrumental.mp4");
            var command1 = $"-i \"{inputAudioPath}\" -vn -af pan=stereo|c0=FL-0.5*FC|c1=FR-0.5*FC -c:a aac -ar 44100 \"{instrumentalPath}\"";
            await FFmpeg.Conversions.New().AddParameter(command1).Start();
            return instrumentalPath;
        }
    }

    public async Task<string> GenerateBlackVideoWithAudioAndSubtitlesAsync(string instrumentalAudioPath, string srtPath, string? musicTitle = "", string? artistName = "")
    {
        string outputPath = Path.ChangeExtension(instrumentalAudioPath, ".mp4").Replace("_instrumental", "_karaoke");

        // Tratamento do caminho do SRT para o filtro do FFmpeg (escape de caracteres)
        var srtPathForFilter = srtPath.Replace(@"\", @"\\").Replace(":", @"\:");

        // Definir título e artista, ou usar valor padrão se não informados
        var title = string.IsNullOrEmpty(musicTitle?.Trim()) ? "Música" : musicTitle?.Trim();
        var artist = string.IsNullOrEmpty(artistName?.Trim()) ? "Artista desconhecido" : artistName?.Trim();
        string songInfo = $"{title} - {artist}";

        // Estilo da legenda
        // PrimaryColour=&H00FFFF (Amarelo/Ciano) - Formato BGR no ASS/SSA (Hex invertido)
        // Outline=1 (Borda preta para legibilidade sobre as ondas)
        string subtitleStyle = "Alignment=2,Fontsize=24,PrimaryColour=&H00FFFF,Outline=1,BackColour=&H80000000,BorderStyle=3";

        // Estilo do título da música
        string titleStyle = "Alignment=1,Fontsize=32,PrimaryColour=&H00FFFFFF,Outline=1,BackColour=&H80000000,BorderStyle=3";

        // Criar um arquivo temporário SRT com o título da música e artista
        string titleSrtPath = Path.GetTempFileName() + ".srt";
        await CreateTitleSrtFile(titleSrtPath, songInfo, _videoWidth, _videoHeight);

        // Tratamento do caminho do SRT de título para o filtro do FFmpeg (escape de caracteres)
        var titleSrtPathForFilter = titleSrtPath.Replace(@"\", @"\\").Replace(":", @"\:");

        // ALTERAÇÃO 2: Efeitos Visuais (Waveform) com título da música no início do vídeo
        // Usamos filter_complex para:
        // [0:a]showwaves -> Gera as ondas baseadas no som
        // mode=line -> Tipo de onda
        // colors=cyan -> Cor da onda
        // [v]subtitles -> Aplica a legenda principal (letras da música)
        // [title]subtitles -> Aplica a legenda com dados da música no início do vídeo

        var filterComplex = $"[0:a]showwaves=s={_videoWidth}x{_videoHeight}:mode=line:colors=cyan:rate=25,format=yuv420p[waves];[waves]subtitles=filename='{titleSrtPathForFilter}':force_style='{titleStyle}'[titlevid];[titlevid]subtitles=filename='{srtPathForFilter}':force_style='{subtitleStyle}'[outv]";

        var command = $"-i \"{instrumentalAudioPath}\" -filter_complex \"{filterComplex}\" -map \"[outv]\" -map 0:a -c:v libx264 -pix_fmt yuv420p -b:v 2M -preset fast \"{outputPath}\"";

        _logger.LogInformation("FFmpeg command (Visual Effects): {command}", command);

        await FFmpeg.Conversions.New()
            .AddParameter(command)
            .Start();

        // Limpar o arquivo temporário de título
        if (File.Exists(titleSrtPath))
            File.Delete(titleSrtPath);

        return outputPath;
    }

    private async Task CreateTitleSrtFile(string srtPath, string titleText, int videoWidth, int videoHeight)
    {
        // Criar SRT com o título e artista que aparece nos primeiros segundos do vídeo
        // Posiciona o texto no canto superior esquerdo
        string srtContent = $@"1 00:00:00,000 --> 00:00:05,000 {{\pos(100,50)}} {titleText}";
        await File.WriteAllTextAsync(srtPath, srtContent);
    }

    private string FormatTime(TimeSpan ts)
    {
        return ts.ToString(@"hh\:mm\:ss\,fff");
    }
}