namespace Rabbitool.Model.DTO.Bilibili;

public enum LiveStatusEnum
{
    Streaming = 1,
    NoLiveStream = 0,
    Replay = 2
}

public class Live
{
    public required uint Uid { get; set; }
    public required string Uname { get; set; }
    public required uint RoomId { get; set; }

    public required LiveStatusEnum LiveStatus { get; set; }
    public DateTime? LiveStartTime { get; set; }
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
}
