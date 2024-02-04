using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Microsoft.AspNetCore.Mvc;

namespace backend_transcricao.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadAudio(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                Message = "Nenhum arquivo enviado",
            });
        }

        // Corrija o Content-Type esperado para "audio/wave" ou "audio/wav", dependendo do que você está recebendo.
        var allowedContentTypes = new[] { "audio/wave", "audio/wav" };
        if (!allowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new
            {
                Message = "O arquivo deve ser do tipo WAV."
            });
        }

        using var memoryStream = new MemoryStream();

        await file.CopyToAsync(memoryStream);
        Console.WriteLine($"Tamanho do arquivo WAV recebido: {memoryStream.Length} bytes");

        // Configuração das credenciais e cliente do AWS Transcribe
        var transcribeClient = new AmazonTranscribeServiceClient();

        // Preparação da solicitação de transcrição
        var jobName = Guid.NewGuid().ToString(); // Nome único para o trabalho de transcrição

        var bucketName = "tcccarrinhointeligentetranscribe";

        var s3Key = $"{jobName}/{file.FileName}";

        var mediaFileUri = $"s3://{bucketName}/{s3Key}"; // Substitua "bucket-name" pelo seu bucket S3
        var mediaFormat = "wav";

        var s3Client = new AmazonS3Client();
        
        var s3TransferUtility = new TransferUtility(s3Client);
        await s3TransferUtility.UploadAsync(new MemoryStream(memoryStream.ToArray()), bucketName, s3Key);

        var startTranscriptionRequest = new StartTranscriptionJobRequest
        {
            LanguageCode = LanguageCode.PtBR, // Substitua pelo código de idioma desejado
            Media = new Media
            {
                MediaFileUri = mediaFileUri
            },
            TranscriptionJobName = jobName,
            MediaFormat = mediaFormat,
        };

        // Inicia o trabalho de transcrição
        await transcribeClient.StartTranscriptionJobAsync(startTranscriptionRequest);

        var transcriptionStatus = await WaitForTranscriptionCompletionAsync(transcribeClient, jobName);
        
        if (transcriptionStatus == TranscriptionJobStatus.COMPLETED)
        {
            // Recupera os resultados da transcrição
            var transcriptionResults = await transcribeClient.GetTranscriptionJobAsync(new GetTranscriptionJobRequest
            {
                TranscriptionJobName = jobName
            });

            var transcriptFileUri = transcriptionResults.TranscriptionJob.Transcript.TranscriptFileUri;
            // Aqui você pode processar ou retornar os resultados da transcrição
            Console.WriteLine($"Resultados da transcrição disponíveis em: {transcriptFileUri}");
            return Ok(new
            {
                Message = "Transcrição concluída. Resultados disponíveis em: " + transcriptFileUri
            });
        }

        return StatusCode(500, new
        {
            Message = "A transcrição falhou"
        });
    }
    
    private async Task<TranscriptionJobStatus> WaitForTranscriptionCompletionAsync(AmazonTranscribeServiceClient transcribeClient, string jobName)
    {
        while (true)
        {
            var transcriptionJob = await transcribeClient.GetTranscriptionJobAsync(new GetTranscriptionJobRequest
            {
                TranscriptionJobName = jobName
            });

            var status = transcriptionJob.TranscriptionJob.TranscriptionJobStatus;

            if (status == TranscriptionJobStatus.COMPLETED || status == TranscriptionJobStatus.FAILED)
            {
                return status;
            }

            await Task.Delay(1000); // Aguarda 1 segundos antes de verificar novamente o status
        }
    }
}
