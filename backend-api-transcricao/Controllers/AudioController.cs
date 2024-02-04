using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using backend_api_transcricao.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

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
            
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, transcriptFileUri);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<RootObject>(await response.Content.ReadAsStringAsync());
            
            var responseObject = ProcessarComando(result!.results.transcripts[0].transcript);

            if (responseObject is null)
            {
                return NotFound(new
                {
                    Message = "Nenhum comando encontrado",
                    Text = result!.results.transcripts[0].transcript,
                });
            }
            
            return Ok(responseObject);
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
    
    private object? ProcessarComando(string comando)
    {
        // Regex para identificar "dúzia" e outros casos especiais
        var matchEspecial = Regex.Match(comando, @"Adicionar (\w+) (d\wzia) de ([\w\s]+)");
        var matchNumeroComposto = Regex.Match(comando, @"Adicionar (\w+) e (\w+) ([\w\s]+)");

        if (matchEspecial.Success)
        {
            string quantidade = matchEspecial.Groups[1].Value;
            string unidade = matchEspecial.Groups[2].Value;
            string item = matchEspecial.Groups[3].Value;

            int quantidadeFormatada = ConverterQuantidadeTextoParaNumero(quantidade);

            // Converter a quantidade com base na unidade
            quantidadeFormatada = ConverterQuantidadeEspecialParaNumero(quantidadeFormatada, unidade);
            return new
            {
                Nome = item,
                Quantidade = quantidadeFormatada,
            };
        }
        else if (matchNumeroComposto.Success)
        {
            int dezena = ConverterQuantidadeTextoParaNumero(matchNumeroComposto.Groups[1].Value);
            int unidade= ConverterQuantidadeTextoParaNumero(matchNumeroComposto.Groups[2].Value);

            int quantidade = dezena+unidade;
            string item = matchNumeroComposto.Groups[3].Value;

            // Converter a quantidade com base na unidade
            return new
            {
                Nome = item,
                Quantidade = quantidade,
            };
        }
        
        // Regex para identificar comandos comuns
        var matchComum = Regex.Match(comando, @"Adicionar (\w+) ([\w\s]+)");
        if (matchComum.Success)
        {
            string quantidadeTexto = matchComum.Groups[1].Value;
            string item = matchComum.Groups[2].Value;

            int quantidade = ConverterQuantidadeTextoParaNumero(quantidadeTexto);
            return new
            {
                Nome = "Item",
                Quantidade = quantidade,
            };
            
        }

        return null;
    }
    
    
    private int ConverterQuantidadeTextoParaNumero(string quantidadeTexto)
    {
        var numeros = new Dictionary<string, int>
        {
            { "um", 1 }, { "uma", 1 },
            { "dois", 2 }, { "duas", 2 },
            { "três", 3 }, { "tres", 3 },
            { "quatro", 4 },
            { "cinco", 5 },
            { "seis", 6 },
            { "sete", 7 },
            { "oito", 8 },
            { "nove", 9 },
            { "dez", 10 },
            { "vinte", 20 },
            { "trinta", 30 },
            { "quarenta", 40 },
            { "cinquenta", 50 }
            // Continue adicionando conforme necessário
        };

        return numeros.TryGetValue(quantidadeTexto, out int numero) ? numero : 0;
    }

    private int ConverterQuantidadeEspecialParaNumero(int quantidade, string unidade)
    {
        // Dicionário para unidades especiais
        var unidades = new Dictionary<string, int>
        {
            { "dúzia", 12 },
            { "duzia", 12 },
            // Adicione mais unidades conforme necessário
        };

        return unidades.TryGetValue(unidade, out int valorUnidade) ? quantidade * valorUnidade : quantidade;
    }
    
}
