/**
 * Program.cs — 轻量测试运行器（无第三方测试框架，离线可跑）。
 *
 * 每个断言失败计入 failures，退出码 0=全过 / 1=有失败。
 * CI 通过 `dotnet run --project FrontStudy.Api.Tests` 执行并检查退出码。
 */
using System.Text.Json;
using FrontStudy.Api.Services;

var failures = new List<string>();
var passed = 0;

void Check(bool cond, string name)
{
    if (cond) passed++;
    else failures.Add(name);
}

// ---------------- PersonaForgeService（规则抽取，确定性） ----------------
var svc = new PersonaForgeService();

var r1 = svc.Forge(
    "第1章 信号塔\n隗辛说：\"队长，目标确认了。\"\n旁白：隗辛收起枪，动作很轻。",
    "隗辛", "测试作品", "第1章");
Check(r1.Success, "Forge 抽取成功");
Check(r1.Summary!.QuoteCount >= 1, "Forge 至少抽到 1 条台词");
Check(r1.SkillMarkdown!.Contains("隗辛"), "SkillMarkdown 含角色名");
Check(r1.Slug!.StartsWith("persona-"), "Slug 以 persona- 开头");

Check(!svc.Forge("   ", "隗辛", null, null).Success, "空文本返回失败");
Check(!svc.Forge("正文文本", "  ", null, null).Success, "空角色名返回失败");

// ---------------- LlmChatClient.ExtractJsonObject（纯函数） ----------------
Check(LlmChatClient.ExtractJsonObject("{\"a\":1}") == "{\"a\":1}", "纯 JSON");
Check(LlmChatClient.ExtractJsonObject("```json\n{\"a\":1}\n```") == "{\"a\":1}", "去 markdown 围栏");
Check(LlmChatClient.ExtractJsonObject("解释：{\"a\":1} 完成") == "{\"a\":1}", "取首尾花括号");

// ---------------- StringListConverter（容错：字符串→数组） ----------------
var options = new JsonSerializerOptions();
options.Converters.Add(new StringListConverter());
var list = JsonSerializer.Deserialize<List<string>>("\"证据不足\"", options);
Check(list is { Count: 1 } && list[0] == "证据不足", "StringListConverter 字符串→单元素数组");

var list2 = JsonSerializer.Deserialize<List<string>>("[\"a\",\"b\"]", options);
Check(list2 is { Count: 2 } && list2[1] == "b", "StringListConverter 数组原样解析");

// ---------------- 汇总 ----------------
Console.WriteLine();
Console.WriteLine($"通过 {passed} 项，失败 {failures.Count} 项");
foreach (var f in failures) Console.WriteLine("  [FAIL] " + f);
return failures.Count == 0 ? 0 : 1;
