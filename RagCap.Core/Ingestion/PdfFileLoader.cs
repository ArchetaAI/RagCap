using System.Text;
using UglyToad.PdfPig;

namespace RagCap.Core.Ingestion
{
    public class PdfFileLoader : IFileLoader
    {
        public string LoadContent(string filePath)
        {
            var sb = new StringBuilder();

            using (var document = PdfDocument.Open(filePath))
            {
                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);

                    // Use PdfPig's built-in page text extraction
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text.Trim());
                    }

                    // Separate pages by a blank line to aid paragraph chunking
                    if (i < document.NumberOfPages)
                    {
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}
