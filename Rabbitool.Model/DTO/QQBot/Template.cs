using QQChannelFramework.Models.MessageModels;

namespace Rabbitool.Model.DTO.QQBot;

public class MarkdownTemplateParams
{
    public required string Info { get; set; }
    public required string From { get; set; }
    public required string Text { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }

    public MarkdownTemplateParams? Origin { get; set; }

    public List<MessageMarkdownParams> ToMessageMarkdownParams()
    {
        List<MessageMarkdownParams> messageParams = new()
        {
            new MessageMarkdownParams("info", Info),
            new MessageMarkdownParams("from", From),
            new MessageMarkdownParams("text", Text.Replace("\n", "\u200B")),
            new MessageMarkdownParams("link", Url)
        };
        if (ImageUrl != null)
            messageParams.Add(new MessageMarkdownParams("image", Text));
        if (Origin?.Info != null)
            messageParams.Add(new MessageMarkdownParams("origin_info", Origin.Info));
        if (Origin?.From != null)
            messageParams.Add(new MessageMarkdownParams("origin_from", Origin.From));
        if (Origin?.Text != null)
            messageParams.Add(new MessageMarkdownParams("origin_text", Origin.Text.Replace("\n", "\u200B")));
        if (Origin?.Url != null)
            messageParams.Add(new MessageMarkdownParams("origin_link", Origin.Url));
        if (Origin?.ImageUrl != null)
            messageParams.Add(new MessageMarkdownParams("origin_image", Origin.ImageUrl));

        return messageParams;
    }
}