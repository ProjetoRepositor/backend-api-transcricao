namespace backend_api_transcricao.Models;

public class RootObject
{
    public string jobName { get; set; }
    public string accountId { get; set; }
    public string status { get; set; }
    public Results results { get; set; }
}

public class Results
{
    public Transcripts[] transcripts { get; set; }
    public Items[] items { get; set; }
}

public class Transcripts
{
    public string transcript { get; set; }
}

public class Items
{
    public string type { get; set; }
    public Alternatives[] alternatives { get; set; }
    public string start_time { get; set; }
    public string end_time { get; set; }
}

public class Alternatives
{
    public string confidence { get; set; }
    public string content { get; set; }
}

