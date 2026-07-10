using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public class MirrorProvider : IMirrorProvider
    {
        private List<MirrorSource> _mirrors;
        private readonly string _configPath;
        private readonly object _saveLock = new object();
        private readonly ILanguageService _languageService;

        public MirrorProvider(ILanguageService languageService)
        {
            _languageService = languageService;
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "mirrors.json");
            _mirrors = LoadMirrors();
        }

        private List<MirrorSource> GetDefaultMirrors()
        {
            return new List<MirrorSource>
            {
                // Node.js 镜像
                new MirrorSource { Id = "node-official", Language = SdkLanguage.NodeJs, Name = "Node.js Official", BaseUrl = "https://nodejs.org/dist/", IsEnabled = true, Priority = 1, IsDefault = true, IsPreset = true },
                new MirrorSource { Id = "node-npmmirror", Language = SdkLanguage.NodeJs, Name = _languageService.GetString("Mirror_NPMMirror"), BaseUrl = "https://npmmirror.com/mirrors/node/", IsEnabled = true, Priority = 2, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "node-tsinghua", Language = SdkLanguage.NodeJs, Name = _languageService.GetString("Mirror_Tsinghua"), BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/nodejs-release/", IsEnabled = true, Priority = 3, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "node-tencent", Language = SdkLanguage.NodeJs, Name = _languageService.GetString("Mirror_Tencent"), BaseUrl = "https://mirrors.cloud.tencent.com/nodejs-release/", IsEnabled = true, Priority = 4, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "node-huawei", Language = SdkLanguage.NodeJs, Name = _languageService.GetString("Mirror_Huawei"), BaseUrl = "https://repo.huaweicloud.com/nodejs/", IsEnabled = true, Priority = 5, IsDefault = false, IsPreset = true },

                // Java 镜像
                new MirrorSource { Id = "java-adoptium", Language = SdkLanguage.Java, Name = "Adoptium (Temurin)", BaseUrl = "https://api.adoptium.net/", IsEnabled = true, Priority = 1, IsDefault = true, IsPreset = true },
                new MirrorSource { Id = "java-tsinghua", Language = SdkLanguage.Java, Name = _languageService.GetString("Mirror_TsinghuaAdoptium"), BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium/", IsEnabled = true, Priority = 2, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "java-huawei", Language = SdkLanguage.Java, Name = _languageService.GetString("Mirror_Huawei"), BaseUrl = "https://repo.huaweicloud.com/java/jdk/", IsEnabled = true, Priority = 3, IsDefault = false, IsPreset = true },

                // Python 镜像
                new MirrorSource { Id = "python-official", Language = SdkLanguage.Python, Name = "Python Official", BaseUrl = "https://www.python.org/ftp/python/", IsEnabled = true, Priority = 1, IsDefault = true, IsPreset = true },
                new MirrorSource { Id = "python-npmmirror", Language = SdkLanguage.Python, Name = _languageService.GetString("Mirror_NPMMirror"), BaseUrl = "https://npmmirror.com/mirrors/python/", IsEnabled = true, Priority = 2, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "python-tsinghua", Language = SdkLanguage.Python, Name = _languageService.GetString("Mirror_Tsinghua"), BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/python/", IsEnabled = true, Priority = 3, IsDefault = false, IsPreset = true },
                new MirrorSource { Id = "python-huawei", Language = SdkLanguage.Python, Name = _languageService.GetString("Mirror_Huawei"), BaseUrl = "https://repo.huaweicloud.com/python/", IsEnabled = true, Priority = 4, IsDefault = false, IsPreset = true },
            };
        }

        private List<MirrorSource> LoadMirrors()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var mirrors = JsonConvert.DeserializeObject<List<MirrorSource>>(json);
                    if (mirrors != null && mirrors.Count > 0)
                    {
                        var defaults = GetDefaultMirrors();
                        var presetIds = defaults.Select(m => m.Id).ToHashSet();

                        // 移除已失效的旧镜像源
                        var deprecatedIds = new HashSet<string>
                        {
                            "python-aliyun",   // npm.taobao.org 域名已停用
                            "java-aliyun",     // Dragonwell 镜像不稳定
                        };
                        mirrors.RemoveAll(m => deprecatedIds.Contains(m.Id));

                        // 修复旧配置中缺少 IsPreset 字段的预置镜像
                        foreach (var m in mirrors)
                        {
                            if (presetIds.Contains(m.Id))
                                m.IsPreset = true;
                        }
                        return mirrors;
                    }
                }
            }
            catch { }

            return GetDefaultMirrors();
        }

        private void SaveMirrors()
        {
            lock (_saveLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var json = JsonConvert.SerializeObject(_mirrors, Formatting.Indented);
                    File.WriteAllText(_configPath, json);
                }
                catch { }
            }
        }

        public Task<IEnumerable<MirrorSource>> GetMirrorsAsync(SdkLanguage language)
        {
            var mirrors = _mirrors.Where(m => m.Language == language).OrderBy(m => m.Priority).AsEnumerable();
            return Task.FromResult(mirrors);
        }

        public async Task<MirrorSource> GetBestMirrorAsync(SdkLanguage language)
        {
            var mirrors = await GetMirrorsAsync(language);
            var enabled = mirrors.Where(m => m.IsEnabled).ToList();

            if (!enabled.Any()) return null;

            // 并行测试所有启用的镜像延迟
            var testTasks = enabled.Select(async mirror =>
            {
                await TestMirrorLatencyAsync(mirror);
                return mirror;
            }).ToList();

            var results = await Task.WhenAll(testTasks);

            // 优先返回延迟最低的可用镜像
            var best = results
                .Where(m => m.Latency.HasValue && m.Latency.Value >= 0)
                .OrderBy(m => m.Latency)
                .FirstOrDefault();

            if (best != null) return best;

            // 回退到默认镜像
            return enabled.FirstOrDefault(m => m.IsDefault) ?? enabled.FirstOrDefault();
        }

        public async Task TestMirrorLatencyAsync(MirrorSource mirror)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    // 优先使用 HEAD 请求减少数据传输
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, mirror.BaseUrl);
                        await client.SendAsync(request);
                        sw.Stop();
                        mirror.Latency = sw.ElapsedMilliseconds;
                        mirror.LastSuccess = true;
                    }
                    catch (HttpRequestException)
                    {
                        // 部分镜像不支持 HEAD，回退到 GET
                        sw.Restart();
                        await client.GetAsync(mirror.BaseUrl);
                        sw.Stop();
                        mirror.Latency = sw.ElapsedMilliseconds;
                        mirror.LastSuccess = true;
                    }
                }
                catch
                {
                    mirror.Latency = -1;
                    mirror.LastSuccess = false;
                }
                mirror.LastUsedTime = DateTime.Now;
            }
            SaveMirrors();
        }

        public Task AddMirrorAsync(MirrorSource mirror)
        {
            if (string.IsNullOrEmpty(mirror.Id))
                mirror.Id = Guid.NewGuid().ToString("N").Substring(0, 8);

            _mirrors.Add(mirror);
            SaveMirrors();
            return Task.CompletedTask;
        }

        public Task RemoveMirrorAsync(string mirrorId)
        {
            var mirror = _mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror != null)
            {
                _mirrors.Remove(mirror);
                SaveMirrors();
            }
            return Task.CompletedTask;
        }

        public Task UpdateMirrorAsync(MirrorSource mirror)
        {
            var existing = _mirrors.FirstOrDefault(m => m.Id == mirror.Id);
            if (existing != null)
            {
                existing.Name = mirror.Name;
                existing.BaseUrl = mirror.BaseUrl;
                existing.IsEnabled = mirror.IsEnabled;
                existing.Priority = mirror.Priority;
                SaveMirrors();
            }
            return Task.CompletedTask;
        }

        public Task SetDefaultMirrorAsync(string mirrorId)
        {
            var mirror = _mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror != null)
            {
                foreach (var m in _mirrors.Where(m => m.Language == mirror.Language))
                    m.IsDefault = false;
                mirror.IsDefault = true;
                mirror.Priority = 0;
                SaveMirrors();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 重置镜像配置为默认值
        /// </summary>
        public Task ResetToDefaultAsync()
        {
            _mirrors = GetDefaultMirrors();
            SaveMirrors();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录镜像使用结果（成功或失败），同步到镜像配置中
        /// </summary>
        public Task RecordMirrorResultAsync(string mirrorId, bool success)
        {
            var mirror = _mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror != null)
            {
                mirror.LastSuccess = success;
                mirror.LastUsedTime = DateTime.Now;
                if (success)
                {
                    mirror.FailCount = 0;
                }
                else
                {
                    mirror.FailCount++;
                    // 连续失败3次以上自动禁用
                    if (mirror.FailCount >= 3 && !mirror.IsDefault)
                    {
                        mirror.IsEnabled = false;
                    }
                }
                SaveMirrors();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 测试所有镜像源延迟，不可达的自动禁用
        /// </summary>
        public async Task TestAndDisableUnreachableMirrorsAsync()
        {
            var allMirrors = _mirrors.ToList();
            var testTasks = allMirrors.Select(async mirror =>
            {
                try
                {
                    await TestMirrorLatencyAsync(mirror);
                }
                catch
                {
                    mirror.Latency = -1;
                    mirror.LastSuccess = false;
                }
            }).ToList();

            await Task.WhenAll(testTasks);

            // 禁用不可达的镜像（非默认镜像）
            bool changed = false;
            foreach (var mirror in allMirrors)
            {
                if (mirror.LastSuccess == false && mirror.IsEnabled && !mirror.IsDefault)
                {
                    mirror.IsEnabled = false;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveMirrors();
            }
        }
    }
}
