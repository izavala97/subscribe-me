using Markdig;

namespace suscribe_me.Services;

/// <summary>
/// Service for rendering Markdown content to HTML
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Convert markdown text to HTML
    /// </summary>
    string ToHtml(string markdown);
}

/// <summary>
/// Markdig implementation for Markdown rendering
/// </summary>
public class MarkdigService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdigService()
    {
        // Configure Markdig with common extensions
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()  // Tables, task lists, auto-links, etc.
            .UseEmojiAndSmiley()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();
    }

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;
        
        return Markdown.ToHtml(markdown, _pipeline);
    }
}
