using System.ComponentModel;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;

namespace Harem.Services.Tools;

[Description("workspace file tool")]
public class FileTool : IFileTool
{
    private readonly IFileService _fileService;

    public FileTool(IFileService fileService)
    {
        _fileService = fileService;
    }

    [Description("read file content")]
    public async Task<string> ReadFile(string fileName, int? startLine = null, int? endLine = null)
    {
        var content = await _fileService.GetFileAsync(fileName, startLine, endLine);
        return string.IsNullOrEmpty(content) ? "读取的文件不存在！" : content;
    }

    [Description("write file content")]
    public async Task<string> WriteFile(string fileName, string content)
    {
        var result = await _fileService.WriteFileAsync(fileName, content);
        return !result ? "写入失败！" : "写入成功！";
    }
}