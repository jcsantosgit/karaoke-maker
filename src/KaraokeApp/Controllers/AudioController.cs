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
        string assPath = null; // Mudado de srtPath para assPath
        try
        {
            // Step 1: Gerar arquivo ASS com efeito karaoke do áudio
            assPath = await _service.GenerateAssFromAudioAsync(audioPath, language);

            // Step 2: Remover vocais para criar faixa instrumental
            instrumentalPath = await _service.RemoveVocalsAsync(audioPath);

            // Step 3: Gerar vídeo com fundo, áudio instrumental e legendas ASS com karaoke
            string outputPath = await _service.GenerateVideoWithAudioAndSubtitlesAsync(instrumentalPath, assPath, musicTitle, artistName);

            // Limpar arquivos temporários
            System.IO.File.Delete(audioPath);
            System.IO.File.Delete(assPath);
            System.IO.File.Delete(instrumentalPath);

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

            // Retornar o vídeo processado para download
            var fileName = $"{artistAndMusic}.mp4";
            return PhysicalFile(outputPath, "video/mp4", fileName);
        }
        catch (Exception ex)
        {
            // Limpar arquivos temporários em caso de erro
            if (System.IO.File.Exists(audioPath)) System.IO.File.Delete(audioPath);
            if (assPath != null && System.IO.File.Exists(assPath)) System.IO.File.Delete(assPath);
            if (instrumentalPath != null && System.IO.File.Exists(instrumentalPath)) System.IO.File.Delete(instrumentalPath);

            Console.WriteLine($"Error processing audio: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return BadRequest($"Erro no processamento: {ex.Message}");
        }
    }
}