using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SlackBingo
{
    public static class WordList
    {
        private static readonly string _wordListUrl = Environment.GetEnvironmentVariable("WordListUrl");
        private static readonly string[] _defaultList = new[]
        {
            "ProWorks",
            "Umbraco",
            "Hackathon",
            "Community",
            "Friendly",
            "OpenSource",
            "Examine",
            "CodeGarden",
            "Heartcore",
            "Uno",
            "Cloud",
            "BackOffice",
            "FrontEnd",
            "CMS",
            "UmbracoForms",
            "Courier",
            "Deploy",
            "Headless",
            "Gridsome",
            "LoadBalancing",
            "JAMstack",
            "Staging",
            "Authoring",
            "Production",
            "Sitemap",
            "Team",
            "Collaboration",
            "Migration",
            "CSS",
            "Hacktoberfest",
            "Unicore",
            "SingleSignOn",
            "VueJS",
            "NoUmbraco9",
            "PreviewAPI",
            "GraphQL",
            "BlockListEditor",
            "Grid",
            "StackedContent",
            "Contentment",
            "WYSIWYG",
            "Documentation",
            "GoldPartner",
            "UmbracoCertified",
            "MVP",
            "CodePatch",
            "DUUGfest",
            "USFest",
            "Retreat",
            "Packages",
            "Roundtable",
            "UmbracoTees",
            "UmbraFriday",
            "Training"
        };

        public static async Task<IEnumerable<string>> GetWordList(ILogger log, CancellationToken token)
        {
            try
            {
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(_wordListUrl, token))
                {
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
                    {
                        log.LogError("Could not retrieve the full word list, using the default instead, status code={code}, body={body}", response.StatusCode, body);
                        return _defaultList;
                    }

                    return body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("//")).ToList();
                }
            }
            catch(Exception ex)
            {
                log.LogError(ex, "Could not retrieve the full word list, using the default instead");
                return _defaultList;
            }
        }
    }
}
