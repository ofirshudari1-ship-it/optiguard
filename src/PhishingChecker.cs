using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UninstallerPro
{
    public class PhishingFinding
    {
        public string ReasonKey;
        public string Detail;
        public int Weight;
    }

    public class PhishingCheckResult
    {
        public int RiskScore;
        public string RiskLevel; // low, medium, high
        public List<PhishingFinding> Findings = new List<PhishingFinding>();
        public List<string> UrlsFound = new List<string>();
    }

    // בודק פישינג "הדבק וניתח": לא סורק תיבת דואר (זה דורש הרשאות OAuth מלאות
    // לחשבון המייל - טווח לא ריאלי לכלי הזה). במקום זאת, המשתמש מדביק טקסט
    // מייל/הודעה חשודה וכלי זה מפעיל היוריסטיקות ידועות (כתובות IP, פונאיקוד,
    // דומיינים דמויי-מותג, מילות דחיפות) ומחזיר ציון סיכון עם הסברים - לא קביעה
    // סופית, רק כלי עזר להערכה.
    public static class PhishingChecker
    {
        private static readonly string[] SuspiciousTlds = { ".zip", ".mov", ".xyz", ".top", ".club", ".gq", ".tk", ".ml", ".cf", ".work", ".click", ".link", ".rest", ".loan", ".country", ".stream" };

        private static readonly string[] UrgencyPhrases = {
            "verify your account", "account suspended", "confirm your identity", "act now",
            "click here immediately", "unusual activity", "password will expire", "your account will be closed",
            "limited time", "immediate action required", "security alert", "update your payment",
            "אמת את החשבון", "החשבון הושעה", "פעולה מיידית", "לחץ כאן מיד", "פעילות חריגה",
            "הסיסמה תפוג", "החשבון ייסגר", "עדכן את פרטי התשלום", "התראת אבטחה"
        };

        private static readonly string[] CredentialWords = {
            "password", "credit card", "social security", "ssn", "cvv", "pin code", "login credentials",
            "סיסמה", "כרטיס אשראי", "תעודת זהות", "קוד אימות", "פרטי התחברות"
        };

        private static readonly string[] BrandNames = {
            "paypal", "microsoft", "apple", "amazon", "google", "facebook", "netflix", "office365",
            "outlook", "dropbox", "bankofamerica", "wellsfargo", "chase", "bit.ly",
            "hapoalim", "leumi", "discount", "mizrahi", "isracard", "cal-online"
        };

        public static PhishingCheckResult Analyze(string text)
        {
            var result = new PhishingCheckResult();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.RiskLevel = "low";
                return result;
            }

            var lower = text.ToLowerInvariant();
            var urlMatches = Regex.Matches(text, @"(https?://[^\s""'<>]+)|(\bwww\.[^\s""'<>]+)", RegexOptions.IgnoreCase);
            foreach (Match m in urlMatches)
            {
                var url = m.Value.TrimEnd('.', ',', ')', ']');
                if (!result.UrlsFound.Contains(url)) result.UrlsFound.Add(url);
            }

            foreach (var url in result.UrlsFound)
            {
                AnalyzeUrl(url, result);
            }

            foreach (var phrase in UrgencyPhrases)
            {
                if (lower.Contains(phrase))
                {
                    result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_urgency", Detail = phrase, Weight = 12 });
                }
            }

            foreach (var word in CredentialWords)
            {
                if (lower.Contains(word))
                {
                    result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_credential_request", Detail = word, Weight = 15 });
                }
            }

            if (result.UrlsFound.Count > 0 && (lower.Contains("dear customer") || lower.Contains("dear user") || lower.Contains("לקוח יקר") || lower.Contains("valued customer")))
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_generic_greeting", Detail = null, Weight = 8 });
            }

            int score = result.Findings.Sum(f => f.Weight);
            if (score > 100) score = 100;
            result.RiskScore = score;
            result.RiskLevel = score >= 50 ? "high" : (score >= 20 ? "medium" : "low");
            return result;
        }

        private static void AnalyzeUrl(string url, PhishingCheckResult result)
        {
            string host;
            try
            {
                var normalized = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "http://" + url;
                host = new Uri(normalized).Host.ToLowerInvariant();
            }
            catch { return; }

            if (Regex.IsMatch(host, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_ip_url", Detail = host, Weight = 25 });
            }

            if (host.StartsWith("xn--") || host.Contains(".xn--"))
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_punycode", Detail = host, Weight = 30 });
            }

            if (url.Contains("@") && Regex.IsMatch(url, @"https?://[^/@]+@"))
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_embedded_credential", Detail = null, Weight = 25 });
            }

            foreach (var tld in SuspiciousTlds)
            {
                if (host.EndsWith(tld, StringComparison.OrdinalIgnoreCase))
                {
                    result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_suspicious_tld", Detail = tld, Weight = 15 });
                    break;
                }
            }

            var hostNoTld = Regex.Replace(host, @"\.[a-z]{2,}$", "");
            var labels = host.Split('.');
            var registrableGuess = labels.Length >= 2 ? labels[labels.Length - 2] : host;
            foreach (var brand in BrandNames)
            {
                if (hostNoTld.Contains(brand) && registrableGuess != brand)
                {
                    result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_lookalike_brand", Detail = brand, Weight = 30 });
                    break;
                }
            }

            var hyphenCount = host.Count(c => c == '-');
            if (hyphenCount >= 3)
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_many_hyphens", Detail = null, Weight = 10 });
            }

            var subdomainCount = labels.Length;
            if (subdomainCount >= 5)
            {
                result.Findings.Add(new PhishingFinding { ReasonKey = "phish_reason_many_subdomains", Detail = null, Weight = 10 });
            }
        }
    }
}
