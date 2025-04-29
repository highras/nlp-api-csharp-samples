using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

namespace com.ilivedata.alt
{
    class Program
    {
        private const string PROJECT_ID = "YOUR_PROJECT_ID_GOES_HERE";
        private const string SECRET_KEY = "YOUR_SECRET_KEY_GOES_HERE";

        private string postUrl = "https://translate.ilivedata.com/api/v2/translate";
        private string endpointHost = "translate.ilivedata.com";
        private string endpointUri = "/api/v2/translate";
        public const string ISO8601DateFormatNoMS = "yyyy-MM-dd\\THH:mm:ss\\Z";

        internal static Dictionary<int, string> RFCEncodingSchemes = new Dictionary<int, string>
        {
            { 3986,  ValidUrlCharacters },
            { 1738,  ValidUrlCharactersRFC1738 }
        };

        /// <summary>
        /// The Set of accepted and valid Url characters per RFC3986. 
        /// Characters outside of this set will be encoded.
        /// </summary>
        public const string ValidUrlCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        /// <summary>
        /// The Set of accepted and valid Url characters per RFC1738. 
        /// Characters outside of this set will be encoded.
        /// </summary>
        public const string ValidUrlCharactersRFC1738 = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.";

        /// <summary>
        /// The set of accepted and valid Url path characters per RFC3986.
        /// </summary>
        private static string ValidPathCharacters = DetermineValidPathCharacters();

        public string Translate(string source, string sourceLanguage, string targetLanguage)
        {
            string now = DateTimeOffset.UtcNow.ToString(ISO8601DateFormatNoMS);


            IDictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("source", sourceLanguage);
            parameters.Add("target", targetLanguage);
            parameters.Add("q", source);
            parameters.Add("timeStamp", now);
            parameters.Add("appId", PROJECT_ID);

            IDictionary<string, string> sortedDic = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal);

            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in sortedDic)
            {
                if (kv.Value != null)
                {
                    builder.Append(UrlEncode(kv.Key, false));
                    builder.Append("=");
                    builder.Append(UrlEncode(kv.Value, false));
                    builder.Append("&");
                }

            }
            builder.Remove(builder.Length - 1, 1);
            // Prepare stringToSign
            StringBuilder data = new StringBuilder();
            data.Append("POST").Append("\n");
            data.Append(endpointHost).Append("\n");
            data.Append(UrlEncode(endpointUri, true)).Append("\n");
            data.Append(builder.ToString());
            // Sign the request 
            String authToken = SignAndBase64Encode(data.ToString(), SECRET_KEY);
            Console.WriteLine(authToken);
            // Send request
            string result = SendRequest(builder.ToString(), authToken, now);
            Console.WriteLine(result);

            return result;
        }

        private string SendRequest(string body, string authToken, string timeStamp)
        {
            string result = "";
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;
            request.Headers.Add(HttpRequestHeader.Authorization, authToken);
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ServicePoint.Expect100Continue = false;

            byte[] requestContent = Encoding.UTF8.GetBytes(body);
            request.ContentLength = requestContent.Length;
            Stream newStream = request.GetRequestStream();
            // Send the data.
            newStream.Write(requestContent, 0, requestContent.Length);
            newStream.Close();

            try
            {
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)request.GetResponse();
                // Gets the stream associated with the response.
                Stream receiveStream = myHttpWebResponse.GetResponseStream();
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, encode);
                // D.log("\r\nResponse stream received.");
                Char[] read = new Char[256];
                // Reads 256 characters at a time.    
                int count = readStream.Read(read, 0, 256);

                while (count > 0)
                {
                    // Dumps the 256 characters on a string and displays the string to the console.
                    result += new String(read, 0, count);
                    count = readStream.Read(read, 0, 256);
                }

                // Releases the resources of the response.
                myHttpWebResponse.Close();
                // Releases the resources of the Stream.
                readStream.Close();
            }
            catch (WebException ex)
            {
                using (WebResponse response = ex.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine(e);
                    if (response == null)
                    {
                        result = ex.Message;
                    }
                    else
                    {
                        using (Stream stream = response.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            result = reader.ReadToEnd();
                            // Console.WriteLine(text);
                            //return text;
                        }
                    }

                }
                //str = ex.Message;
            }
            return result;
        }

        private string SignAndBase64Encode(String data, String key)
        {
            byte[] bytes;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }

            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// URL encodes a string per RFC3986. If the path property is specified,
        /// the accepted path characters {/+:} are not encoded.
        /// </summary>
        /// <param name="data">The string to encode</param>
        /// <param name="path">Whether the string is a URL path or not</param>
        /// <returns>The encoded string</returns>
        public static string UrlEncode(string data, bool path)
        {
            return UrlEncode(3986, data, path);
        }


        /// <summary>
        /// URL encodes a string per the specified RFC. If the path property is specified,
        /// the accepted path characters {/+:} are not encoded.
        /// </summary>
        /// <param name="rfcNumber">RFC number determing safe characters</param>
        /// <param name="data">The string to encode</param>
        /// <param name="path">Whether the string is a URL path or not</param>
        /// <returns>The encoded string</returns>
        /// <remarks>
        /// Currently recognised RFC versions are 1738 (Dec '94) and 3986 (Jan '05). 
        /// If the specified RFC is not recognised, 3986 is used by default.
        /// </remarks>
        public static string UrlEncode(int rfcNumber, string data, bool path)
        {
            StringBuilder encoded = new StringBuilder(data.Length * 2);
            string validUrlCharacters;
            if (!RFCEncodingSchemes.TryGetValue(rfcNumber, out validUrlCharacters))
                validUrlCharacters = ValidUrlCharacters;

            string unreservedChars = String.Concat(validUrlCharacters, (path ? ValidPathCharacters : ""));

            foreach (char symbol in System.Text.Encoding.UTF8.GetBytes(data))
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                {
                    encoded.Append(symbol);
                }
                else
                {
                    encoded.Append("%").Append(string.Format(CultureInfo.InvariantCulture, "{0:X2}", (int)symbol));
                }
            }

            return encoded.ToString();
        }

        // Checks which path characters should not be encoded
        // This set will be different for .NET 4 and .NET 4.5, as
        // per http://msdn.microsoft.com/en-us/library/hh367887%28v=vs.110%29.aspx
        private static string DetermineValidPathCharacters()
        {
            const string basePathCharacters = "/:'()!*[]";

            var sb = new StringBuilder();
            foreach (var c in basePathCharacters)
            {
                var escaped = Uri.EscapeUriString(c.ToString());
                if (escaped.Length == 1 && escaped[0] == c)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        static void Main(string[] args)
        {
            Program translator = new Program();
            translator.Translate("こんにちは", "ja", "en");
            translator.Translate("Hello World!", "en", "ru");
            translator.Translate("Hello everyone!", "en", "ko");

        }
    }
}
