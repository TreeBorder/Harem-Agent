using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

[Description("网络工具")]
public interface IWebTool
{
    [Description("网络搜索工具，输入关键词检索网络内容")]
    Task<string> WebSearch(string words);
}