using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using KaraokeApp.Models;
using System;

public class AudioController : Controller
{
    private readonly AudioProcessorService _service;
    private readonly IWebHostEnvironment _env;

    public AudioController(AudioProcessorService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [RequestSizeLimit(500_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    public async Task<IActionResult> Upload(IFormFile audioFile, string language, string? musicTitle, string? artistName)
    {
        if (audioFile == null || audioFile.Length == 0 || !audioFile.FileName.EndsWith(".mp3"))
            return BadRequest("Selecione um arquivo MP3.");

        string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(audioFile.FileName)}";
        string audioPath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(audioPath, FileMode.Create))
        {
            await audioFile.CopyToAsync(stream);
        }

        string instrumentalPath = null;
        string vocalsPath = null; // Caminho para os vocais limpos
        string assPath = null;
        string outputPath = null;

        try
        {
            // MUDANÇA PRINCIPAL: Separa vocais PRIMEIRO para garantir transcrição limpa
            // Step 1: Separar vocais e instrumental
            (instrumentalPath, vocalsPath) = await _service.RemoveVocalsAsync(audioPath);

            // Step 2: Gerar arquivo ASS usando APENAS a faixa de voz limpa
            // Isso evita que bateria e instrumentos confundam o Whisper
            assPath = await _service.GenerateAssFromAudioAsync(vocalsPath, language);

            // Step 3: Gerar vídeo final com fundo, áudio instrumental e legendas
            outputPath = await _service.GenerateVideoWithAudioAndSubtitlesAsync(instrumentalPath, assPath, musicTitle, artistName);

            // Limpar arquivos temporários
            if (System.IO.File.Exists(audioPath)) System.IO.File.Delete(audioPath);
            if (System.IO.File.Exists(vocalsPath)) System.IO.File.Delete(vocalsPath); // Deleta a voz extraída
            if (System.IO.File.Exists(assPath)) System.IO.File.Delete(assPath);
            if (System.IO.File.Exists(instrumentalPath)) System.IO.File.Delete(instrumentalPath);

            var musicNormalized = TextNormalizer.NormalizeText(musicTitle);
            var artistNormalized = TextNormalizer.NormalizeText(artistName);
            string artistAndMusic = null;

            if(string.IsNullOrEmpty(musicNormalized) && string.IsNullOrEmpty(artistNormalized))
            {
                artistAndMusic = Path.GetFileNameWithoutExtension(outputPath);
            }
            else if(string.IsNullOrEmpty(musicNormalized))
            {
                artistAndMusic = $"{artistNormalized}";
            }
            else if(string.IsNullOrEmpty(artistNormalized))
            {
                artistAndMusic = $"{musicNormalized}";
            }
            else
            {
                artistAndMusic = $"{artistNormalized}-{musicNormalized}";
            }

            var fileName = $"{artistAndMusic}.mp4";
            return PhysicalFile(outputPath, "video/mp4", fileName);
        }
        catch (Exception ex)
        {
            // Limpeza de emergência
            if (System.IO.File.Exists(audioPath)) System.IO.File.Delete(audioPath);
            if (vocalsPath != null && System.IO.File.Exists(vocalsPath)) System.IO.File.Delete(vocalsPath);
            if (assPath != null && System.IO.File.Exists(assPath)) System.IO.File.Delete(assPath);
            if (instrumentalPath != null && System.IO.File.Exists(instrumentalPath)) System.IO.File.Delete(instrumentalPath);

            Console.WriteLine($"Error processing audio: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return BadRequest($"Erro no processamento: {ex.Message}");
        }
    }
}