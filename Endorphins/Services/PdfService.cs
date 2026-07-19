using Endorphins.Models;

namespace Endorphins.Services;

public sealed class PdfService
{
    public Action<PdfFile>? PdfFileSelected { get; set; }
    public PdfFile SelectedPdfFile { get; private set; }
    public bool HasSelectedPdfFile => !string.IsNullOrEmpty(SelectedPdfFile.Name);

    public void SelectPdfFile(PdfFile pdfFile)
    {
        SelectedPdfFile = pdfFile;
    }
}