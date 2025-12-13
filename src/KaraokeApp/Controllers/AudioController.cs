using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using KaraokeApp.Models;

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
    public async Task<IActionResult> Upload(IFormFile audioFile)
    {
        if (audioFile == null || audioFile.Length == 0 || (!audioFile.FileName.EndsWith(".mp3") && !audioFile.FileName.EndsWith(".mp4")))
            return BadRequest("Selecione um arquivo MP3 ou MP4.");

        string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);
        string audioPath = Path.Combine(uploadsFolder, audioFile.FileName);

        using (var stream = new FileStream(audioPath, FileMode.Create))
        {
            await audioFile.CopyToAsync(stream);
        }

        string instrumentalPath = null;
        string srtPath = null;
        try
        {
            // Step 1: Generate SRT from audio
            srtPath = await _service.GenerateSrtFromAudioAsync(audioPath);

            // Step 2: Remove vocals to create instrumental track
            instrumentalPath = await _service.RemoveVocalsAsync(audioPath);

            // Step 3: Generate video with black background, instrumental audio and subtitles
            string outputPath = await _service.GenerateBlackVideoWithAudioAndSubtitlesAsync(instrumentalPath, srtPath);

            // Clean up temporary files
            System.IO.File.Delete(audioPath);
            System.IO.File.Delete(srtPath);
            System.IO.File.Delete(instrumentalPath);

            // Return the processed video file for download
            var fileName = Path.GetFileName(outputPath);
            return PhysicalFile(outputPath, "video/mp4", fileName);
        }
        catch (Exception ex)
        {
            // Clean up any temporary files in case of error
            if (System.IO.File.Exists(audioPath)) System.IO.File.Delete(audioPath);
            if (srtPath != null && System.IO.File.Exists(srtPath)) System.IO.File.Delete(srtPath);
            if (instrumentalPath != null && System.IO.File.Exists(instrumentalPath)) System.IO.File.Delete(instrumentalPath);

            // Log the error or return a more detailed error response
            Console.WriteLine($"Error processing audio: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return BadRequest($"Erro no processamento: {ex.Message}");
        }
    }
}
