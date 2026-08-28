using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace BitLCDMarqueeStudio
{
    internal sealed class ResourceSearchService
    {
        private readonly string _rootDir;
        private readonly string _resourcesDir;
        private readonly string _cacheDir;
        private readonly JavaScriptSerializer _json;

        public ResourceSearchService()
        {
            _rootDir = AppDomain.CurrentDomain.BaseDirectory;
            _resourcesDir = FindResourcesDirectory(_rootDir);
            _cacheDir = Path.Combine(_rootDir, "cache", "resources");
            Directory.CreateDirectory(_cacheDir);
            _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public IList<ResourceResult> SearchJukebox(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            AddProviderResults(results, "Discogs", delegate { return SearchDiscogs(request); });
            AddProviderResults(results, "MusicBrainz", delegate { return SearchMusicBrainz(request); });
            AddProviderResults(results, "FanArt.tv", delegate { return SearchFanArt(request); });
            if (results.Count == 0)
            {
                results.Add(new ResourceResult
                {
                    Source = "Search",
                    ResourceType = "status",
                    Label = "No resources were returned by the enabled providers.",
                    Score = 0
                });
            }
            List<ResourceResult> ordered = results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Source)
                .ThenBy(r => r.ResourceType)
                .ToList();
            CacheArtwork(ordered);
            return ordered;
        }

        public IList<ResourceResult> SearchArcade(ArcadeSearchRequest request)
        {
            var results = new List<ResourceResult>();
            AddProviderResults(results, "ScreenScraper", delegate { return SearchScreenScraper(request); });
            if (results.Count == 0)
            {
                results.Add(new ResourceResult
                {
                    Source = "Search",
                    ResourceType = "status",
                    Label = "No arcade artwork resources were returned.",
                    Score = 0
                });
            }

            List<ResourceResult> ordered = results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Source)
                .ThenBy(r => r.ResourceType)
                .ToList();
            CacheArtwork(ordered);
            return ordered;
        }

        private static void AddProviderResults(ICollection<ResourceResult> results, string providerName, Func<IEnumerable<ResourceResult>> provider)
        {
            try
            {
                foreach (ResourceResult result in provider())
                {
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                results.Add(new ResourceResult
                {
                    Source = providerName,
                    ResourceType = "error",
                    Label = ex.Message,
                    Score = -1
                });
            }
        }

        private IEnumerable<ResourceResult> SearchMusicBrainz(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            string query = string.Format("artist:\"{0}\" AND recording:\"{1}\"", EscapeMusicBrainzQuery(request.Artist), EscapeMusicBrainzQuery(request.Title));
            if (!string.IsNullOrWhiteSpace(request.AlbumOrRelease))
            {
                query += string.Format(" AND release:\"{0}\"", EscapeMusicBrainzQuery(request.AlbumOrRelease));
            }

            string url = "https://musicbrainz.org/ws/2/recording/?query=" + Uri.EscapeDataString(query) + "&fmt=json&limit=10";
            Dictionary<string, object> root;
            try
            {
                root = GetJson(url, new Dictionary<string, string>
                {
                    { "User-Agent", "BitLCDMarqueeStudio/0.1 (personal-use metadata lookup)" }
                });
            }
            catch (WebException ex)
            {
                results.Add(new ResourceResult
                {
                    Source = "MusicBrainz",
                    ResourceType = "status",
                    Label = "MusicBrainz is temporarily unavailable. Other artwork providers can still be used.",
                    Detail = DescribeWebException(ex),
                    Score = -1
                });
                return results;
            }

            foreach (Dictionary<string, object> recording in GetArray(root, "recordings"))
            {
                string title = GetString(recording, "title");
                string artist = GetArtistCredit(recording);
                string release = GetFirstReleaseTitle(recording);
                int score = ScoreName(title, request.Title) + ScoreName(artist, request.Artist);
                if (!string.IsNullOrWhiteSpace(request.AlbumOrRelease))
                {
                    score += ScoreName(release, request.AlbumOrRelease);
                }

                results.Add(new ResourceResult
                {
                    Source = "MusicBrainz",
                    ResourceType = "metadata",
                    Label = title,
                    Detail = string.Format("{0} | {1}", artist, release),
                    ArtworkUrl = string.Empty,
                    Score = score
                });
            }

            return results;
        }

        private IEnumerable<ResourceResult> SearchDiscogs(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            string token = ReadResourceText("discogs_user_token.txt");
            if (string.IsNullOrWhiteSpace(token))
            {
                results.Add(new ResourceResult
                {
                    Source = "Discogs",
                    ResourceType = "status",
                    Label = "Discogs user token not found.",
                    Detail = "Add discogs_user_token.txt to resources.",
                    Score = -1
                });
                return results;
            }

            var detailUrls = new List<DiscogsDetailRequest>();
            foreach (DiscogsSearchRequest search in BuildDiscogsSearchRequests(request, token))
            {
                Dictionary<string, object> root = GetJson(search.Url, new Dictionary<string, string>
                {
                    { "User-Agent", "BitLCDMarqueeStudio/0.1 +https://github.com/stevehammoud/BitLCDMarqueeStudio" }
                });

                foreach (Dictionary<string, object> item in GetArray(root, "results").Take(12))
                {
                    string artworkUrl = GetString(item, "cover_image");
                    if (IsDiscogsPlaceholderImage(artworkUrl))
                    {
                        artworkUrl = GetString(item, "thumb");
                    }

                    string title = GetString(item, "title");
                    string type = GetDiscogsResultType(item, search.Type);
                    string year = GetString(item, "year");
                    string country = GetString(item, "country");
                    string label = string.IsNullOrWhiteSpace(title) ? request.Title : title;
                    int score = ScoreDiscogsCandidate(item, request);

                    if (!IsDiscogsPlaceholderImage(artworkUrl))
                    {
                        results.Add(new ResourceResult
                        {
                            Source = "Discogs",
                            ResourceType = DiscogsResourceType(type, "cover"),
                            Label = label,
                            Detail = string.Join(" | ", new[] { year, country, GetDiscogsFormat(item) }.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray()),
                            ArtworkUrl = artworkUrl,
                            Score = score
                        });
                    }

                    string resourceUrl = GetString(item, "resource_url");
                    if (!string.IsNullOrWhiteSpace(resourceUrl))
                    {
                        detailUrls.Add(new DiscogsDetailRequest(resourceUrl, type, label, score));
                    }
                }

                if (results.Any(r => r.Score >= 250))
                {
                    break;
                }
            }

            foreach (DiscogsDetailRequest detail in detailUrls
                .GroupBy(d => d.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(d => d.Score).First())
                .OrderByDescending(d => d.Score)
                .Take(18))
            {
                AddDiscogsDetailImages(results, detail, token);
            }

            return Deduplicate(results);
        }

        private void AddDiscogsDetailImages(IList<ResourceResult> results, DiscogsDetailRequest detail, string token)
        {
            string url = AppendDiscogsToken(detail.Url, token);
            Dictionary<string, object> root;
            try
            {
                root = GetJson(url, new Dictionary<string, string>
                {
                    { "User-Agent", "BitLCDMarqueeStudio/0.1 +https://github.com/stevehammoud/BitLCDMarqueeStudio" }
                });
            }
            catch (WebException)
            {
                return;
            }

            int imageIndex = 0;
            foreach (Dictionary<string, object> image in GetArray(root, "images"))
            {
                string imageUrl = GetString(image, "uri");
                if (IsDiscogsPlaceholderImage(imageUrl)) imageUrl = GetString(image, "resource_url");
                if (IsDiscogsPlaceholderImage(imageUrl)) imageUrl = GetString(image, "uri150");
                if (IsDiscogsPlaceholderImage(imageUrl)) continue;

                string imageType = GetString(image, "type");
                string resourceType = DiscogsResourceType(detail.Type, string.IsNullOrWhiteSpace(imageType) ? "image" : imageType + " image");
                results.Add(new ResourceResult
                {
                    Source = "Discogs",
                    ResourceType = resourceType,
                    Label = detail.Label,
                    Detail = "Discogs detail image " + (++imageIndex).ToString(CultureInfo.InvariantCulture),
                    ArtworkUrl = imageUrl,
                    Score = detail.Score - imageIndex
                });
            }
        }

        private static IEnumerable<DiscogsSearchRequest> BuildDiscogsSearchRequests(JukeboxSearchRequest request, string token)
        {
            var searches = new List<DiscogsSearchRequest>();
            string baseUrl = "https://api.discogs.com/database/search?per_page=25&token=" + Uri.EscapeDataString(token);
            string artist = (request.Artist ?? string.Empty).Trim();
            string title = (request.Title ?? string.Empty).Trim();
            string album = (request.AlbumOrRelease ?? string.Empty).Trim();
            string year = (request.ReleaseYear ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album))
            {
                AddDiscogsTypedSearch(searches, baseUrl, "release", "&artist=" + Uri.EscapeDataString(artist) + "&release_title=" + Uri.EscapeDataString(album) + OptionalDiscogsYear(year));
                AddDiscogsTypedSearch(searches, baseUrl, "master", "&artist=" + Uri.EscapeDataString(artist) + "&release_title=" + Uri.EscapeDataString(album) + OptionalDiscogsYear(year));
            }

            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            {
                AddDiscogsTypedSearch(searches, baseUrl, "release", "&artist=" + Uri.EscapeDataString(artist) + "&track=" + Uri.EscapeDataString(title) + OptionalDiscogsYear(year));
                AddDiscogsTypedSearch(searches, baseUrl, "master", "&artist=" + Uri.EscapeDataString(artist) + "&track=" + Uri.EscapeDataString(title) + OptionalDiscogsYear(year));
            }

            if (!string.IsNullOrWhiteSpace(artist))
            {
                AddDiscogsTypedSearch(searches, baseUrl, "artist", "&q=" + Uri.EscapeDataString(artist));
                AddDiscogsTypedSearch(searches, baseUrl, "label", "&q=" + Uri.EscapeDataString(artist));
            }

            string fullQuery = string.Join(" ", new[] { artist, title, album, year }.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray()).Trim();
            if (!string.IsNullOrWhiteSpace(fullQuery))
            {
                AddDiscogsTypedSearch(searches, baseUrl, "release", "&q=" + Uri.EscapeDataString(fullQuery));
                AddDiscogsTypedSearch(searches, baseUrl, "master", "&q=" + Uri.EscapeDataString(fullQuery));
            }

            return searches
                .GroupBy(s => s.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());
        }

        private static void AddDiscogsTypedSearch(ICollection<DiscogsSearchRequest> searches, string baseUrl, string type, string parameters)
        {
            searches.Add(new DiscogsSearchRequest(type, baseUrl + "&type=" + Uri.EscapeDataString(type) + parameters));
        }

        private static string OptionalDiscogsYear(string year)
        {
            return string.IsNullOrWhiteSpace(year) ? string.Empty : "&year=" + Uri.EscapeDataString(year);
        }

        private static bool IsDiscogsPlaceholderImage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            string normalized = url.ToLowerInvariant();
            return normalized.Contains("spacer.gif") || normalized.Contains("transparent.gif");
        }

        private static string AppendDiscogsToken(string url, string token)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            string separator = url.Contains("?") ? "&" : "?";
            return url + separator + "token=" + Uri.EscapeDataString(token);
        }

        private static string GetDiscogsResultType(Dictionary<string, object> item, string fallbackType)
        {
            string type = GetString(item, "type");
            return string.IsNullOrWhiteSpace(type) ? fallbackType : type;
        }

        private static string DiscogsResourceType(string type, string suffix)
        {
            type = string.IsNullOrWhiteSpace(type) ? "resource" : type.Trim();
            suffix = string.IsNullOrWhiteSpace(suffix) ? "image" : suffix.Trim();
            return type + " " + suffix;
        }

        private static string GetDiscogsFormat(Dictionary<string, object> item)
        {
            object value;
            if (item != null && item.TryGetValue("format", out value))
            {
                var array = value as ArrayList;
                if (array != null) return string.Join(", ", array.Cast<object>().Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray());
                var enumerable = value as IEnumerable;
                if (enumerable != null && !(value is string)) return string.Join(", ", enumerable.Cast<object>().Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray());
            }
            return GetString(item, "format");
        }

        private static int ScoreDiscogsCandidate(Dictionary<string, object> item, JukeboxSearchRequest request)
        {
            string title = GetString(item, "title");
            string year = GetString(item, "year");
            int score = 35;
            score += ScoreName(title, request.Artist) * 2;
            score += ScoreName(title, request.AlbumOrRelease) * 2;
            score += ScoreName(title, request.Title);
            if (!string.IsNullOrWhiteSpace(request.ReleaseYear) && string.Equals(year, request.ReleaseYear, StringComparison.OrdinalIgnoreCase))
            {
                score += 35;
            }
            return score;
        }

        private IEnumerable<ResourceResult> SearchFanArt(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            string projectKey = ReadResourceText("fanart_project_api_key.txt");
            string personalKey = ReadResourceText("fanart_personal_api_key.txt");
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                results.Add(new ResourceResult { Source = "FanArt.tv", ResourceType = "status", Label = "FanArt project API key not found." });
                return results;
            }

            string mbid = FindMusicBrainzArtistId(request.Artist);
            if (string.IsNullOrWhiteSpace(mbid))
            {
                results.Add(new ResourceResult { Source = "FanArt.tv", ResourceType = "status", Label = "No MusicBrainz artist ID found for FanArt lookup." });
                return results;
            }

            string url = "https://webservice.fanart.tv/v3/music/" + Uri.EscapeDataString(mbid) + "?api_key=" + Uri.EscapeDataString(projectKey);
            if (!string.IsNullOrWhiteSpace(personalKey))
            {
                url += "&client_key=" + Uri.EscapeDataString(personalKey);
            }
            Dictionary<string, object> root = GetJson(url, null);

            AddFanArtArray(results, root, "hdmusiclogo", "artist logo", mbid);
            AddFanArtArray(results, root, "musiclogo", "artist logo", mbid);
            AddFanArtArray(results, root, "musicbanner", "artist banner", mbid);
            AddFanArtArray(results, root, "artistthumb", "artist image", mbid);
            AddFanArtArray(results, root, "artistbackground", "artist background", mbid);
            return results;
        }

        private void AddFanArtArray(IList<ResourceResult> results, Dictionary<string, object> root, string key, string type, string mbid)
        {
            foreach (Dictionary<string, object> item in GetArray(root, key))
            {
                results.Add(new ResourceResult
                {
                    Source = "FanArt.tv",
                    ResourceType = type,
                    Label = key,
                    Detail = "FanArt.tv asset | MBID: " + mbid,
                    ArtworkUrl = GetString(item, "url"),
                    Score = type == "artist logo" ? 70 : 45
                });
            }
        }

        private string FindMusicBrainzArtistId(string artistName)
        {
            string url = "https://musicbrainz.org/ws/2/artist/?query=" + Uri.EscapeDataString("artist:" + artistName) + "&fmt=json&limit=1";
            Dictionary<string, object> root;
            try
            {
                root = GetJson(url, new Dictionary<string, string>
                {
                    { "User-Agent", "BitLCDMarqueeStudio/0.1 (personal-use metadata lookup)" }
                });
            }
            catch (WebException)
            {
                return string.Empty;
            }
            Dictionary<string, object> first = GetArray(root, "artists").FirstOrDefault();
            return first == null ? string.Empty : GetString(first, "id");
        }

        private IEnumerable<ResourceResult> SearchScreenScraper(ArcadeSearchRequest request)
        {
            var results = new List<ResourceResult>();
            string devId = ReadResourceText("screenscraper_devid.txt");
            string devPassword = ReadResourceText("screenscraper_devpassword.txt");
            string softName = ReadResourceText("screenscraper_softname.txt");
            string ssid = ReadResourceText("screenscraper_ssid.txt");
            string ssPassword = ReadResourceText("screenscraper_sspassword.txt");

            if (string.IsNullOrWhiteSpace(softName)) softName = "BitLCDMarqueeStudio";
            if (string.IsNullOrWhiteSpace(devId) || string.IsNullOrWhiteSpace(devPassword))
            {
                results.Add(new ResourceResult
                {
                    Source = "ScreenScraper",
                    ResourceType = "status",
                    Label = "ScreenScraper developer credentials are required.",
                    Detail = "Add screenscraper_devid.txt and screenscraper_devpassword.txt to resources.",
                    Score = -1
                });
                return results;
            }

            string gameName = (request.GameName ?? string.Empty).Trim();
            string romName = (request.RomName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(gameName) && string.IsNullOrWhiteSpace(romName))
            {
                results.Add(new ResourceResult { Source = "ScreenScraper", ResourceType = "status", Label = "Arcade game name or ROM name is required." });
                return results;
            }

            string baseParams = BuildScreenScraperBaseParameters(devId, devPassword, softName, ssid, ssPassword, request.SystemId);

            if (!string.IsNullOrWhiteSpace(romName))
            {
                AddScreenScraperRomLookup(results, baseParams, "romnom", romName);
                AddScreenScraperRomLookup(results, baseParams, "romfilename", romName);
            }

            if (!string.IsNullOrWhiteSpace(gameName))
            {
                string url = "https://api.screenscraper.fr/api2/jeuRecherche.php?output=json" + baseParams;
                url += "&recherche=" + Uri.EscapeDataString(gameName);

                Dictionary<string, object> root = GetJson(url, null);
                AddScreenScraperGamesFromResponse(results, GetDictionary(root, "response"), gameName);
            }

            return Deduplicate(results);
        }

        private static string BuildScreenScraperBaseParameters(string devId, string devPassword, string softName, string ssid, string ssPassword, string systemId)
        {
            string url = "&devid=" + Uri.EscapeDataString(devId);
            url += "&devpassword=" + Uri.EscapeDataString(devPassword);
            url += "&softname=" + Uri.EscapeDataString(softName);
            if (!string.IsNullOrWhiteSpace(ssid)) url += "&ssid=" + Uri.EscapeDataString(ssid);
            if (!string.IsNullOrWhiteSpace(ssPassword)) url += "&sspassword=" + Uri.EscapeDataString(ssPassword);
            if (!string.IsNullOrWhiteSpace(systemId)) url += "&systemeid=" + Uri.EscapeDataString(systemId.Trim());
            return url;
        }

        private void AddScreenScraperRomLookup(IList<ResourceResult> results, string baseParams, string parameterName, string romName)
        {
            string url = "https://api.screenscraper.fr/api2/jeuInfos.php?output=json" + baseParams;
            url += "&" + parameterName + "=" + Uri.EscapeDataString(romName);
            try
            {
                Dictionary<string, object> root = GetJson(url, null);
                AddScreenScraperGamesFromResponse(results, GetDictionary(root, "response"), romName);
            }
            catch (WebException)
            {
            }
        }

        private void AddScreenScraperGamesFromResponse(IList<ResourceResult> results, Dictionary<string, object> response, string fallbackName)
        {
            Dictionary<string, object> singleGame = GetDictionary(response, "jeu");
            if (singleGame.Count > 0)
            {
                AddScreenScraperGame(results, singleGame, fallbackName);
            }

            foreach (Dictionary<string, object> game in GetArray(response, "jeux"))
            {
                AddScreenScraperGame(results, game, fallbackName);
            }
        }

        private void AddScreenScraperGame(IList<ResourceResult> results, Dictionary<string, object> game, string fallbackName)
        {
            string label = GetPreferredLocalizedText(game, "noms", "text");
            if (string.IsNullOrWhiteSpace(label)) label = GetString(game, "nom");
            AddScreenScraperMedia(results, game, label, fallbackName);
        }
        private void AddScreenScraperMedia(IList<ResourceResult> results, Dictionary<string, object> game, string label, string gameName)
        {
            foreach (Dictionary<string, object> media in GetArray(game, "medias"))
            {
                string type = GetString(media, "type");
                string url = GetString(media, "url");
                if (string.IsNullOrWhiteSpace(url)) continue;
                if (!IsUsefulScreenScraperMedia(type)) continue;

                results.Add(new ResourceResult
                {
                    Source = "ScreenScraper",
                    ResourceType = type,
                    Label = string.IsNullOrWhiteSpace(label) ? gameName : label,
                    Detail = "ScreenScraper media: " + type,
                    ArtworkUrl = url,
                    Score = ScoreScreenScraperMedia(type)
                });
            }
        }

        private static bool IsUsefulScreenScraperMedia(string type)
        {
            string normalized = (type ?? string.Empty).ToLowerInvariant();
            return normalized.Contains("wheel") ||
                   normalized.Contains("logo") ||
                   normalized.Contains("marquee") ||
                   normalized.Contains("screen") ||
                   normalized.Contains("box") ||
                   normalized.Contains("fanart") ||
                   normalized.Contains("mix") ||
                   normalized.Contains("bezel") ||
                   normalized.Contains("cabinet");
        }

        private static int ScoreScreenScraperMedia(string type)
        {
            string normalized = (type ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("wheel") || normalized.Contains("logo")) return 90;
            if (normalized.Contains("marquee")) return 85;
            if (normalized.Contains("fanart")) return 75;
            if (normalized.Contains("screen")) return 70;
            if (normalized.Contains("box")) return 60;
            return 45;
        }

        private static int ScoreName(string actual, string expected)
        {
            string a = NormalizeText(actual);
            string e = NormalizeText(expected);
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(e)) return 0;
            if (a == e) return 100;
            if (a.Contains(e) || e.Contains(a)) return 55;

            string[] expectedParts = e.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int hits = expectedParts.Count(part => a.Contains(part));
            if (expectedParts.Length == 0) return 0;
            return (int)Math.Round((hits / (double)expectedParts.Length) * 35);
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string formD = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (char ch in formD)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }
            string text = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            text = Regex.Replace(text, @"\bac\s*/?\s*dc\b", "acdc");
            text = Regex.Replace(text, @"\ba\s*-\s*ha\b", "aha");
            text = Regex.Replace(text, @"\b(ft|feat|featuring|with|and|x|y|con)\b", " ");
            text = Regex.Replace(text, @"[^\p{L}\p{Nd}]+", " ");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private static IEnumerable<string> SplitFeaturedArtists(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Enumerable.Empty<string>();
            return Regex.Split(value, @"\s*(?:,|&|\+|\sx\s|\sand\s)\s*", RegexOptions.IgnoreCase)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        private Dictionary<string, object> GetJson(string url, IDictionary<string, string> headers)
        {
            const int attempts = 3;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        if (headers != null)
                        {
                            foreach (KeyValuePair<string, string> header in headers)
                            {
                                client.Headers[header.Key] = header.Value;
                            }
                        }
                        string json = client.DownloadString(url);
                        return _json.DeserializeObject(json) as Dictionary<string, object> ?? new Dictionary<string, object>();
                    }
                }
                catch (WebException ex)
                {
                    if (!ShouldRetry(ex) || attempt == attempts)
                    {
                        throw;
                    }
                    Thread.Sleep(750 * attempt);
                }
            }
            return new Dictionary<string, object>();
        }

        private static bool ShouldRetry(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response == null) return false;
            return response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                   response.StatusCode == (HttpStatusCode)429 ||
                   response.StatusCode == HttpStatusCode.GatewayTimeout ||
                   response.StatusCode == HttpStatusCode.BadGateway;
        }

        private static string DescribeWebException(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response == null) return ex.Message;
            return ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + response.StatusDescription;
        }

        private void CacheArtwork(IEnumerable<ResourceResult> results)
        {
            foreach (ResourceResult result in results)
            {
                if (string.IsNullOrWhiteSpace(result.ArtworkUrl)) continue;
                try
                {
                    result.CachedImagePath = GetCachedArtworkPath(result.ArtworkUrl);
                }
                catch
                {
                    result.CachedImagePath = string.Empty;
                }
            }
        }

        private string GetCachedArtworkPath(string url)
        {
            byte[] hashBytes;
            using (SHA256 sha = SHA256.Create())
            {
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
            }
            string name = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant() + ".img";
            string path = Path.Combine(_cacheDir, name);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

            using (var client = new WebClient())
            {
                client.DownloadFile(url, path);
            }
            return path;
        }

        private string ReadResourceText(string fileName)
        {
            string path = Path.Combine(_resourcesDir, fileName);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : string.Empty;
        }

        private static string FindResourcesDirectory(string baseDirectory)
        {
            string current = baseDirectory;
            for (int i = 0; i < 5 && !string.IsNullOrWhiteSpace(current); i++)
            {
                string candidate = Path.Combine(current, "resources");
                if (Directory.Exists(candidate)) return candidate;
                current = Directory.GetParent(current) == null ? null : Directory.GetParent(current).FullName;
            }
            return Path.Combine(baseDirectory, "resources");
        }

        private static IEnumerable<ResourceResult> Deduplicate(IEnumerable<ResourceResult> results)
        {
            return results
                .GroupBy(r => (r.Source + "|" + r.ResourceType + "|" + r.Label + "|" + r.ArtworkUrl).ToLowerInvariant())
                .Select(g => g.OrderByDescending(r => r.Score).First());
        }

        private static string EscapeMusicBrainzQuery(string value)
        {
            return (value ?? string.Empty).Replace("\"", "\\\"");
        }

        private static string GetArtistCredit(Dictionary<string, object> recording)
        {
            var names = new List<string>();
            foreach (Dictionary<string, object> credit in GetArray(recording, "artist-credit"))
            {
                string name = GetString(credit, "name");
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            return string.Join(" & ", names.ToArray());
        }

        private static string GetFirstReleaseTitle(Dictionary<string, object> recording)
        {
            Dictionary<string, object> release = GetArray(recording, "releases").FirstOrDefault();
            return release == null ? string.Empty : GetString(release, "title");
        }

        private static string GetPreferredLocalizedText(Dictionary<string, object> source, string arrayKey, string valueKey)
        {
            var items = GetArray(source, arrayKey).ToList();
            string preferred = GetLocalizedTextByKeys(items, valueKey, new[] { "us", "usa", "en", "eng", "english", "world", "wor" });
            if (!string.IsNullOrWhiteSpace(preferred)) return preferred;

            foreach (Dictionary<string, object> item in items)
            {
                string text = GetString(item, valueKey);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            return string.Empty;
        }

        private static string GetLocalizedTextByKeys(IEnumerable<Dictionary<string, object>> items, string valueKey, IEnumerable<string> preferredKeys)
        {
            foreach (string preferredKey in preferredKeys)
            {
                foreach (Dictionary<string, object> item in items)
                {
                    if (!LocalizedItemMatches(item, preferredKey)) continue;
                    string text = GetString(item, valueKey);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
            return string.Empty;
        }

        private static bool LocalizedItemMatches(Dictionary<string, object> item, string preferredKey)
        {
            return LocalizedValueMatches(GetString(item, "region"), preferredKey) ||
                   LocalizedValueMatches(GetString(item, "regions"), preferredKey) ||
                   LocalizedValueMatches(GetString(item, "langue"), preferredKey) ||
                   LocalizedValueMatches(GetString(item, "language"), preferredKey) ||
                   LocalizedValueMatches(GetString(item, "lang"), preferredKey) ||
                   LocalizedValueMatches(GetString(item, "loc"), preferredKey);
        }

        private static bool LocalizedValueMatches(string value, string preferredKey)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string normalized = value.Trim().ToLowerInvariant();
            return normalized == preferredKey || normalized.Contains("-" + preferredKey) || normalized.Contains(preferredKey + "-");
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null) return string.Empty;
            return Convert.ToString(source[key], CultureInfo.InvariantCulture);
        }

        private static string GetNestedString(Dictionary<string, object> source, string objectKey, string stringKey)
        {
            return GetString(GetDictionary(source, objectKey), stringKey);
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key)) return new Dictionary<string, object>();
            return source[key] as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static IEnumerable<Dictionary<string, object>> GetDataArray(Dictionary<string, object> source)
        {
            return GetArray(source, "data");
        }

        private static IEnumerable<Dictionary<string, object>> GetArray(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null) return Enumerable.Empty<Dictionary<string, object>>();
            var array = source[key] as ArrayList;
            if (array != null) return array.OfType<Dictionary<string, object>>();

            var objectArray = source[key] as object[];
            if (objectArray != null) return objectArray.OfType<Dictionary<string, object>>();

            var enumerable = source[key] as IEnumerable;
            if (enumerable == null || source[key] is string) return Enumerable.Empty<Dictionary<string, object>>();
            return enumerable.OfType<Dictionary<string, object>>();
        }

        private sealed class DiscogsSearchRequest
        {
            public DiscogsSearchRequest(string type, string url)
            {
                Type = type ?? string.Empty;
                Url = url ?? string.Empty;
            }

            public string Type { get; private set; }
            public string Url { get; private set; }
        }

        private sealed class DiscogsDetailRequest
        {
            public DiscogsDetailRequest(string url, string type, string label, int score)
            {
                Url = url ?? string.Empty;
                Type = type ?? string.Empty;
                Label = label ?? string.Empty;
                Score = score;
            }

            public string Url { get; private set; }
            public string Type { get; private set; }
            public string Label { get; private set; }
            public int Score { get; private set; }
        }
    }
}
