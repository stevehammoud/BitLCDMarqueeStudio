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
using System.Web.Script.Serialization;

namespace BitLCDMarqueeStudio
{
    internal sealed class ResourceSearchService
    {
        private readonly string _rootDir;
        private readonly string _resourcesDir;
        private readonly JavaScriptSerializer _json;
        private string _appleToken;
        private DateTime _appleTokenExpiresUtc;

        public ResourceSearchService()
        {
            _rootDir = AppDomain.CurrentDomain.BaseDirectory;
            _resourcesDir = FindResourcesDirectory(_rootDir);
            _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public IList<ResourceResult> SearchJukebox(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            results.AddRange(SearchAppleMusic(request));
            results.AddRange(SearchMusicBrainz(request));
            results.AddRange(SearchFanArt(request));
            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Source)
                .ThenBy(r => r.ResourceType)
                .ToList();
        }

        private IEnumerable<ResourceResult> SearchAppleMusic(JukeboxSearchRequest request)
        {
            var results = new List<ResourceResult>();
            string token = GetAppleDeveloperToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                results.Add(new ResourceResult { Source = "Apple Music", ResourceType = "status", Label = "Apple credentials not found or token could not be created." });
                return results;
            }

            foreach (string term in BuildAppleSearchTerms(request))
            {
                string url = string.Format(
                    "https://api.music.apple.com/v1/catalog/us/search?term={0}&types=songs,music-videos,artists&limit=25",
                    Uri.EscapeDataString(term));

                Dictionary<string, object> root = GetJson(url, new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
                Dictionary<string, object> data = GetDictionary(root, "results");

                foreach (Dictionary<string, object> song in GetDataArray(GetDictionary(data, "songs")))
                {
                    Dictionary<string, object> attr = GetDictionary(song, "attributes");
                    int score = ScoreAppleCandidate(attr, request);
                    results.Add(new ResourceResult
                    {
                        Source = "Apple Music",
                        ResourceType = "song/album art",
                        Label = GetString(attr, "name"),
                        Detail = string.Format("{0} | {1}", GetString(attr, "artistName"), GetString(attr, "albumName")),
                        ArtworkUrl = FormatAppleArtworkUrl(GetNestedString(attr, "artwork", "url"), 1200, 1200),
                        Score = score
                    });
                }

                foreach (Dictionary<string, object> video in GetDataArray(GetDictionary(data, "music-videos")))
                {
                    Dictionary<string, object> attr = GetDictionary(video, "attributes");
                    int score = ScoreAppleCandidate(attr, request) - 4;
                    results.Add(new ResourceResult
                    {
                        Source = "Apple Music",
                        ResourceType = "music video still",
                        Label = GetString(attr, "name"),
                        Detail = GetString(attr, "artistName"),
                        ArtworkUrl = FormatAppleArtworkUrl(GetNestedString(attr, "artwork", "url"), 1920, 1080),
                        Score = score
                    });
                }

                foreach (Dictionary<string, object> artist in GetDataArray(GetDictionary(data, "artists")))
                {
                    Dictionary<string, object> attr = GetDictionary(artist, "attributes");
                    int score = ScoreName(GetString(attr, "name"), request.Artist) + 20;
                    results.Add(new ResourceResult
                    {
                        Source = "Apple Music",
                        ResourceType = "artist art",
                        Label = GetString(attr, "name"),
                        Detail = "Artist profile artwork",
                        ArtworkUrl = FormatAppleArtworkUrl(GetNestedString(attr, "artwork", "url"), 1200, 1200),
                        Score = score
                    });
                }

                if (results.Count(r => r.Source == "Apple Music") > 0)
                {
                    break;
                }
            }

            foreach (string featured in SplitFeaturedArtists(request.FeaturedArtist))
            {
                string url = string.Format(
                    "https://api.music.apple.com/v1/catalog/us/search?term={0}&types=artists&limit=10",
                    Uri.EscapeDataString(featured));
                Dictionary<string, object> root = GetJson(url, new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
                foreach (Dictionary<string, object> artist in GetDataArray(GetDictionary(GetDictionary(root, "results"), "artists")))
                {
                    Dictionary<string, object> attr = GetDictionary(artist, "attributes");
                    results.Add(new ResourceResult
                    {
                        Source = "Apple Music",
                        ResourceType = "featured artist art",
                        Label = GetString(attr, "name"),
                        Detail = "Right panel priority candidate",
                        ArtworkUrl = FormatAppleArtworkUrl(GetNestedString(attr, "artwork", "url"), 1200, 1200),
                        Score = ScoreName(GetString(attr, "name"), featured) + 80
                    });
                }
            }

            return Deduplicate(results);
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
            Dictionary<string, object> root = GetJson(url, new Dictionary<string, string>
            {
                { "User-Agent", "BitLCDMarqueeStudio/0.1 (personal-use metadata lookup)" }
            });

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

            AddFanArtArray(results, root, "musiclogo", "artist logo");
            AddFanArtArray(results, root, "artistthumb", "artist image");
            AddFanArtArray(results, root, "artistbackground", "artist background");
            return results;
        }

        private void AddFanArtArray(IList<ResourceResult> results, Dictionary<string, object> root, string key, string type)
        {
            foreach (Dictionary<string, object> item in GetArray(root, key))
            {
                results.Add(new ResourceResult
                {
                    Source = "FanArt.tv",
                    ResourceType = type,
                    Label = type,
                    Detail = "FanArt.tv asset",
                    ArtworkUrl = GetString(item, "url"),
                    Score = type == "artist logo" ? 70 : 45
                });
            }
        }

        private string FindMusicBrainzArtistId(string artistName)
        {
            string url = "https://musicbrainz.org/ws/2/artist/?query=" + Uri.EscapeDataString("artist:\"" + EscapeMusicBrainzQuery(artistName) + "\"") + "&fmt=json&limit=5";
            Dictionary<string, object> root = GetJson(url, new Dictionary<string, string>
            {
                { "User-Agent", "BitLCDMarqueeStudio/0.1 (personal-use metadata lookup)" }
            });
            Dictionary<string, object> best = null;
            int bestScore = -1;
            foreach (Dictionary<string, object> artist in GetArray(root, "artists"))
            {
                int score = ScoreName(GetString(artist, "name"), artistName);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = artist;
                }
            }
            return best == null ? string.Empty : GetString(best, "id");
        }

