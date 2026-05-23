using System.IO;
using System.Text;
using shmtu.terminal.desktop.Models.Config;
using Tomlyn;
using Tomlyn.Model;

namespace shmtu.terminal.desktop.Services.Config;

/// <summary>
/// TOML 配置文件读写服务 — 管理 app_config.toml 和 classification_rules.toml
/// </summary>
public static class TomlConfigService
{
    private static readonly string DataDirectory =
        shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath;

    #region app_config.toml

    /// <summary>
    /// 获取 app_config.toml 文件路径
    /// </summary>
    public static string GetAppConfigPath()
    {
        return Path.Combine(DataDirectory, "app_config.toml");
    }

    /// <summary>
    /// 加载应用配置
    /// </summary>
    public static AppConfig LoadAppConfig()
    {
        var path = GetAppConfigPath();

        if (!File.Exists(path))
        {
            var defaultConfig = new AppConfig();
            SaveAppConfig(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var tomlText = File.ReadAllText(path);
            var model = TomlSerializer.Deserialize<TomlTable>(tomlText);
            return model is null ? new AppConfig() : ParseAppConfig(model);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载配置文件失败: {ex.Message}");
            return new AppConfig();
        }
    }

    /// <summary>
    /// 保存应用配置
    /// </summary>
    public static void SaveAppConfig(AppConfig config)
    {
        EnsureDataDirectoryExists();

        var path = GetAppConfigPath();
        var tomlText = SerializeAppConfig(config);

        try
        {
            File.WriteAllText(path, tomlText, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存配置文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新配置的部分字段
    /// </summary>
    public static void UpdateAppConfig(Action<AppConfig> updateAction)
    {
        var config = LoadAppConfig();
        updateAction(config);
        SaveAppConfig(config);
    }

    private static AppConfig ParseAppConfig(TomlTable model)
    {
        var config = new AppConfig();

        // [security]
        if (model.TryGetValue("security", out var secObj) && secObj is TomlTable sec)
        {
            config.Security.EnableStartupProtection = GetBool(sec, "enable_startup_protection", false);
            config.Security.PasswordHash = GetString(sec, "password_hash", "");
        }

        // [identity]
        if (model.TryGetValue("identity", out var idObj) && idObj is TomlTable id)
        {
            config.Identity.RememberDefault = GetBool(id, "remember_default", false);
            config.Identity.DefaultIdentityId = GetInt(id, "default_identity_id", 0);
        }

        // [captcha]
        if (model.TryGetValue("captcha", out var capObj) && capObj is TomlTable cap)
        {
            config.Captcha.Mode = GetString(cap, "mode", "manual");
            config.Captcha.RemoteOcrHost = GetString(cap, "remote_ocr_host", "");
            config.Captcha.RemoteOcrPort = GetInt(cap, "remote_ocr_port", 0);
            config.Captcha.OnnxModelPath = GetString(cap, "onnx_model_path", "");
            config.Captcha.OcrRetryCount = GetInt(cap, "ocr_retry_count", 3);
        }

        // [sync]
        if (model.TryGetValue("sync", out var syncObj) && syncObj is TomlTable sync)
        {
            config.Sync.MaxPages = GetInt(sync, "max_pages", 100);
            config.Sync.EarlyStopThreshold = GetInt(sync, "early_stop_threshold", 5);
            config.Sync.AutoMergeAfterSync = GetBool(sync, "auto_merge_after_sync", true);
        }

        // [data]
        if (model.TryGetValue("data", out var dataObj) && dataObj is TomlTable data)
        {
            config.Data.DataDirectory = GetString(data, "data_directory", "Data");
            config.Data.SnapshotKeepCount = GetInt(data, "snapshot_keep_count", 10);
        }

        // [classification]
        if (model.TryGetValue("classification", out var clsObj) && clsObj is TomlTable cls)
        {
            config.Classification.RulesPath = GetString(cls, "rules_path", "");
            config.Classification.RulesUpdateUrl = GetString(cls, "rules_update_url", "");
        }

        // [update]
        if (model.TryGetValue("update", out var updObj) && updObj is TomlTable upd)
        {
            config.Update.AutoCheck = GetBool(upd, "auto_check", true);
            config.Update.CheckIntervalHours = GetInt(upd, "check_interval_hours", 24);
            config.Update.LastCheckTime = GetString(upd, "last_check_time", "");
        }

        // [ui]
        if (model.TryGetValue("ui", out var uiObj) && uiObj is TomlTable ui)
        {
            config.Ui.Theme = GetString(ui, "theme", "light");
            config.Ui.Language = GetString(ui, "language", "zh-CN");
        }

        return config;
    }

    private static string SerializeAppConfig(AppConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 上海海事大学终端应用 — 全局配置文件");
        sb.AppendLine();

        sb.AppendLine("[security]");
        sb.AppendLine($"enable_startup_protection = {config.Security.EnableStartupProtection.ToString().ToLower()}");
        sb.AppendLine($"password_hash = \"{config.Security.PasswordHash}\"");
        sb.AppendLine();

        sb.AppendLine("[identity]");
        sb.AppendLine($"remember_default = {config.Identity.RememberDefault.ToString().ToLower()}");
        sb.AppendLine($"default_identity_id = {config.Identity.DefaultIdentityId}");
        sb.AppendLine();

        sb.AppendLine("[captcha]");
        sb.AppendLine($"mode = \"{config.Captcha.Mode}\"");
        sb.AppendLine($"remote_ocr_host = \"{config.Captcha.RemoteOcrHost}\"");
        sb.AppendLine($"remote_ocr_port = {config.Captcha.RemoteOcrPort}");
        sb.AppendLine($"onnx_model_path = \"{config.Captcha.OnnxModelPath}\"");
        sb.AppendLine($"ocr_retry_count = {config.Captcha.OcrRetryCount}");
        sb.AppendLine();

        sb.AppendLine("[sync]");
        sb.AppendLine($"max_pages = {config.Sync.MaxPages}");
        sb.AppendLine($"early_stop_threshold = {config.Sync.EarlyStopThreshold}");
        sb.AppendLine($"auto_merge_after_sync = {config.Sync.AutoMergeAfterSync.ToString().ToLower()}");
        sb.AppendLine();

        sb.AppendLine("[data]");
        sb.AppendLine($"data_directory = \"{config.Data.DataDirectory}\"");
        sb.AppendLine($"snapshot_keep_count = {config.Data.SnapshotKeepCount}");
        sb.AppendLine();

        sb.AppendLine("[classification]");
        sb.AppendLine($"rules_path = \"{config.Classification.RulesPath}\"");
        sb.AppendLine($"rules_update_url = \"{config.Classification.RulesUpdateUrl}\"");
        sb.AppendLine();

        sb.AppendLine("[update]");
        sb.AppendLine($"auto_check = {config.Update.AutoCheck.ToString().ToLower()}");
        sb.AppendLine($"check_interval_hours = {config.Update.CheckIntervalHours}");
        sb.AppendLine($"last_check_time = \"{config.Update.LastCheckTime}\"");
        sb.AppendLine();

        sb.AppendLine("[ui]");
        sb.AppendLine($"theme = \"{config.Ui.Theme}\"");
        sb.AppendLine($"language = \"{config.Ui.Language}\"");

        return sb.ToString();
    }

    #endregion

    #region classification_rules.toml

    /// <summary>
    /// 获取 classification_rules.toml 文件路径
    /// </summary>
    public static string GetClassificationRulesPath(AppConfig? appConfig = null)
    {
        appConfig ??= LoadAppConfig();
        if (!string.IsNullOrEmpty(appConfig.Classification.RulesPath))
            return appConfig.Classification.RulesPath;
        return Path.Combine(DataDirectory, "classification_rules.toml");
    }

    /// <summary>
    /// 加载分类规则
    /// </summary>
    public static ClassificationRules LoadClassificationRules()
    {
        var appConfig = LoadAppConfig();
        var path = GetClassificationRulesPath(appConfig);

        if (!File.Exists(path))
        {
            var defaultRules = CreateDefaultClassificationRules();
            SaveClassificationRules(defaultRules);
            return defaultRules;
        }

        try
        {
            var tomlText = File.ReadAllText(path);
            return ParseClassificationRules(tomlText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载分类规则失败: {ex.Message}");
            return CreateDefaultClassificationRules();
        }
    }

    /// <summary>
    /// 保存分类规则
    /// </summary>
    public static void SaveClassificationRules(ClassificationRules rules)
    {
        EnsureDataDirectoryExists();

        var appConfig = LoadAppConfig();
        var path = GetClassificationRulesPath(appConfig);
        var tomlText = SerializeClassificationRules(rules);

        File.WriteAllText(path, tomlText, Encoding.UTF8);
    }

    /// <summary>
    /// 从 GitHub 远程更新分类规则
    /// </summary>
    public static async Task<bool> UpdateClassificationRulesFromRemote(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var tomlText = await response.Content.ReadAsStringAsync(cancellationToken);

            // 验证 TOML 格式
            TomlSerializer.Deserialize<TomlTable>(tomlText);

            // 备份当前规则
            var appConfig = LoadAppConfig();
            var path = GetClassificationRulesPath(appConfig);
            if (File.Exists(path))
            {
                var backupPath = path + ".bak";
                File.Copy(path, backupPath, true);
            }

            // 保存新规则
            File.WriteAllText(path, tomlText, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"更新分类规则失败: {ex.Message}");
            return false;
        }
    }

    private static ClassificationRules ParseClassificationRules(string tomlText)
    {
        var model = TomlSerializer.Deserialize<TomlTable>(tomlText);
        var rules = new ClassificationRules();

        if (model is null) return rules;

        // [type.*]
        if (model.TryGetValue("type", out var typeObj) && typeObj is TomlTable typeTable)
        {
            foreach (var kvp in typeTable)
            {
                if (kvp.Value is TomlTable typeRule)
                {
                    var rule = new TypeRule
                    {
                        Name = GetString(typeRule, "name", kvp.Key),
                        MatchField = GetString(typeRule, "match_field", "item_type"),
                    };

                    if (typeRule.TryGetValue("match_names", out var namesObj) && namesObj is TomlArray namesArr)
                    {
                        rule.MatchNames = namesArr.Cast<string>().ToList();
                    }

                    if (typeRule.TryGetValue("match_targets", out var targetsObj) && targetsObj is TomlArray targetsArr)
                    {
                        rule.MatchTargets = targetsArr.Cast<string>().ToList();
                    }

                    rules.Type[kvp.Key] = rule;
                }
            }
        }

        // [position]
        if (model.TryGetValue("position", out var posObj) && posObj is TomlTable posTable)
        {
            rules.Position.Field = GetString(posTable, "field", "target_user");

            if (posTable.TryGetValue("keywords", out var kwObj) && kwObj is TomlTable kwTable)
            {
                foreach (var kvp in kwTable)
                {
                    if (kvp.Value is TomlTable kwDetail)
                    {
                        rules.Position.Keywords[kvp.Key] = new PositionKeyword
                        {
                            Building = GetString(kwDetail, "building", ""),
                            Room = GetString(kwDetail, "room", ""),
                        };
                    }
                }
            }
        }

        // [[schedule]]
        if (model.TryGetValue("schedule", out var schedObj) && schedObj is TomlArray schedArr)
        {
            foreach (var item in schedArr)
            {
                if (item is not TomlTable schedTable) continue;

                var schedule = new ScheduleRule();

                // [schedule.valid_date]
                if (schedTable.TryGetValue("valid_date", out var vdObj) && vdObj is TomlTable vdTable)
                {
                    schedule.ValidDate = new DateRange
                    {
                        StartDate = GetString(vdTable, "start_date", ""),
                        EndDate = GetString(vdTable, "end_date", "now"),
                    };
                }

                // [schedule.timetable.*]
                if (schedTable.TryGetValue("timetable", out var ttObj) && ttObj is TomlTable ttTable)
                {
                    foreach (var kvp in ttTable)
                    {
                        if (kvp.Value is TomlTable mealTable)
                        {
                            schedule.Timetable[kvp.Key] = new MealPeriod
                            {
                                Name = GetString(mealTable, "name", kvp.Key),
                                StartTime = GetString(mealTable, "start_time", ""),
                                EndTime = GetString(mealTable, "end_time", ""),
                            };
                        }
                    }
                }

                rules.Schedule.Add(schedule);
            }
        }

        return rules;
    }

    private static string SerializeClassificationRules(ClassificationRules rules)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 上海海事大学终端应用 — 账单分类规则");
        sb.AppendLine();

        // [type.*]
        sb.AppendLine("# 按消费类型分类");
        sb.AppendLine("[type]");
        foreach (var kvp in rules.Type)
        {
            sb.AppendLine();
            sb.AppendLine($"[type.{kvp.Key}]");
            sb.AppendLine($"name = \"{kvp.Value.Name}\"");
            sb.AppendLine($"match_field = \"{kvp.Value.MatchField}\"");
            if (kvp.Value.MatchNames.Count > 0)
            {
                sb.AppendLine($"match_names = [{string.Join(", ", kvp.Value.MatchNames.Select(n => $"\"{n}\""))}]");
            }
            if (kvp.Value.MatchTargets.Count > 0)
            {
                sb.AppendLine($"match_targets = [{string.Join(", ", kvp.Value.MatchTargets.Select(t => $"\"{t}\""))}]");
            }
        }

        // [position]
        sb.AppendLine();
        sb.AppendLine("# 按位置映射");
        sb.AppendLine("[position]");
        sb.AppendLine($"field = \"{rules.Position.Field}\"");
        foreach (var kvp in rules.Position.Keywords)
        {
            sb.AppendLine();
            sb.AppendLine($"[position.keywords.\"{kvp.Key}\"]");
            sb.AppendLine($"building = \"{kvp.Value.Building}\"");
            sb.AppendLine($"room = \"{kvp.Value.Room}\"");
        }

        // [[schedule]]
        foreach (var schedule in rules.Schedule)
        {
            sb.AppendLine();
            sb.AppendLine("[[schedule]]");
            sb.AppendLine("[schedule.valid_date]");
            sb.AppendLine($"start_date = \"{schedule.ValidDate.StartDate}\"");
            sb.AppendLine($"end_date = \"{schedule.ValidDate.EndDate}\"");

            sb.AppendLine("[schedule.timetable]");
            foreach (var kvp in schedule.Timetable)
            {
                sb.AppendLine();
                sb.AppendLine($"[schedule.timetable.{kvp.Key}]");
                sb.AppendLine($"name = \"{kvp.Value.Name}\"");
                sb.AppendLine($"start_time = \"{kvp.Value.StartTime}\"");
                sb.AppendLine($"end_time = \"{kvp.Value.EndTime}\"");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建默认分类规则
    /// </summary>
    private static ClassificationRules CreateDefaultClassificationRules()
    {
        var rules = new ClassificationRules();

        rules.Type["deposit"] = new TypeRule
        {
            Name = "充值", MatchField = "item_type",
            MatchNames = ["中行云充值", "微信充值"],
        };
        rules.Type["electricity"] = new TypeRule
        {
            Name = "电费", MatchField = "item_type",
            MatchNames = ["电费缴费"],
        };
        rules.Type["bath"] = new TypeRule
        {
            Name = "洗澡", MatchField = "target_user",
            MatchTargets = ["淋浴", "热水"],
        };
        rules.Type["hot_water"] = new TypeRule
        {
            Name = "热水", MatchField = "item_type",
            MatchNames = ["水控转账"],
        };
        rules.Type["cake"] = new TypeRule
        {
            Name = "蛋糕", MatchField = "target_user",
            MatchTargets = ["北区西点房"],
        };
        rules.Type["canteen"] = new TypeRule
        {
            Name = "食堂", MatchField = "target_user",
            MatchTargets = ["食堂", "餐厅"],
        };

        rules.Position.Field = "target_user";
        rules.Position.Keywords["A食堂1楼大餐厅"] = new PositionKeyword { Building = "海馨楼", Room = "海馨第1食堂" };
        rules.Position.Keywords["A食堂2楼大餐厅"] = new PositionKeyword { Building = "海馨楼", Room = "海馨第2食堂" };
        rules.Position.Keywords["A食堂2楼小餐厅"] = new PositionKeyword { Building = "海馨楼", Room = "海馨第2食堂小厅" };
        rules.Position.Keywords["B食堂1楼"] = new PositionKeyword { Building = "海琴楼", Room = "海琴1楼" };
        rules.Position.Keywords["B食堂2楼"] = new PositionKeyword { Building = "海琴楼", Room = "海琴2楼" };
        rules.Position.Keywords["C1大餐厅"] = new PositionKeyword { Building = "海联楼", Room = "海联1楼" };
        rules.Position.Keywords["C2大餐厅"] = new PositionKeyword { Building = "海联楼", Room = "海联2楼" };

        rules.Schedule.Add(new ScheduleRule
        {
            ValidDate = new DateRange { StartDate = "2019.9.1", EndDate = "now" },
            Timetable = new Dictionary<string, MealPeriod>
            {
                ["breakfast"] = new() { Name = "早餐", StartTime = "6:30", EndTime = "8:30" },
                ["lunch"] = new() { Name = "午餐", StartTime = "10:45", EndTime = "12:30" },
                ["dinner"] = new() { Name = "晚餐", StartTime = "16:30", EndTime = "18:15" },
                ["midnight_snack"] = new() { Name = "夜宵", StartTime = "18:15", EndTime = "21:00" },
            },
        });

        return rules;
    }

    #endregion

    #region 辅助方法

    private static void EnsureDataDirectoryExists()
    {
        if (!Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }
    }

    private static string GetString(TomlTable table, string key, string defaultValue)
    {
        if (table.TryGetValue(key, out var value) && value is string str)
            return str;
        return defaultValue;
    }

    private static bool GetBool(TomlTable table, string key, bool defaultValue)
    {
        if (table.TryGetValue(key, out var value) && value is bool b)
            return b;
        return defaultValue;
    }

    private static int GetInt(TomlTable table, string key, int defaultValue)
    {
        if (table.TryGetValue(key, out var value) && value is long l)
            return (int)l;
        if (value is int i)
            return i;
        return defaultValue;
    }

    #endregion
}
