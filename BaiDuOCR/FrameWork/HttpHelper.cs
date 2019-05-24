using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BaiDuOCR.FrameWork
{
    public static class HttpHelper
    {
        /// <summary>
        /// Http Post
        /// </summary>
        /// <param name="Url"></param>
        /// <param name="Params">grant_type=client_credentials&client_id=" + AppId + "&client_secret=" + AppSecret</param>
        /// <returns></returns>
        public static string HttpPost(string Url, string Params)
        {
            var result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            byte[] data = Encoding.UTF8.GetBytes(Params);
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }


    }
}
