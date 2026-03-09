using System.ComponentModel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MonocoBot.Tools;

public class PdfTools
{
    [Description("Creates a PDF document with the given title and content. " +
        "Content supports simple markdown: '# ' for headings, '## ' for subheadings, " +
        "'- ' or '* ' for bullet points, and plain text for paragraphs. " +
        "Returns a confirmation message. The PDF file will be attached to the Discord message automatically.")]
    public string CreatePdf(
        [Description("The title of the PDF document")] string title,
        [Description("The content using simple markdown: '# ' headings, '## ' subheadings, '- ' bullet points")] string content)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var filePath = Path.Combine(Path.GetTempPath(), $"{SanitizeFileName(title)}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .PaddingBottom(10)
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Medium)
                    .Text(title)
                    .FontSize(24)
                    .Bold()
                    .FontColor(Colors.Blue.Darken3);

                page.Content().PaddingTop(15).Column(column =>
                {
                    column.Spacing(4);
                    foreach (var line in content.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                        {
                            column.Item().PaddingTop(5);
                            continue;
                        }

                        if (trimmed.StartsWith("## "))
                        {
                            column.Item().PaddingTop(10).Text(trimmed[3..]).FontSize(16).SemiBold();
                        }
                        else if (trimmed.StartsWith("# "))
                        {
                            column.Item().PaddingTop(12).Text(trimmed[2..]).FontSize(18).Bold();
                        }
                        else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                        {
                            column.Item().Row(row =>
                            {
                                row.ConstantItem(20).Text("\u2022").FontSize(12);
                                row.RelativeItem().Text(trimmed[2..]).FontSize(12);
                            });
                        }
                        else
                        {
                            column.Item().Text(trimmed).FontSize(12);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf(filePath);

        ToolOutput.AddFile(filePath);
        return $"PDF document '{title}' has been created and will be attached to the message.";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 80 ? sanitized[..80] : sanitized;
    }
}
