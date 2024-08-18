using System.Net;
using System.Text;
using System.Xml;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace CatBot.Music.NhacCuaTui
{
    internal static class NhacCuaTuiSearch
    {
        static readonly string searchLink = "https://www.nhaccuatui.com/tim-kiem/bai-hat?q=";

        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
          if (linkOrKeyword.StartsWith(NhacCuaTuiMusic.nhacCuaTuiLink))
            {
                XmlDocument xmlDoc = NhacCuaTuiMusic.GetXML(linkOrKeyword);
                return new List<SearchResult>()
                {
                    new SearchResult(linkOrKeyword, $"{xmlDoc.DocumentElement["track"].SelectSingleNode("title").InnerText}", xmlDoc.DocumentElement["track"].SelectSingleNode("creator").InnerText, "", xmlDoc.DocumentElement["track"].SelectSingleNode("avatar").InnerText)
                };
            }
            string html = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(searchLink + Uri.EscapeDataString(linkOrKeyword));
            HtmlParser parser = new HtmlParser();
            IHtmlDocument document = parser.ParseDocument(html);
            var node1 = document.Body.Children.First(n => n.GetType().GetInterface(nameof(IHtmlDivElement)) != null && ((IHtmlDivElement)n).ClassName == "box-content");
            var node2 = node1.Children[0].Children[0].Children[0].Children[3].Children[0];
            var nodes = node2.Children.Where(n => n.ClassName == "sn_search_single_song");
            var result = new List<SearchResult>();
            foreach (IElement element in nodes)
            {
                string thumbnailLink = ((IHtmlImageElement)element.Children[0].Children[0]).Dataset["src"];
                if (string.IsNullOrWhiteSpace(thumbnailLink))
                    thumbnailLink = ((IHtmlImageElement)element.Children[0].Children[0]).Source;
                result.Add(new SearchResult("ID: " + NhacCuaTuiMusic.GetSongID(((IHtmlAnchorElement)element.Children[0]).Href), ((IHtmlAnchorElement)element.Children[1].Children[0].Children[0]).Text, string.Join(", ", element.Children[1].Children[1].Children.Select(n => ((IHtmlAnchorElement)n).Text)), string.Join(", ", element.Children[1].Children[1].Children.Select(n => ((IHtmlAnchorElement)n).Href)), thumbnailLink));
                if (result.Count == count)
                    break;
            }
            return result;
        }
    }
}
