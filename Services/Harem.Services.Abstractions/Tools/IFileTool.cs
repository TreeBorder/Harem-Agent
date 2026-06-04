using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

[Description("workspace file tool")]
public interface IFileTool
{
    [Description("read file content")]
    Task<string> ReadFile(string fileName, int? startLine, int? endLine);

    [Description("write file content")]
    Task<string> WriteFile(string fileName, string content);
}