        private IEnumerable<string> BuildAppleSearchTerms(JukeboxSearchRequest request)
        {
            var terms = new List<string>();
            AddTerm(terms, request.Artist, request.Title, request.AlbumOrRelease, request.ReleaseYear);
            AddTerm(terms, request.Artist, request.Title, request.AlbumOrRelease, string.Empty);
            AddTerm(terms, request.Artist, request.Title, string.Empty, request.ReleaseYear);
            AddTerm(terms, request.Artist, request.Title, string.Empty, string.Empty);
            return terms.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddTerm(ICollection<string> terms, string artist, string title, string release, string year)
        {
            string term = string.Join(" ", new[] { artist, title, release, year }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim();
            if (!string.IsNullOrWhiteSpace(term))
            {
                terms.Add(term);
            }
        }

        private int ScoreAppleCandidate(Dictionary<string, object> attributes, JukeboxSearchRequest request)
        {
            int score = 0;
            score += ScoreName(GetString(attributes, "name"), request.Title) * 2;
            score += ScoreName(GetString(attributes, "artistName"), request.Artist);
            if (!string.IsNullOrWhiteSpace(request.AlbumOrRelease))
            {
                score += ScoreName(GetString(attributes, "albumName"), request.AlbumOrRelease) * 2;
            }
            if (!string.IsNullOrWhiteSpace(request.ReleaseYear))
            {
                string releaseDate = GetString(attributes, "releaseDate");
                if (releaseDate.StartsWith(request.ReleaseYear, StringComparison.Ordinal)) score += 25;
            }
            string combined = (GetString(attributes, "name") + " " + GetString(attributes, "artistName") + " " + GetString(attributes, "albumName")).ToLowerInvariant();
            if (Regex.IsMatch(combined, "karaoke|tribute|made famous by|workout|cover version")) score -= 80;
            return score;
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

        private string GetAppleDeveloperToken()
        {
            if (!string.IsNullOrWhiteSpace(_appleToken) && _appleTokenExpiresUtc > DateTime.UtcNow.AddMinutes(5))
            {
                return _appleToken;
            }

            string teamId = ReadResourceText("apple_music_team_id.txt");
            string keyId = ReadResourceText("apple_music_key_id.txt");
            string privateKeyPath = ReadResourceText("apple_music_private_key_path.txt");
            if (!string.IsNullOrWhiteSpace(privateKeyPath) && Directory.Exists(privateKeyPath) && !string.IsNullOrWhiteSpace(keyId))
            {
                privateKeyPath = Path.Combine(privateKeyPath, "AuthKey_" + keyId + ".p8");
            }
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
            {
                return string.Empty;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset exp = now.AddDays(170);
            string header = _json.Serialize(new Dictionary<string, object> { { "alg", "ES256" }, { "kid", keyId }, { "typ", "JWT" } });
            string payload = _json.Serialize(new Dictionary<string, object> { { "iss", teamId }, { "iat", ToUnixTimeSeconds(now) }, { "exp", ToUnixTimeSeconds(exp) } });
            string signingInput = Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
            byte[] signature = SignAppleJwt(signingInput, privateKeyPath);
            _appleToken = signingInput + "." + Base64Url(ConvertDerSignatureToJose(signature));
            _appleTokenExpiresUtc = exp.UtcDateTime;
            return _appleToken;
        }

        private static byte[] SignAppleJwt(string signingInput, string privateKeyPath)
        {
            string pem = File.ReadAllText(privateKeyPath);
            pem = pem.Replace("-----BEGIN PRIVATE KEY-----", string.Empty).Replace("-----END PRIVATE KEY-----", string.Empty);
            pem = Regex.Replace(pem, @"\s+", string.Empty);
            byte[] pkcs8 = Convert.FromBase64String(pem);
            using (CngKey key = CngKey.Import(pkcs8, CngKeyBlobFormat.Pkcs8PrivateBlob))
            using (ECDsaCng ecdsa = new ECDsaCng(key))
            {
                ecdsa.HashAlgorithm = CngAlgorithm.Sha256;
                return ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput));
            }
        }

        private static byte[] ConvertDerSignatureToJose(byte[] signature)
        {
            if (signature.Length == 64 || signature.Length < 8 || signature[0] != 0x30) return signature;
            int offset = 2;
            if ((signature[1] & 0x80) != 0)
            {
                offset = 2 + (signature[1] & 0x7f);
            }
            if (signature[offset] != 0x02) return signature;
            int rLength = signature[offset + 1];
            int rStart = offset + 2;
            int sMarker = rStart + rLength;
            if (sMarker >= signature.Length || signature[sMarker] != 0x02) return signature;
            int sLength = signature[sMarker + 1];
            int sStart = sMarker + 2;
            byte[] r = TrimInteger(signature, rStart, rLength);
            byte[] s = TrimInteger(signature, sStart, sLength);
            byte[] raw = new byte[64];
            Buffer.BlockCopy(r, 0, raw, 32 - r.Length, r.Length);
            Buffer.BlockCopy(s, 0, raw, 64 - s.Length, s.Length);
            return raw;
        }

        private static byte[] TrimInteger(byte[] source, int start, int length)
        {
            var bytes = new List<byte>();
            for (int i = 0; i < length; i++) bytes.Add(source[start + i]);
            while (bytes.Count > 32 && bytes[0] == 0) bytes.RemoveAt(0);
            return bytes.ToArray();
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static long ToUnixTimeSeconds(DateTimeOffset value)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(value.UtcDateTime - epoch).TotalSeconds;
        }

        private Dictionary<string, object> GetJson(string url, IDictionary<string, string> headers)
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

        private static string FormatAppleArtworkUrl(string url, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            return url.Replace("{w}", width.ToString(CultureInfo.InvariantCulture)).Replace("{h}", height.ToString(CultureInfo.InvariantCulture));
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
            if (array == null) return Enumerable.Empty<Dictionary<string, object>>();
            return array.OfType<Dictionary<string, object>>();
        }
    }
}
