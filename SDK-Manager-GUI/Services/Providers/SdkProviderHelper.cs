using System;
using System.Net.Http;

namespace SDK_Manager_GUI.Services
{
    /// <summary>
    /// SDK Provider 通用工具方法
    /// </summary>
    internal static class SdkProviderHelper
    {
        /// <summary>
        /// 创建标准化的 HttpClient 实例（自动解压、30秒超时、统一 User-Agent）
        /// </summary>
        public static HttpClient CreateHttpClient(TimeSpan? timeout = null)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "SDK-Manager-GUI/1.0");
            return client;
        }
    }
}
